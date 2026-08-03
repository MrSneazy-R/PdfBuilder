using PdfBuilder.Elements;
using PdfBuilder.Models;
using PdfBuilder.Writer;

namespace PdfBuilder.Document;

/// <summary>Describes a document composed through PdfBuilder's canonical API.</summary>
public interface IDocumentDescriptor
{
    /// <summary>Configures document metadata.</summary>
    void Metadata(Action<DocumentMetadata> configure);

    /// <summary>Adds and configures a page.</summary>
    void Page(Action<IPageDescriptor> configure);
}

/// <summary>Describes one coordinate-free document page.</summary>
public interface IPageDescriptor
{
    /// <summary>Sets the page size in points.</summary>
    void Size(PageSize size);
    /// <summary>Sets the page orientation.</summary>
    void Orientation(PageOrientation orientation);
    /// <summary>Sets a uniform page margin in points.</summary>
    void Margin(float value);
    /// <summary>Sets page margins in points.</summary>
    void Margin(float left, float top, float right, float bottom);
    /// <summary>Configures the default text style for page content.</summary>
    void DefaultTextStyle(Action<ITextStyleDescriptor> configure);
    /// <summary>Returns the root content container.</summary>
    IContainer Content();
}

/// <summary>Represents a container that can receive canonical content.</summary>
public interface IContainer
{
    /// <summary>Adds text and returns its style descriptor.</summary>
    ITextDescriptor Text(string text);
    /// <summary>Adds a vertical column.</summary>
    void Column(Action<IColumnDescriptor> configure);
    /// <summary>Adds a horizontal row.</summary>
    void Row(Action<IRowDescriptor> configure);
}

/// <summary>Describes a vertical column.</summary>
public interface IColumnDescriptor
{
    /// <summary>Sets spacing between column items in points.</summary>
    void Spacing(float value);
    /// <summary>Adds a column item.</summary>
    IContainer Item();
}

/// <summary>Describes a horizontal row.</summary>
public interface IRowDescriptor
{
    /// <summary>Adds a constant-width item in points.</summary>
    IContainer ConstantItem(float width);
    /// <summary>Adds a proportional-width item.</summary>
    IContainer RelativeItem(float weight = 1f);
}

/// <summary>Configures reusable text style settings.</summary>
public interface ITextStyleDescriptor
{
    /// <summary>Sets the font family.</summary>
    ITextStyleDescriptor FontFamily(string family);
    /// <summary>Sets the font size in points.</summary>
    ITextStyleDescriptor FontSize(float size);
    /// <summary>Uses a bold font style.</summary>
    ITextStyleDescriptor Bold();
}

/// <summary>Configures text content added to a container.</summary>
public interface ITextDescriptor : ITextStyleDescriptor
{
}

/// <summary>Defines the orientation applied to a page size.</summary>
public enum PageOrientation { Portrait, Landscape }

/// <summary>Represents a PDF page size in points.</summary>
public readonly record struct PageSize(float Width, float Height)
{
    /// <summary>Returns this size rotated to the requested orientation.</summary>
    public PageSize WithOrientation(PageOrientation orientation) => orientation == PageOrientation.Landscape
        ? (Width >= Height ? this : new PageSize(Height, Width))
        : (Height >= Width ? this : new PageSize(Height, Width));
}

/// <summary>Provides common page sizes in PDF points.</summary>
public static class PageSizes
{
    /// <summary>ISO A4 (595 × 842 points).</summary>
    public static PageSize A4 => new(595f, 842f);
    /// <summary>US Letter (612 × 792 points).</summary>
    public static PageSize Letter => new(612f, 792f);
    /// <summary>ISO A3 (842 × 1191 points).</summary>
    public static PageSize A3 => new(842f, 1191f);
}

public partial class PdfDocument
{
    /// <summary>Creates a document using the canonical composition API.</summary>
    /// <param name="configure">Configures metadata and pages.</param>
    /// <returns>The composed document.</returns>
    public static PdfDocument Create(Action<IDocumentDescriptor> configure)
    {
        if (configure == null) throw new ArgumentNullException(nameof(configure));
        var document = new PdfDocument();
        configure(new CanonicalDocumentDescriptor(document));
        return document;
    }

    /// <summary>Generates the document as PDF bytes.</summary>
    public byte[] GenerateBytes() => new PdfWriter().GenerateBytes(this);

    /// <summary>Generates the document into a writable stream.</summary>
    public void Generate(Stream destination) => new PdfWriter().GenerateStream(this, destination);

    /// <summary>Generates and saves the document to a file path.</summary>
    public void Save(string path) => new PdfWriter().Save(this, path);

    private sealed class CanonicalDocumentDescriptor : IDocumentDescriptor
    {
        private readonly PdfDocument _document;
        public CanonicalDocumentDescriptor(PdfDocument document) => _document = document;
        public void Metadata(Action<DocumentMetadata> configure)
        {
            if (configure == null) throw new ArgumentNullException(nameof(configure));
            configure(_document.Metadata);
            _document.Title = _document.Metadata.Title;
        }
        public void Page(Action<IPageDescriptor> configure)
        {
            if (configure == null) throw new ArgumentNullException(nameof(configure));
            var descriptor = new CanonicalPageDescriptor(_document);
            configure(descriptor);
            descriptor.Build();
        }
    }

    private sealed class CanonicalPageDescriptor : IPageDescriptor
    {
        private readonly PdfDocument _document;
        private readonly CanonicalContainer _content = new();
        private PageSize _size = PageSizes.Letter;
        private PageOrientation _orientation = PageOrientation.Portrait;
        private float _left = 40f, _top = 40f, _right = 40f, _bottom = 40f;
        private readonly CanonicalTextStyle _defaultStyle = new();

        public CanonicalPageDescriptor(PdfDocument document) => _document = document;
        public void Size(PageSize size)
        {
            if (size.Width <= 0 || size.Height <= 0) throw new ArgumentOutOfRangeException(nameof(size));
            _size = size;
        }
        public void Orientation(PageOrientation orientation) => _orientation = orientation;
        public void Margin(float value) => Margin(value, value, value, value);
        public void Margin(float left, float top, float right, float bottom)
        {
            if (left < 0 || top < 0 || right < 0 || bottom < 0) throw new ArgumentOutOfRangeException(nameof(left));
            _left = left; _top = top; _right = right; _bottom = bottom;
        }
        public void DefaultTextStyle(Action<ITextStyleDescriptor> configure)
        {
            if (configure == null) throw new ArgumentNullException(nameof(configure));
            configure(_defaultStyle);
        }
        public IContainer Content() => _content;
        public void Build()
        {
            var size = _size.WithOrientation(_orientation);
            var page = _document.AddPage(size.Width, size.Height);
            page.MarginLeft = _left; page.MarginTop = _top; page.MarginRight = _right; page.MarginBottom = _bottom;
            _defaultStyle.Apply(page.TextDefaults);
            new PdfPageBuilder(page, _document).Margin(0).AutoPaginate(_document).Content(column =>
                column.ComposeContent(composer => _content.Compose(composer)));
        }
    }

    private sealed class CanonicalContainer : IContainer
    {
        private readonly List<Action<Layout.ContentComposer>> _content = new();
        public ITextDescriptor Text(string text)
        {
            var descriptor = new CanonicalTextStyle();
            _content.Add(composer => composer.Text(text ?? string.Empty, descriptor.Apply));
            return descriptor;
        }
        public void Column(Action<IColumnDescriptor> configure)
        {
            if (configure == null) throw new ArgumentNullException(nameof(configure));
            var column = new CanonicalColumnDescriptor(); configure(column);
            _content.Add(composer => composer.Column(builder => column.Compose(builder)));
        }
        public void Row(Action<IRowDescriptor> configure)
        {
            if (configure == null) throw new ArgumentNullException(nameof(configure));
            var row = new CanonicalRowDescriptor(); configure(row);
            _content.Add(composer => composer.Row(builder => row.Compose(builder)));
        }
        public void Compose(Layout.ContentComposer composer) { foreach (var action in _content) action(composer); }
    }

    private sealed class CanonicalColumnDescriptor : IColumnDescriptor
    {
        private readonly List<CanonicalContainer> _items = new();
        private float _spacing = 8f;
        public void Spacing(float value) { if (value < 0) throw new ArgumentOutOfRangeException(nameof(value)); _spacing = value; }
        public IContainer Item() { var item = new CanonicalContainer(); _items.Add(item); return item; }
        public void Compose(Layout.LayoutComponentCollection.ColumnComponentBuilder builder)
        {
            builder.Spacing(_spacing);
            foreach (var item in _items) builder.Item(item.Compose);
        }
    }

    private sealed class CanonicalRowDescriptor : IRowDescriptor
    {
        private readonly List<(RowItemKind kind, float value, CanonicalContainer container)> _items = new();
        public IContainer ConstantItem(float width) => Add(RowItemKind.Constant, width);
        public IContainer RelativeItem(float weight = 1f) => Add(RowItemKind.Relative, weight);
        private IContainer Add(RowItemKind kind, float value)
        {
            if (value < 0 || (kind == RowItemKind.Relative && value == 0)) throw new ArgumentOutOfRangeException(nameof(value));
            var container = new CanonicalContainer(); _items.Add((kind, value, container)); return container;
        }
        public void Compose(Layout.LayoutComponentCollection.RowComponentBuilder builder)
        {
            foreach (var item in _items)
                switch (item.kind)
                {
                    case RowItemKind.Constant: builder.Constant(item.value, item.container.Compose); break;
                    case RowItemKind.Relative: builder.Relative(item.value, item.container.Compose); break;
                }
        }
        private enum RowItemKind { Constant, Relative }
    }

    private sealed class CanonicalTextStyle : ITextDescriptor
    {
        private string? _family; private float? _size; private bool _bold;
        public ITextStyleDescriptor FontFamily(string family) { _family = string.IsNullOrWhiteSpace(family) ? throw new ArgumentException("A font family is required.", nameof(family)) : family; return this; }
        public ITextStyleDescriptor FontSize(float size) { _size = size <= 0 ? throw new ArgumentOutOfRangeException(nameof(size)) : size; return this; }
        public ITextStyleDescriptor Bold() { _bold = true; return this; }
        public void Apply(TextStyleDefaults defaults) { if (_family != null) defaults.FontFamily = _family; if (_size.HasValue) defaults.FontSize = _size; if (_bold) defaults.Bold = true; }
        public void Apply(TextElement element) { if (_family != null) element.FontFamily = _family; if (_size.HasValue) element.FontSize = _size.Value; if (_bold) element.Bold = true; }
    }
}
