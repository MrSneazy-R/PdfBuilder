using PdfBuilder.Elements;
using PdfBuilder.Models;

namespace PdfBuilder.Document;

public partial class PdfDocument
{
    private sealed class CanonicalContainer : IContainer
    {
        private readonly DocumentTheme _theme;
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
        private string? _debugLabel;
        private readonly List<Type> _componentPath;
        private readonly PaginationRegistry _pagination;

        public CanonicalContainer(DocumentTheme theme, List<Type>? componentPath = null, PaginationRegistry? pagination = null)
        {
            _theme = theme ?? throw new ArgumentNullException(nameof(theme));
            _componentPath = componentPath ?? new List<Type>();
            _pagination = pagination ?? new PaginationRegistry();
        }

        public IContainer Padding(float value) => Padding(value, value, value, value);
        public IContainer Padding(string spacingToken) => Padding(ResolveSpacing(spacingToken));
        public IContainer Padding(float left, float top, float right, float bottom)
        {
            ValidateNonNegative(left, nameof(left)); ValidateNonNegative(top, nameof(top)); ValidateNonNegative(right, nameof(right)); ValidateNonNegative(bottom, nameof(bottom));
            _paddingLeft = left; _paddingTop = top; _paddingRight = right; _paddingBottom = bottom; return this;
        }
        public IContainer Margin(float value) => Margin(value, value, value, value);
        public IContainer Margin(string spacingToken) => Margin(ResolveSpacing(spacingToken));
        public IContainer Margin(float left, float top, float right, float bottom)
        {
            ValidateNonNegative(left, nameof(left)); ValidateNonNegative(top, nameof(top)); ValidateNonNegative(right, nameof(right)); ValidateNonNegative(bottom, nameof(bottom));
            _marginLeft = left; _marginTop = top; _marginRight = right; _marginBottom = bottom; return this;
        }
        public IContainer Background(string color) { _background = ResolveColor(color); return this; }
        public IContainer Border(float width = 1f, string color = "#000000") { _border.SetAll(width, ResolveColor(color)); return this; }
        public IContainer BorderLeft(float width = 1f, string color = "#000000") { _border.Left = BorderValues.Create(width, ResolveColor(color)); return this; }
        public IContainer BorderTop(float width = 1f, string color = "#000000") { _border.Top = BorderValues.Create(width, ResolveColor(color)); return this; }
        public IContainer BorderRight(float width = 1f, string color = "#000000") { _border.Right = BorderValues.Create(width, ResolveColor(color)); return this; }
        public IContainer BorderBottom(float width = 1f, string color = "#000000") { _border.Bottom = BorderValues.Create(width, ResolveColor(color)); return this; }
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
        public IContainer DebugLabel(string label)
        {
            if (string.IsNullOrWhiteSpace(label)) throw new ArgumentException("A debug label is required.", nameof(label));
            _debugLabel = label;
            return this;
        }
        public IContainer Component(IPdfComponent component)
        {
            if (component == null) throw new ArgumentNullException(nameof(component));
            ComposeReusable(component.GetType(), () => component.Compose(this));
            return this;
        }
        public IContainer Component<TModel>(IPdfComponent<TModel> component, TModel model)
        {
            if (component == null) throw new ArgumentNullException(nameof(component));
            ComposeReusable(component.GetType(), () => component.Compose(this, model));
            return this;
        }
        public IContainer PageBreak() { _content.Add(composer => composer.PageBreak()); return this; }
        public ITextDescriptor Text(string text)
        {
            var descriptor = new CanonicalTextStyle();
            _content.Add(composer => composer.Text(text ?? string.Empty, descriptor.Apply));
            return descriptor;
        }
        public ITextDescriptor PageText(string template)
        {
            if (string.IsNullOrEmpty(template)) throw new ArgumentException("A page-text template is required.", nameof(template));
            if (!PageTextFormatter.ContainsToken(template))
                throw new ArgumentException("Page text must contain PageTextTokens.CurrentPage or PageTextTokens.TotalPages.", nameof(template));

            var descriptor = new CanonicalTextStyle();
            string measurementText = PageTextFormatter.CreateConservativeMeasurementText(template);
            _content.Add(composer => composer.Text(measurementText, element =>
            {
                descriptor.Apply(element);
                element.PageTextTemplate = template;
                element.Wrapping = TextWrapping.NoWrap;
                element.MaximumLines = 1;
            }));
            return descriptor;
        }
        public IContainer Anchor(string id)
        {
            string anchorId = NavigationUriPolicy.ValidateAnchorId(id, nameof(id));
            _pagination.RegisterAnchor(anchorId);
            _content.Add(composer => composer.NavigationAnchor(anchorId, null, 1));
            return this;
        }
        public IContainer Bookmark(string id, string title, int level = 1)
        {
            string anchorId = NavigationUriPolicy.ValidateAnchorId(id, nameof(id));
            if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("A bookmark title is required.", nameof(title));
            if (level <= 0) throw new ArgumentOutOfRangeException(nameof(level));
            _pagination.RegisterAnchor(anchorId);
            _content.Add(composer => composer.NavigationAnchor(anchorId, title, level));
            return this;
        }
        public ITextDescriptor ExternalLink(string text, string uri)
            => LinkedText(text, NavigationUriPolicy.ValidateExternal(uri), null);
        public ITextDescriptor InternalLink(string text, string anchorId)
            => LinkedText(text, null, NavigationUriPolicy.ValidateAnchorId(anchorId, nameof(anchorId)));
        public void Section(string id, string title, Action<IContainer> content, Action<ISectionDescriptor>? configure = null)
        {
            string anchorId = NavigationUriPolicy.ValidateAnchorId(id, nameof(id));
            if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("A section title is required.", nameof(title));
            if (content == null) throw new ArgumentNullException(nameof(content));

            var options = new CanonicalSectionDescriptor();
            configure?.Invoke(options);
            SectionEntry section = _pagination.RegisterSection(
                title,
                options.SectionLevel,
                anchorId,
                options.IsInTableOfContents,
                options.IsNumbered);
            bool hasPriorContent = _content.Count > 0;
            var child = new CanonicalContainer(_theme, _componentPath, _pagination);
            content(child);

            _content.Add(composer =>
            {
                if (options.StartsOnNewPage && hasPriorContent)
                    composer.PageBreak();
                composer.NavigationAnchor(
                    section.AnchorId,
                    options.IsInOutline ? section.TitleWithNumber : null,
                    section.Level);
                child.Compose(composer);
            });
        }
        public void TableOfContents(Action<ITableOfContentsDescriptor>? configure = null)
        {
            var descriptor = new CanonicalTableOfContentsDescriptor();
            configure?.Invoke(descriptor);
            _content.Add(composer => descriptor.Compose(composer, _pagination));
        }
        public ITextDescriptor PageReference(string anchorId, string format = "{0}", string pendingText = "…")
        {
            string target = NavigationUriPolicy.ValidateAnchorId(anchorId, nameof(anchorId));
            if (string.IsNullOrEmpty(pendingText)) throw new ArgumentException("Pending page text is required.", nameof(pendingText));
            _ = PageReferenceFormatter.CreateConservativeMeasurementText(format);
            var descriptor = new CanonicalTextStyle();
            _content.Add(composer => composer.PageReference(target, format, pendingText, descriptor.Apply));
            return descriptor;
        }
        public void RichText(Action<IRichTextDescriptor> configure)
        {
            if (configure == null) throw new ArgumentNullException(nameof(configure));
            var descriptor = new CanonicalRichTextDescriptor(_theme);
            configure(descriptor);
            _content.Add(composer => descriptor.Compose(composer));
        }
        public IImageDescriptor Image(byte[] data, float width, float height)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (width <= 0f || height <= 0f) throw new ArgumentOutOfRangeException(nameof(width), "Image dimensions must be positive.");
            var descriptor = new CanonicalImageDescriptor();
            _content.Add(composer => composer.Image(data, width, height, descriptor.Apply));
            return descriptor;
        }
        public void Svg(string markup, float width, float height)
        {
            if (string.IsNullOrWhiteSpace(markup)) throw new ArgumentException("SVG markup is required.", nameof(markup));
            if (width <= 0f || height <= 0f) throw new ArgumentOutOfRangeException(nameof(width), "SVG dimensions must be positive.");
            _content.Add(composer => composer.Svg(width, height, element => element.SvgContent = markup));
        }
        public void Barcode(string value, BarcodeKind kind = BarcodeKind.QrCode, float moduleSize = 2f, int quietZone = 4)
        {
            if (kind is not BarcodeKind.QrCode and not BarcodeKind.Code128)
                throw new NotSupportedException("The canonical barcode API supports QR Code and Code 128.");
            _content.Add(composer => composer.Barcode(value, kind, moduleSize, quietZone));
        }
        public void Chart(Action<IChartDescriptor> configure)
        {
            if (configure == null) throw new ArgumentNullException(nameof(configure));
            var descriptor = new CanonicalChartDescriptor(_theme);
            configure(descriptor);
            _content.Add(composer => composer.Component(new Layout.Components.ChartComponent(descriptor.Chart)));
        }
        public void Table(Action<ITableDescriptor> configure)
        {
            if (configure == null) throw new ArgumentNullException(nameof(configure));
            var descriptor = new CanonicalTableDescriptor(_theme);
            configure(descriptor);
            _content.Add(composer => composer.Table(descriptor.Build()));
        }
        [Obsolete("Use PageText with PageTextTokens for final-pagination values.")]
        public ITextDescriptor Text(Func<string> text)
        {
            if (text == null) throw new ArgumentNullException(nameof(text));
            var descriptor = new CanonicalTextStyle();
            _content.Add(composer => composer.Text(text() ?? string.Empty, descriptor.Apply));
            return descriptor;
        }

        private ITextDescriptor LinkedText(string text, string? externalUri, string? internalAnchor)
        {
            if (string.IsNullOrEmpty(text)) throw new ArgumentException("Link text is required.", nameof(text));
            var descriptor = new CanonicalTextStyle();
            _content.Add(composer => composer.RichText(element =>
            {
                element.AvoidBreakInside = false;
                var run = new RichRun
                {
                    Text = text,
                    FontFamily = element.FontFamily,
                    FontSize = element.FontSize,
                    Color = element.Color,
                    LinkUrl = externalUri,
                    LinkAnchor = internalAnchor
                };
                descriptor.Apply(run, _theme);
                element.Runs.Add(run);
            }));
            return descriptor;
        }
        public void Column(Action<IColumnDescriptor> configure)
        {
            if (configure == null) throw new ArgumentNullException(nameof(configure));
            var column = new CanonicalColumnDescriptor(_theme, _componentPath, _pagination); configure(column);
            _content.Add(composer => composer.Column(builder => column.Compose(builder)));
        }
        public void Row(Action<IRowDescriptor> configure)
        {
            if (configure == null) throw new ArgumentNullException(nameof(configure));
            var row = new CanonicalRowDescriptor(_theme, _componentPath, _pagination); configure(row);
            _content.Add(composer => composer.Row(builder => row.Compose(builder)));
        }
        public void Grid(Action<IGridDescriptor> configure)
        {
            if (configure == null) throw new ArgumentNullException(nameof(configure));
            var grid = new CanonicalGridDescriptor(_theme, _componentPath, _pagination); configure(grid);
            _content.Add(composer => composer.Grid(builder => grid.Compose(builder)));
        }
        public void Stack(Action<IStackDescriptor> configure)
        {
            if (configure == null) throw new ArgumentNullException(nameof(configure));
            var stack = new CanonicalStackDescriptor(_theme, _componentPath, _pagination); configure(stack);
            _content.Add(composer => composer.Stack(builder => stack.Compose(builder)));
        }
        public void Layer(Action<ILayerDescriptor> configure)
        {
            if (configure == null) throw new ArgumentNullException(nameof(configure));
            var layer = new CanonicalLayerDescriptor(_theme, _componentPath, _pagination); configure(layer);
            _content.Add(composer => composer.Layer(builder => layer.Compose(builder)));
        }
        public void Repeat(int count, Action<int, IContainer> configure)
        {
            if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
            if (configure == null) throw new ArgumentNullException(nameof(configure));
            for (var index = 0; index < count; index++) { var item = new CanonicalContainer(_theme, _componentPath, _pagination); configure(index, item); _content.Add(item.Compose); }
        }
        public void Compose(Layout.ContentComposer composer) => Compose(composer, null);

        internal void Compose(Layout.ContentComposer composer, string? automaticLabel)
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
            var label = _debugLabel ?? automaticLabel;
            if (label != null) { var next = content; content = inner => inner.DebugLabel(label, next); }
            content(composer);
        }
        private void ComposeCore(Layout.ContentComposer composer)
        {
            if (_content.Count == 0) composer.Component(new Layout.Components.EmptyComponent());
            foreach (var action in _content) action(composer);
        }
        private void ComposeReusable(Type componentType, Action compose)
        {
            int cycleStart = _componentPath.IndexOf(componentType);
            string path = string.Join(" -> ", _componentPath.Select(type => type.Name).Append(componentType.Name));
            if (cycleStart >= 0)
                throw new PdfComponentCompositionException($"Circular PDF component composition detected at '{path}'. Remove the recursive component reference.", path);
            if (_componentPath.Count >= 64)
                throw new PdfComponentCompositionException($"PDF component nesting exceeded the safety limit of 64 at '{path}'. Flatten the component hierarchy.", path);

            _componentPath.Add(componentType);
            try
            {
                compose();
            }
            catch (PdfComponentCompositionException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw new PdfComponentCompositionException($"Component '{componentType.Name}' failed while composing at '{path}'.", path, exception);
            }
            finally
            {
                _componentPath.RemoveAt(_componentPath.Count - 1);
            }
        }
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
        private string ResolveColor(string color) => _theme.ResolveColor(ValidateColor(color));
        private float ResolveSpacing(string spacingToken)
        {
            if (string.IsNullOrWhiteSpace(spacingToken))
                throw new ArgumentException("A theme spacing token is required.", nameof(spacingToken));
            return _theme.Spacing[spacingToken];
        }
    }

    private sealed class CanonicalColumnDescriptor : IColumnDescriptor
    {
        private readonly DocumentTheme _theme;
        private readonly List<Type> _componentPath;
        private readonly PaginationRegistry _pagination;
        private readonly List<CanonicalContainer> _items = new();
        private float _spacing = 8f;
        public CanonicalColumnDescriptor(DocumentTheme theme, List<Type> componentPath, PaginationRegistry pagination) { _theme = theme; _componentPath = componentPath; _pagination = pagination; }
        public void Spacing(float value) { if (value < 0) throw new ArgumentOutOfRangeException(nameof(value)); _spacing = value; }
        public void Spacing(string spacingToken) => Spacing(_theme.Spacing[spacingToken]);
        public IContainer Item() { var item = new CanonicalContainer(_theme, _componentPath, _pagination); _items.Add(item); return item; }
        public void Compose(Layout.LayoutComponentCollection.ColumnComponentBuilder builder)
        {
            builder.Spacing(_spacing);
            for (var index = 0; index < _items.Count; index++)
            {
                var item = _items[index];
                var itemNumber = index + 1;
                builder.Item(composer => item.Compose(composer, $"Column > Item[{itemNumber}]"));
            }
        }
    }

    private sealed class CanonicalRowDescriptor : IRowDescriptor
    {
        private readonly DocumentTheme _theme;
        private readonly List<Type> _componentPath;
        private readonly PaginationRegistry _pagination;
        private readonly List<(RowItemKind kind, float value, CanonicalContainer container)> _items = new();
        public CanonicalRowDescriptor(DocumentTheme theme, List<Type> componentPath, PaginationRegistry pagination) { _theme = theme; _componentPath = componentPath; _pagination = pagination; }
        public IContainer ConstantItem(float width) => Add(RowItemKind.Constant, width);
        public IContainer RelativeItem(float weight = 1f) => Add(RowItemKind.Relative, weight);
        public IContainer AutoItem() => Add(RowItemKind.Auto, 0f);
        private IContainer Add(RowItemKind kind, float value)
        {
            if (value < 0 || (kind == RowItemKind.Relative && value == 0)) throw new ArgumentOutOfRangeException(nameof(value));
            var container = new CanonicalContainer(_theme, _componentPath, _pagination); _items.Add((kind, value, container)); return container;
        }
        public void Compose(Layout.LayoutComponentCollection.RowComponentBuilder builder)
        {
            for (var index = 0; index < _items.Count; index++)
            {
                var item = _items[index];
                var itemNumber = index + 1;
                switch (item.kind)
                {
                    case RowItemKind.Constant: builder.Constant(item.value, composer => item.container.Compose(composer, $"Row > Item[{itemNumber}]")); break;
                    case RowItemKind.Relative: builder.Relative(item.value, composer => item.container.Compose(composer, $"Row > Item[{itemNumber}]")); break;
                    case RowItemKind.Auto: builder.Auto(composer => item.container.Compose(composer, $"Row > Item[{itemNumber}]")); break;
                }
            }
        }
        private enum RowItemKind { Constant, Relative, Auto }
    }

    private sealed class CanonicalGridDescriptor : IGridDescriptor
    {
        private readonly DocumentTheme _theme;
        private readonly List<Type> _componentPath;
        private readonly PaginationRegistry _pagination;
        private readonly List<CanonicalContainer> _items = new();
        private int _columns = 1;
        private float _rowGap = 8f, _columnGap = 8f;
        public CanonicalGridDescriptor(DocumentTheme theme, List<Type> componentPath, PaginationRegistry pagination) { _theme = theme; _componentPath = componentPath; _pagination = pagination; }
        public void Columns(int value) { if (value <= 0) throw new ArgumentOutOfRangeException(nameof(value)); _columns = value; }
        public void RowSpacing(float value) { if (value < 0) throw new ArgumentOutOfRangeException(nameof(value)); _rowGap = value; }
        public void RowSpacing(string spacingToken) => RowSpacing(_theme.Spacing[spacingToken]);
        public void ColumnSpacing(float value) { if (value < 0) throw new ArgumentOutOfRangeException(nameof(value)); _columnGap = value; }
        public void ColumnSpacing(string spacingToken) => ColumnSpacing(_theme.Spacing[spacingToken]);
        public IContainer Item() { var item = new CanonicalContainer(_theme, _componentPath, _pagination); _items.Add(item); return item; }
        public void Compose(Layout.LayoutComponentCollection.GridComponentBuilder builder)
        {
            builder.Columns(_columns).RowGap(_rowGap).ColumnGap(_columnGap);
            foreach (var item in _items) builder.Item(item.Compose);
        }
    }

    private sealed class CanonicalStackDescriptor : IStackDescriptor
    {
        private readonly DocumentTheme _theme;
        private readonly List<Type> _componentPath;
        private readonly PaginationRegistry _pagination;
        private readonly List<CanonicalContainer> _items = new();
        public CanonicalStackDescriptor(DocumentTheme theme, List<Type> componentPath, PaginationRegistry pagination) { _theme = theme; _componentPath = componentPath; _pagination = pagination; }
        public IContainer Item() { var item = new CanonicalContainer(_theme, _componentPath, _pagination); _items.Add(item); return item; }
        public void Compose(Layout.LayoutComponentCollection.StackComponentBuilder builder) { foreach (var item in _items) builder.Item(item.Compose); }
    }

    private sealed class CanonicalLayerDescriptor : ILayerDescriptor
    {
        private readonly CanonicalContainer _background;
        private readonly CanonicalContainer _content;
        private readonly CanonicalContainer _foreground;
        private bool _hasBackground, _hasContent, _hasForeground;
        public CanonicalLayerDescriptor(DocumentTheme theme, List<Type> componentPath, PaginationRegistry pagination)
        {
            _background = new CanonicalContainer(theme, componentPath, pagination);
            _content = new CanonicalContainer(theme, componentPath, pagination);
            _foreground = new CanonicalContainer(theme, componentPath, pagination);
        }
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
}
