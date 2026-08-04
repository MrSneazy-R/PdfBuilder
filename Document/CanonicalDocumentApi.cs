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
    /// <summary>Applies uniform inner padding in points.</summary>
    IContainer Padding(float value);
    /// <summary>Applies per-side inner padding in points.</summary>
    IContainer Padding(float left, float top, float right, float bottom);
    /// <summary>Applies uniform outer margin in points.</summary>
    IContainer Margin(float value);
    /// <summary>Applies per-side outer margin in points.</summary>
    IContainer Margin(float left, float top, float right, float bottom);
    /// <summary>Paints a background behind this container.</summary>
    IContainer Background(string color);
    /// <summary>Draws a border around this container.</summary>
    IContainer Border(float width = 1f, string color = "#000000");
    /// <summary>Draws a border on the left side of this container.</summary>
    IContainer BorderLeft(float width = 1f, string color = "#000000");
    /// <summary>Draws a border on the top side of this container.</summary>
    IContainer BorderTop(float width = 1f, string color = "#000000");
    /// <summary>Draws a border on the right side of this container.</summary>
    IContainer BorderRight(float width = 1f, string color = "#000000");
    /// <summary>Draws a border on the bottom side of this container.</summary>
    IContainer BorderBottom(float width = 1f, string color = "#000000");
    /// <summary>Rounds the decoration corners by the supplied radius in points.</summary>
    IContainer CornerRadius(float value);
    /// <summary>Sets the opacity of this container's decoration.</summary>
    IContainer Opacity(float value);
    /// <summary>Aligns this container to the left.</summary>
    IContainer AlignLeft();
    /// <summary>Aligns this container horizontally in the centre.</summary>
    IContainer AlignCenter();
    /// <summary>Aligns this container to the right.</summary>
    IContainer AlignRight();
    /// <summary>Aligns this container to the top.</summary>
    IContainer AlignTop();
    /// <summary>Aligns this container vertically in the middle.</summary>
    IContainer AlignMiddle();
    /// <summary>Aligns this container to the bottom.</summary>
    IContainer AlignBottom();
    /// <summary>Sets an exact width in points.</summary>
    IContainer Width(float value);
    /// <summary>Sets an exact height in points.</summary>
    IContainer Height(float value);
    /// <summary>Sets a minimum width in points.</summary>
    IContainer MinWidth(float value);
    /// <summary>Sets a maximum width in points.</summary>
    IContainer MaxWidth(float value);
    /// <summary>Sets a minimum height in points.</summary>
    IContainer MinHeight(float value);
    /// <summary>Sets a maximum height in points.</summary>
    IContainer MaxHeight(float value);
    /// <summary>Sets the width-to-height aspect ratio.</summary>
    IContainer AspectRatio(float value);
    /// <summary>Extends this container to the available width and height.</summary>
    IContainer Extend();
    /// <summary>Shrinks this container to the available width and height.</summary>
    IContainer Shrink();
    /// <summary>Moves this container to the next page when less than the specified height is available.</summary>
    IContainer EnsureSpace(float minimumHeight);
    /// <summary>Keeps the container on one page when it can fit on a page.</summary>
    IContainer KeepTogether();
    /// <summary>Keeps this container with the next layout item when practical.</summary>
    IContainer KeepWithNext();
    /// <summary>Includes this container only when <paramref name="condition"/> is true.</summary>
    IContainer ShowIf(bool condition);
    /// <summary>Forces subsequent content onto a new page.</summary>
    IContainer PageBreak();
    /// <summary>Adds text and returns its style descriptor.</summary>
    ITextDescriptor Text(string text);
    /// <summary>Adds a rich-text paragraph with independently styled spans.</summary>
    void RichText(Action<IRichTextDescriptor> configure);
    /// <summary>Adds a vertical column.</summary>
    void Column(Action<IColumnDescriptor> configure);
    /// <summary>Adds a horizontal row.</summary>
    void Row(Action<IRowDescriptor> configure);
    /// <summary>Adds a grid.</summary>
    void Grid(Action<IGridDescriptor> configure);
    /// <summary>Adds stacked content layers.</summary>
    void Stack(Action<IStackDescriptor> configure);
    /// <summary>Adds background, content, and foreground layers.</summary>
    void Layer(Action<ILayerDescriptor> configure);
    /// <summary>Repeats content a fixed number of times.</summary>
    void Repeat(int count, Action<int, IContainer> configure);
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
    /// <summary>Adds an item sized by the row layout.</summary>
    IContainer AutoItem();
}

/// <summary>Describes a grid layout.</summary>
public interface IGridDescriptor
{
    /// <summary>Sets the number of columns.</summary>
    void Columns(int value);
    /// <summary>Sets the gap between grid rows in points.</summary>
    void RowSpacing(float value);
    /// <summary>Sets the gap between grid columns in points.</summary>
    void ColumnSpacing(float value);
    /// <summary>Adds a grid item.</summary>
    IContainer Item();
}

/// <summary>Describes a stack layout.</summary>
public interface IStackDescriptor
{
    /// <summary>Adds a stack item.</summary>
    IContainer Item();
}

/// <summary>Describes explicit background, content, and foreground layers.</summary>
public interface ILayerDescriptor
{
    /// <summary>Configures the background layer.</summary>
    IContainer Background();
    /// <summary>Configures the content layer.</summary>
    IContainer Content();
    /// <summary>Configures the foreground layer.</summary>
    IContainer Foreground();
}

/// <summary>Provides explicit unit conversion helpers for layout values.</summary>
public static class Units
{
    /// <summary>Returns a value expressed in PDF points.</summary>
    public static float Points(float value) => value;
    /// <summary>Converts millimetres to PDF points.</summary>
    public static float Millimeters(float value) => value * 72f / 25.4f;
    /// <summary>Converts centimetres to PDF points.</summary>
    public static float Centimeters(float value) => Millimeters(value * 10f);
    /// <summary>Converts inches to PDF points.</summary>
    public static float Inches(float value) => value * 72f;
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
    /// <summary>Uses an italic font style.</summary>
    ITextStyleDescriptor Italic();
    /// <summary>Sets the text colour as a #RRGGBB value.</summary>
    ITextStyleDescriptor Color(string color);
    /// <summary>Sets the line-height multiplier.</summary>
    ITextStyleDescriptor LineHeight(float value);
    /// <summary>Sets extra spacing between glyphs in points.</summary>
    ITextStyleDescriptor LetterSpacing(float value);
    /// <summary>Sets extra spacing for whitespace glyphs in points.</summary>
    ITextStyleDescriptor WordSpacing(float value);
    /// <summary>Draws an underline beneath the text.</summary>
    ITextStyleDescriptor Underline();
    /// <summary>Draws a strikethrough through the text.</summary>
    ITextStyleDescriptor Strikethrough();
    /// <summary>Raises the text relative to its baseline.</summary>
    ITextStyleDescriptor Superscript();
    /// <summary>Lowers the text relative to its baseline.</summary>
    ITextStyleDescriptor Subscript();
    /// <summary>Aligns text to the left of its available width.</summary>
    ITextStyleDescriptor AlignLeft();
    /// <summary>Centers text within its available width.</summary>
    ITextStyleDescriptor AlignCenter();
    /// <summary>Aligns text to the right of its available width.</summary>
    ITextStyleDescriptor AlignRight();
    /// <summary>Justifies all non-final wrapped lines.</summary>
    ITextStyleDescriptor Justify();
    /// <summary>Sets the paragraph text direction.</summary>
    ITextStyleDescriptor Direction(FlowDirection direction);
    /// <summary>Prevents automatic line wrapping.</summary>
    ITextStyleDescriptor NoWrap();
    /// <summary>Enables automatic line wrapping.</summary>
    ITextStyleDescriptor Wrap();
    /// <summary>Uses an ellipsis when a no-wrap paragraph is constrained.</summary>
    ITextStyleDescriptor Ellipsis();
}

/// <summary>Configures text content added to a container.</summary>
public interface ITextDescriptor : ITextStyleDescriptor
{
}

/// <summary>Configures a rich-text paragraph.</summary>
public interface IRichTextDescriptor
{
    /// <summary>Sets paragraph text styling.</summary>
    ITextStyleDescriptor DefaultStyle();
    /// <summary>Adds a text span and returns its style descriptor.</summary>
    ITextStyleDescriptor Span(string text);
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
        private float? _paddingLeft, _paddingTop, _paddingRight, _paddingBottom;
        private float? _marginLeft, _marginTop, _marginRight, _marginBottom;
        private string? _background;
        private readonly BorderValues _border = new();
        private float _cornerRadius;
        private float _opacity = 1f;
        private Layout.Components.LayoutHorizontalAlignment _horizontal = Layout.Components.LayoutHorizontalAlignment.Left;
        private Layout.Components.LayoutVerticalAlignment _vertical = Layout.Components.LayoutVerticalAlignment.Top;
        private float? _width, _height, _minWidth, _maxWidth, _minHeight, _maxHeight, _aspectRatio, _ensureSpace;
        private bool _extend, _shrink, _keepTogether, _keepWithNext, _visible = true;

        public IContainer Padding(float value) => Padding(value, value, value, value);
        public IContainer Padding(float left, float top, float right, float bottom)
        {
            ValidateNonNegative(left, nameof(left)); ValidateNonNegative(top, nameof(top)); ValidateNonNegative(right, nameof(right)); ValidateNonNegative(bottom, nameof(bottom));
            _paddingLeft = left; _paddingTop = top; _paddingRight = right; _paddingBottom = bottom; return this;
        }
        public IContainer Margin(float value) => Margin(value, value, value, value);
        public IContainer Margin(float left, float top, float right, float bottom)
        {
            ValidateNonNegative(left, nameof(left)); ValidateNonNegative(top, nameof(top)); ValidateNonNegative(right, nameof(right)); ValidateNonNegative(bottom, nameof(bottom));
            _marginLeft = left; _marginTop = top; _marginRight = right; _marginBottom = bottom; return this;
        }
        public IContainer Background(string color) { _background = ValidateColor(color); return this; }
        public IContainer Border(float width = 1f, string color = "#000000") { _border.SetAll(width, color); return this; }
        public IContainer BorderLeft(float width = 1f, string color = "#000000") { _border.Left = BorderValues.Create(width, color); return this; }
        public IContainer BorderTop(float width = 1f, string color = "#000000") { _border.Top = BorderValues.Create(width, color); return this; }
        public IContainer BorderRight(float width = 1f, string color = "#000000") { _border.Right = BorderValues.Create(width, color); return this; }
        public IContainer BorderBottom(float width = 1f, string color = "#000000") { _border.Bottom = BorderValues.Create(width, color); return this; }
        public IContainer CornerRadius(float value) { ValidateNonNegative(value, nameof(value)); _cornerRadius = value; return this; }
        public IContainer Opacity(float value) { if (value < 0f || value > 1f || float.IsNaN(value)) throw new ArgumentOutOfRangeException(nameof(value)); _opacity = value; return this; }
        public IContainer AlignLeft() { _horizontal = Layout.Components.LayoutHorizontalAlignment.Left; return this; }
        public IContainer AlignCenter() { _horizontal = Layout.Components.LayoutHorizontalAlignment.Center; return this; }
        public IContainer AlignRight() { _horizontal = Layout.Components.LayoutHorizontalAlignment.Right; return this; }
        public IContainer AlignTop() { _vertical = Layout.Components.LayoutVerticalAlignment.Top; return this; }
        public IContainer AlignMiddle() { _vertical = Layout.Components.LayoutVerticalAlignment.Middle; return this; }
        public IContainer AlignBottom() { _vertical = Layout.Components.LayoutVerticalAlignment.Bottom; return this; }
        public IContainer Width(float value) { _width = ValidateDimension(value, nameof(value)); return this; }
        public IContainer Height(float value) { _height = ValidateDimension(value, nameof(value)); return this; }
        public IContainer MinWidth(float value) { _minWidth = ValidateDimension(value, nameof(value)); return this; }
        public IContainer MaxWidth(float value) { _maxWidth = ValidateDimension(value, nameof(value)); return this; }
        public IContainer MinHeight(float value) { _minHeight = ValidateDimension(value, nameof(value)); return this; }
        public IContainer MaxHeight(float value) { _maxHeight = ValidateDimension(value, nameof(value)); return this; }
        public IContainer AspectRatio(float value) { if (value <= 0f || float.IsNaN(value) || float.IsInfinity(value)) throw new ArgumentOutOfRangeException(nameof(value)); _aspectRatio = value; return this; }
        public IContainer Extend() { _extend = true; return this; }
        public IContainer Shrink() { _shrink = true; return this; }
        public IContainer EnsureSpace(float minimumHeight) { _ensureSpace = ValidateDimension(minimumHeight, nameof(minimumHeight)); return this; }
        public IContainer KeepTogether() { _keepTogether = true; return this; }
        public IContainer KeepWithNext() { _keepWithNext = true; return this; }
        public IContainer ShowIf(bool condition) { _visible &= condition; return this; }
        public IContainer PageBreak() { _content.Add(composer => composer.PageBreak()); return this; }
        public ITextDescriptor Text(string text)
        {
            var descriptor = new CanonicalTextStyle();
            _content.Add(composer => composer.Text(text ?? string.Empty, descriptor.Apply));
            return descriptor;
        }
        public void RichText(Action<IRichTextDescriptor> configure)
        {
            if (configure == null) throw new ArgumentNullException(nameof(configure));
            var descriptor = new CanonicalRichTextDescriptor();
            configure(descriptor);
            _content.Add(composer => composer.RichText(descriptor.Apply));
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
        public void Grid(Action<IGridDescriptor> configure)
        {
            if (configure == null) throw new ArgumentNullException(nameof(configure));
            var grid = new CanonicalGridDescriptor(); configure(grid);
            _content.Add(composer => composer.Grid(builder => grid.Compose(builder)));
        }
        public void Stack(Action<IStackDescriptor> configure)
        {
            if (configure == null) throw new ArgumentNullException(nameof(configure));
            var stack = new CanonicalStackDescriptor(); configure(stack);
            _content.Add(composer => composer.Stack(builder => stack.Compose(builder)));
        }
        public void Layer(Action<ILayerDescriptor> configure)
        {
            if (configure == null) throw new ArgumentNullException(nameof(configure));
            var layer = new CanonicalLayerDescriptor(); configure(layer);
            _content.Add(composer => composer.Layer(builder => layer.Compose(builder)));
        }
        public void Repeat(int count, Action<int, IContainer> configure)
        {
            if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
            if (configure == null) throw new ArgumentNullException(nameof(configure));
            for (var index = 0; index < count; index++) { var item = new CanonicalContainer(); configure(index, item); _content.Add(item.Compose); }
        }
        public void Compose(Layout.ContentComposer composer)
        {
            if (!_visible) { composer.Component(new Layout.Components.EmptyComponent()); return; }
            ValidateConstraints();
            Action<Layout.ContentComposer> content = ComposeCore;
            if (_paddingLeft.HasValue) { var next = content; content = inner => inner.Padding(_paddingLeft.Value, _paddingTop!.Value, _paddingRight!.Value, _paddingBottom!.Value, next); }
            if (_background != null || _border.HasAny) { var next = content; content = inner => inner.Decorate(decoration => ConfigureDecoration(decoration), next); }
            if (_marginLeft.HasValue) { var next = content; content = inner => inner.Padding(_marginLeft.Value, _marginTop!.Value, _marginRight!.Value, _marginBottom!.Value, next); }
            if (_width.HasValue || _height.HasValue || _minWidth.HasValue || _maxWidth.HasValue || _minHeight.HasValue || _maxHeight.HasValue || _aspectRatio.HasValue || _extend || _shrink)
            { var next = content; content = inner => inner.Size(next, _minWidth, _maxWidth, _width, _minHeight, _maxHeight, _height, _aspectRatio, _extend, _extend, _shrink, _shrink); }
            if (_horizontal != Layout.Components.LayoutHorizontalAlignment.Left || _vertical != Layout.Components.LayoutVerticalAlignment.Top)
            { var next = content; content = inner => inner.Align(_horizontal, _vertical, next, _ensureSpace); }
            else if (_ensureSpace.HasValue) { var next = content; content = inner => inner.EnsureSpace(_ensureSpace.Value, next); }
            if (_keepTogether || _keepWithNext) { var next = content; content = inner => inner.KeepTogether(next); }
            content(composer);
        }
        private void ComposeCore(Layout.ContentComposer composer) { foreach (var action in _content) action(composer); }
        private void ConfigureDecoration(Layout.LayoutComponentCollection.DecorationBuilder decoration)
        {
            decoration.Background(context =>
            {
                var rect = context.Rect;
                if (_background != null) context.Page.AddElement(new SolidRectElement(rect.X, rect.Bottom, rect.Width, rect.Height) { FillColor = _background, Opacity = _opacity, CornerRadius = _cornerRadius });
            });
            decoration.Foreground(context => _border.Add(context, _cornerRadius, _opacity));
        }
        private void ValidateConstraints()
        {
            if (_minWidth.HasValue && _maxWidth.HasValue && _minWidth > _maxWidth) throw new InvalidOperationException("Minimum width cannot exceed maximum width.");
            if (_minHeight.HasValue && _maxHeight.HasValue && _minHeight > _maxHeight) throw new InvalidOperationException("Minimum height cannot exceed maximum height.");
            if (_width.HasValue && ((_minWidth.HasValue && _width < _minWidth) || (_maxWidth.HasValue && _width > _maxWidth))) throw new InvalidOperationException("Width conflicts with its minimum or maximum constraint.");
            if (_height.HasValue && ((_minHeight.HasValue && _height < _minHeight) || (_maxHeight.HasValue && _height > _maxHeight))) throw new InvalidOperationException("Height conflicts with its minimum or maximum constraint.");
        }
        private static float ValidateDimension(float value, string name) { ValidateNonNegative(value, name); return value; }
        private static void ValidateNonNegative(float value, string name) { if (value < 0f || float.IsNaN(value) || float.IsInfinity(value)) throw new ArgumentOutOfRangeException(name); }
        private static string ValidateColor(string color) => string.IsNullOrWhiteSpace(color) ? throw new ArgumentException("A color is required.", nameof(color)) : color;
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
        public IContainer AutoItem() => Add(RowItemKind.Auto, 0f);
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
                    case RowItemKind.Auto: builder.Auto(item.container.Compose); break;
                }
        }
        private enum RowItemKind { Constant, Relative, Auto }
    }

    private sealed class CanonicalGridDescriptor : IGridDescriptor
    {
        private readonly List<CanonicalContainer> _items = new();
        private int _columns = 1;
        private float _rowGap = 8f, _columnGap = 8f;
        public void Columns(int value) { if (value <= 0) throw new ArgumentOutOfRangeException(nameof(value)); _columns = value; }
        public void RowSpacing(float value) { if (value < 0) throw new ArgumentOutOfRangeException(nameof(value)); _rowGap = value; }
        public void ColumnSpacing(float value) { if (value < 0) throw new ArgumentOutOfRangeException(nameof(value)); _columnGap = value; }
        public IContainer Item() { var item = new CanonicalContainer(); _items.Add(item); return item; }
        public void Compose(Layout.LayoutComponentCollection.GridComponentBuilder builder)
        {
            builder.Columns(_columns).RowGap(_rowGap).ColumnGap(_columnGap);
            foreach (var item in _items) builder.Item(item.Compose);
        }
    }

    private sealed class CanonicalStackDescriptor : IStackDescriptor
    {
        private readonly List<CanonicalContainer> _items = new();
        public IContainer Item() { var item = new CanonicalContainer(); _items.Add(item); return item; }
        public void Compose(Layout.LayoutComponentCollection.StackComponentBuilder builder) { foreach (var item in _items) builder.Item(item.Compose); }
    }

    private sealed class CanonicalLayerDescriptor : ILayerDescriptor
    {
        private readonly CanonicalContainer _background = new();
        private readonly CanonicalContainer _content = new();
        private readonly CanonicalContainer _foreground = new();
        private bool _hasBackground, _hasContent, _hasForeground;
        public IContainer Background() { _hasBackground = true; return _background; }
        public IContainer Content() { _hasContent = true; return _content; }
        public IContainer Foreground() { _hasForeground = true; return _foreground; }
        public void Compose(Layout.LayoutComponentCollection.LayerBuilder builder)
        {
            if (!_hasBackground && !_hasContent && !_hasForeground) throw new InvalidOperationException("Layer requires at least one child.");
            if (_hasBackground) builder.Background(collection => _background.Compose(new Layout.ContentComposer(collection)));
            if (_hasContent) builder.Content(collection => _content.Compose(new Layout.ContentComposer(collection)));
            if (_hasForeground) builder.Foreground(collection => _foreground.Compose(new Layout.ContentComposer(collection)));
        }
    }

    private sealed class BorderValues
    {
        internal BorderSide? Left { get; set; }
        internal BorderSide? Top { get; set; }
        internal BorderSide? Right { get; set; }
        internal BorderSide? Bottom { get; set; }
        internal bool HasAny => Left.HasValue || Top.HasValue || Right.HasValue || Bottom.HasValue;
        internal static BorderSide Create(float width, string color)
        {
            if (width < 0f || float.IsNaN(width) || float.IsInfinity(width)) throw new ArgumentOutOfRangeException(nameof(width));
            if (string.IsNullOrWhiteSpace(color)) throw new ArgumentException("A border color is required.", nameof(color));
            return new BorderSide(width, color);
        }
        internal void SetAll(float width, string color) { var side = Create(width, color); Left = Top = Right = Bottom = side; }
        internal void Add(Layout.DecorationDrawContext context, float cornerRadius, float opacity)
        {
            var rect = context.Rect;
            if (HasUniformBorder(out var uniform))
            {
                context.Page.AddElement(new SolidRectElement(rect.X, rect.Bottom, rect.Width, rect.Height) { StrokeColor = uniform.Color, StrokeWidth = uniform.Width, Opacity = opacity, CornerRadius = cornerRadius });
                return;
            }
            AddSide(context, Left, rect.X, rect.Bottom, rect.Height, true, opacity);
            AddSide(context, Right, rect.X + rect.Width, rect.Bottom, rect.Height, true, opacity);
            AddSide(context, Top, rect.X, rect.Bottom + rect.Height, rect.Width, false, opacity);
            AddSide(context, Bottom, rect.X, rect.Bottom, rect.Width, false, opacity);
        }
        private bool HasUniformBorder(out BorderSide side)
        {
            side = default;
            if (!Left.HasValue || !Top.HasValue || !Right.HasValue || !Bottom.HasValue) return false;
            if (Left.Value != Top.Value || Left.Value != Right.Value || Left.Value != Bottom.Value) return false;
            side = Left.Value; return true;
        }
        private static void AddSide(Layout.DecorationDrawContext context, BorderSide? side, float x, float y, float length, bool vertical, float opacity)
        {
            if (!side.HasValue || side.Value.Width <= 0f) return;
            float width = vertical ? side.Value.Width : length;
            float height = vertical ? length : side.Value.Width;
            if (vertical && x > context.Rect.X) x -= side.Value.Width;
            if (!vertical && y > context.Rect.Bottom) y -= side.Value.Width;
            context.Page.AddElement(new SolidRectElement(x, y, width, height) { FillColor = side.Value.Color, Opacity = opacity });
        }
        internal readonly record struct BorderSide(float Width, string Color);
    }

    private sealed class CanonicalTextStyle : ITextDescriptor
    {
        private string? _family, _color;
        private float? _size, _lineHeight, _letterSpacing, _wordSpacing, _baselineOffset;
        private bool _bold, _italic, _underline, _strikethrough, _noWrap, _ellipsis;
        private TextAlignment? _alignment;
        private FlowDirection? _direction;
        public ITextStyleDescriptor FontFamily(string family) { _family = string.IsNullOrWhiteSpace(family) ? throw new ArgumentException("A font family is required.", nameof(family)) : family; return this; }
        public ITextStyleDescriptor FontSize(float size) { _size = size <= 0 ? throw new ArgumentOutOfRangeException(nameof(size)) : size; return this; }
        public ITextStyleDescriptor Bold() { _bold = true; return this; }
        public ITextStyleDescriptor Italic() { _italic = true; return this; }
        public ITextStyleDescriptor Color(string color) { _color = ValidateColor(color); return this; }
        public ITextStyleDescriptor LineHeight(float value) { _lineHeight = ValidatePositive(value, nameof(value)); return this; }
        public ITextStyleDescriptor LetterSpacing(float value) { _letterSpacing = ValidateFinite(value, nameof(value)); return this; }
        public ITextStyleDescriptor WordSpacing(float value) { _wordSpacing = ValidateFinite(value, nameof(value)); return this; }
        public ITextStyleDescriptor Underline() { _underline = true; return this; }
        public ITextStyleDescriptor Strikethrough() { _strikethrough = true; return this; }
        public ITextStyleDescriptor Superscript() { _baselineOffset = 0.35f; return this; }
        public ITextStyleDescriptor Subscript() { _baselineOffset = -0.20f; return this; }
        public ITextStyleDescriptor AlignLeft() { _alignment = TextAlignment.Left; return this; }
        public ITextStyleDescriptor AlignCenter() { _alignment = TextAlignment.Center; return this; }
        public ITextStyleDescriptor AlignRight() { _alignment = TextAlignment.Right; return this; }
        public ITextStyleDescriptor Justify() { _alignment = TextAlignment.Justify; return this; }
        public ITextStyleDescriptor Direction(FlowDirection direction) { _direction = direction; return this; }
        public ITextStyleDescriptor NoWrap() { _noWrap = true; return this; }
        public ITextStyleDescriptor Wrap() { _noWrap = false; return this; }
        public ITextStyleDescriptor Ellipsis() { _ellipsis = true; return this; }
        public void Apply(TextStyleDefaults defaults)
        {
            if (_family != null) defaults.FontFamily = _family;
            if (_size.HasValue) defaults.FontSize = _size;
            if (_bold) defaults.Bold = true;
            if (_italic) defaults.Italic = true;
        }
        public void Apply(TextElement element)
        {
            if (_family != null) element.FontFamily = _family;
            if (_size.HasValue) element.FontSize = _size.Value;
            if (_bold) element.Bold = true;
            if (_italic) element.Italic = true;
            if (_color != null) element.Color = _color;
            if (_lineHeight.HasValue) element.LineHeight = _lineHeight.Value;
            if (_letterSpacing.HasValue) element.LetterSpacing = _letterSpacing;
            if (_wordSpacing.HasValue) element.WordSpacing = _wordSpacing;
            if (_baselineOffset.HasValue) element.BaselineOffset = element.FontSize * _baselineOffset.Value;
            if (_underline) element.Underline = true;
            if (_strikethrough) element.Strikethrough = true;
            if (_alignment.HasValue) element.Alignment = _alignment.Value;
            if (_direction.HasValue) element.FlowDirection = _direction.Value;
            element.NoWrap = _noWrap;
            element.EllipsisWhenConstrained = _ellipsis;
        }
        public void Apply(RichRun run)
        {
            if (_family != null) run.FontFamily = _family;
            if (_size.HasValue) run.FontSize = _size;
            if (_bold) run.Bold = true;
            if (_italic) run.Italic = true;
            if (_color != null) run.Color = _color;
            if (_underline) run.Underline = true;
            if (_strikethrough) run.Strikethrough = true;
            if (_letterSpacing.HasValue) run.LetterSpacing = _letterSpacing;
            if (_wordSpacing.HasValue) run.WordSpacing = _wordSpacing;
        }
        public void Apply(RichTextElement element)
        {
            if (_family != null) element.FontFamily = _family;
            if (_size.HasValue) element.FontSize = _size.Value;
            if (_lineHeight.HasValue) element.LineHeight = _lineHeight.Value;
            if (_alignment.HasValue) element.Alignment = _alignment.Value;
            if (_direction.HasValue) element.FlowDirection = _direction.Value;
        }
        private static float ValidatePositive(float value, string parameterName) => value <= 0f || float.IsNaN(value) || float.IsInfinity(value) ? throw new ArgumentOutOfRangeException(parameterName) : value;
        private static float ValidateFinite(float value, string parameterName) => float.IsNaN(value) || float.IsInfinity(value) ? throw new ArgumentOutOfRangeException(parameterName) : value;
        private static string ValidateColor(string color)
        {
            if (string.IsNullOrWhiteSpace(color)) throw new ArgumentException("A color is required.", nameof(color));
            return color;
        }
    }

    private sealed class CanonicalRichTextDescriptor : IRichTextDescriptor
    {
        private readonly CanonicalTextStyle _defaultStyle = new();
        private readonly List<(string Text, CanonicalTextStyle Style)> _spans = new();
        public ITextStyleDescriptor DefaultStyle() => _defaultStyle;
        public ITextStyleDescriptor Span(string text)
        {
            var style = new CanonicalTextStyle();
            _spans.Add((text ?? string.Empty, style));
            return style;
        }
        public void Apply(RichTextElement element)
        {
            _defaultStyle.Apply(element);
            foreach (var (text, style) in _spans)
            {
                var run = new RichRun { Text = text, FontFamily = element.FontFamily, FontSize = element.FontSize };
                _defaultStyle.Apply(run);
                style.Apply(run);
                element.Runs.Add(run);
            }
        }
    }
}
