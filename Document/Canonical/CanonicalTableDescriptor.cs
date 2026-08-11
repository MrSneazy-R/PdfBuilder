using PdfBuilder.Elements;
using PdfBuilder.Models;
using TableModels = PdfBuilder.Elements.Table;

namespace PdfBuilder.Document;

public partial class PdfDocument
{
    private sealed class CanonicalTableDescriptor : ITableDescriptor
    {
        private readonly DocumentTheme _theme;
        private readonly List<Type> _componentPath;
        private readonly PaginationRegistry _pagination;
        private readonly CanonicalCompositionState? _compositionState;
        private readonly TableElement _table = new();

        public CanonicalTableDescriptor(
            DocumentTheme theme,
            List<Type> componentPath,
            PaginationRegistry pagination,
            CanonicalCompositionState? compositionState)
        {
            _theme = theme;
            _componentPath = componentPath;
            _pagination = pagination;
            _compositionState = compositionState;
        }

        public void Columns(Action<ITableColumnsDescriptor> configure)
        {
            if (configure == null) throw new ArgumentNullException(nameof(configure));
            var columns = new CanonicalTableColumnsDescriptor(_table);
            configure(columns);
        }

        public void Header(Action<ITableRowDescriptor> configure) => AddRow(configure, isHeader: true);

        public void Row(Action<ITableRowDescriptor> configure) => AddRow(configure, isHeader: false);

        public void Footer(Action<ITableRowDescriptor> configure) => AddRow(configure, isHeader: false, isFooter: true);

        public void RepeatHeaders(bool value = true) => _table.RepeatHeaders = value;

        public void RepeatFooters(TableFooterRepeatMode mode) => _table.FooterRepeatMode = mode;

        public void WidowOrphanRows(int minimumAtPageStart, int minimumAtPageEnd)
        {
            if (minimumAtPageStart < 0) throw new ArgumentOutOfRangeException(nameof(minimumAtPageStart));
            if (minimumAtPageEnd < 0) throw new ArgumentOutOfRangeException(nameof(minimumAtPageEnd));
            _table.MinRowsAtPageStart = minimumAtPageStart;
            _table.MinRowsAtPageEnd = minimumAtPageEnd;
        }

        public void AllowRowSplitting(bool value = true) => _table.AllowRowSplitting = value;

        public void CellPadding(float value)
        {
            if (value < 0f || float.IsNaN(value) || float.IsInfinity(value)) throw new ArgumentOutOfRangeException(nameof(value));
            _table.CellPadding = value;
        }

        public void Border(float width = 1f, string color = "#000000")
        {
            if (width < 0f || float.IsNaN(width) || float.IsInfinity(width)) throw new ArgumentOutOfRangeException(nameof(width));
            _table.BorderWidth = width;
            _table.BorderColor = System.Drawing.ColorTranslator.FromHtml(ResolveColor(color));
        }

        public void HeaderBackground(string color) => _table.HeaderBackground = System.Drawing.ColorTranslator.FromHtml(ResolveColor(color));

        public void RowBanding(Action<ITableBandingDescriptor> configure)
            => _table.RowBanding = ConfigureBanding(configure).ToRowBanding();

        public void ColumnBanding(Action<ITableBandingDescriptor> configure)
            => _table.ColumnBanding = ConfigureBanding(configure).ToColumnBanding();

        public void BorderCollapse(TableBorderCollapseMode mode)
        {
            bool collapse = mode == TableBorderCollapseMode.Collapse;
            _table.BorderCollapse = collapse ? TableModels.BorderCollapseMode.Collapse : TableModels.BorderCollapseMode.Separate;
            _table.ResolveBorderConflicts = collapse;
        }

        public void OuterBorder(Action<ITableBorderDescriptor> configure)
            => _table.OuterBorder = ConfigureBorder(configure);

        public void InnerBorder(Action<ITableBorderDescriptor> configure)
            => _table.InnerBorder = ConfigureBorder(configure);

        public void CornerRadius(float value)
        {
            if (value < 0f || !float.IsFinite(value)) throw new ArgumentOutOfRangeException(nameof(value));
            _table.OuterCornerRadiusTopLeft = value;
            _table.OuterCornerRadiusTopRight = value;
            _table.OuterCornerRadiusBottomRight = value;
            _table.OuterCornerRadiusBottomLeft = value;
        }

        public TableElement Build()
        {
            if (_table.ColumnDefinitions.Count == 0)
                throw new InvalidOperationException("A table requires at least one column.");
            var table = Layout.LayoutSplitUtils.CloneTable(_table);
            Layout.TableGridValidator.Validate(table);
            return table;
        }

        private void AddRow(Action<ITableRowDescriptor> configure, bool isHeader, bool isFooter = false)
        {
            if (configure == null) throw new ArgumentNullException(nameof(configure));
            var row = new TableRow { IsHeader = isHeader, IsFooter = isFooter };
            configure(new CanonicalTableRowDescriptor(row, _theme, _componentPath, _pagination, _compositionState));
            if (row.Cells.Count == 0)
                throw new InvalidOperationException("A table row requires at least one cell.");
            _table.Rows.Add(row);
        }

        private string ResolveColor(string color) => _theme.ResolveColor(ValidateColor(color));
        private TableModels.BorderStyle ConfigureBorder(Action<ITableBorderDescriptor> configure)
        {
            if (configure == null) throw new ArgumentNullException(nameof(configure));
            var descriptor = new CanonicalTableBorderDescriptor(_theme);
            configure(descriptor);
            return descriptor.Build();
        }
        private CanonicalTableBandingDescriptor ConfigureBanding(Action<ITableBandingDescriptor> configure)
        {
            if (configure == null) throw new ArgumentNullException(nameof(configure));
            var descriptor = new CanonicalTableBandingDescriptor(_theme);
            configure(descriptor);
            return descriptor;
        }
    }

    private sealed class CanonicalTableColumnsDescriptor : ITableColumnsDescriptor
    {
        private readonly TableElement _table;
        public CanonicalTableColumnsDescriptor(TableElement table) => _table = table;
        public void RelativeColumn(float weight = 1f)
        {
            if (weight <= 0f || float.IsNaN(weight) || float.IsInfinity(weight)) throw new ArgumentOutOfRangeException(nameof(weight));
            _table.ColumnDefinitions.Add(PdfBuilder.Elements.Table.TableColumn.Relative(weight));
        }
        public void RelativeColumn(float weight, float? minWidth, float? maxWidth)
        {
            ValidateColumn(widthOrWeight: weight, minWidth, maxWidth, nameof(weight));
            _table.ColumnDefinitions.Add(TableModels.TableColumn.Relative(weight, minWidth, maxWidth));
        }
        public void ConstantColumn(float width)
        {
            if (width <= 0f || float.IsNaN(width) || float.IsInfinity(width)) throw new ArgumentOutOfRangeException(nameof(width));
            _table.ColumnDefinitions.Add(PdfBuilder.Elements.Table.TableColumn.Fixed(width));
        }
        public void FixedColumn(float width, float? minWidth = null, float? maxWidth = null)
        {
            ValidateColumn(width, minWidth, maxWidth, nameof(width));
            _table.ColumnDefinitions.Add(TableModels.TableColumn.Fixed(width, minWidth, maxWidth));
        }
        public void AutoColumn(float? minWidth = null, float? maxWidth = null)
        {
            ValidateBounds(minWidth, maxWidth);
            _table.ColumnDefinitions.Add(TableModels.TableColumn.Auto(minWidth, maxWidth));
        }
        private static void ValidateColumn(float widthOrWeight, float? minWidth, float? maxWidth, string name)
        {
            if (widthOrWeight <= 0f || !float.IsFinite(widthOrWeight)) throw new ArgumentOutOfRangeException(name);
            ValidateBounds(minWidth, maxWidth);
        }
        private static void ValidateBounds(float? minWidth, float? maxWidth)
        {
            if (minWidth is < 0f || (minWidth.HasValue && !float.IsFinite(minWidth.Value))) throw new ArgumentOutOfRangeException(nameof(minWidth));
            if (maxWidth is < 0f || (maxWidth.HasValue && !float.IsFinite(maxWidth.Value))) throw new ArgumentOutOfRangeException(nameof(maxWidth));
            if (minWidth.HasValue && maxWidth.HasValue && minWidth > maxWidth) throw new ArgumentException("A column minimum width cannot exceed its maximum width.");
        }
    }

    private sealed class CanonicalTableRowDescriptor : ITableRowDescriptor
    {
        private readonly TableRow _row;
        private readonly DocumentTheme _theme;
        private readonly List<Type> _componentPath;
        private readonly PaginationRegistry _pagination;
        private readonly CanonicalCompositionState? _compositionState;
        public CanonicalTableRowDescriptor(TableRow row, DocumentTheme theme, List<Type> componentPath, PaginationRegistry pagination, CanonicalCompositionState? compositionState)
        {
            _row = row;
            _theme = theme;
            _componentPath = componentPath;
            _pagination = pagination;
            _compositionState = compositionState;
        }
        public ITableRowDescriptor Position(int rowIndex)
        {
            if (rowIndex < 0) throw new ArgumentOutOfRangeException(nameof(rowIndex));
            _row.ExplicitRowIndex = rowIndex;
            return this;
        }
        public ITableRowDescriptor KeepWithNext() { _row.KeepWithNext = true; return this; }
        public ITableRowDescriptor Background(string color)
        {
            _row.BackgroundColor = System.Drawing.ColorTranslator.FromHtml(_theme.ResolveColor(ValidateColor(color)));
            return this;
        }
        public ITableRowDescriptor Height(float value)
        {
            if (value <= 0f || !float.IsFinite(value)) throw new ArgumentOutOfRangeException(nameof(value));
            _row.RowHeight = value;
            return this;
        }
        public ITableRowDescriptor AllowSplit(bool value = true) { _row.AllowSplit = value; return this; }
        public ITableCellDescriptor Cell()
        {
            var cell = new TableCell();
            _row.Cells.Add(cell);
            return new CanonicalTableCellDescriptor(cell, _theme, _componentPath, _pagination, _compositionState);
        }
    }

    private sealed class CanonicalTableCellDescriptor : CanonicalContainer, ITableCellDescriptor
    {
        private readonly TableCell _cell;
        private readonly DocumentTheme _theme;
        private TextWrapping _wrapping = TextWrapping.Wrap;
        private bool _ellipsis;
        public CanonicalTableCellDescriptor(TableCell cell, DocumentTheme theme, List<Type> componentPath, PaginationRegistry pagination, CanonicalCompositionState? compositionState)
            : base(theme, componentPath, pagination, compositionState)
        {
            _cell = cell;
            _theme = theme;
            _cell.ContentBuilder = owner => BuildComponent(owner, "Table cell");
        }
        public ITableCellDescriptor Position(int columnIndex)
        {
            if (columnIndex < 0) throw new ArgumentOutOfRangeException(nameof(columnIndex));
            _cell.ExplicitColumnIndex = columnIndex;
            return this;
        }
        public ITableCellDescriptor ColumnSpan(int value)
        {
            if (value <= 0) throw new ArgumentOutOfRangeException(nameof(value));
            _cell.ColSpan = value;
            return this;
        }
        public ITableCellDescriptor RowSpan(int value)
        {
            if (value <= 0) throw new ArgumentOutOfRangeException(nameof(value));
            _cell.RowSpan = value;
            return this;
        }
        public new ITableCellDescriptor AlignLeft() { _cell.HorizontalAlign = HorizontalAlign.Left; return this; }
        public new ITableCellDescriptor AlignCenter() { _cell.HorizontalAlign = HorizontalAlign.Center; return this; }
        public new ITableCellDescriptor AlignRight() { _cell.HorizontalAlign = HorizontalAlign.Right; return this; }
        public new ITableCellDescriptor AlignTop() { _cell.VerticalAlign = VerticalAlign.Top; return this; }
        public new ITableCellDescriptor AlignMiddle() { _cell.VerticalAlign = VerticalAlign.Middle; return this; }
        public new ITableCellDescriptor AlignBottom() { _cell.VerticalAlign = VerticalAlign.Bottom; return this; }
        public new ITableCellDescriptor Background(string color) { _cell.BackgroundColor = System.Drawing.ColorTranslator.FromHtml(ResolveColor(color)); return this; }
        public new ITableCellDescriptor Border(float width = 1f, string color = "#000000")
        {
            if (width < 0f || float.IsNaN(width) || float.IsInfinity(width)) throw new ArgumentOutOfRangeException(nameof(width));
            _cell.BorderWidth = width;
            _cell.BorderColor = System.Drawing.ColorTranslator.FromHtml(ResolveColor(color));
            return this;
        }
        public new ITableCellDescriptor BorderLeft(float width = 1f, string color = "#000000") => SetSideBorder(TableBorderSide.Left, width, color);
        public new ITableCellDescriptor BorderTop(float width = 1f, string color = "#000000") => SetSideBorder(TableBorderSide.Top, width, color);
        public new ITableCellDescriptor BorderRight(float width = 1f, string color = "#000000") => SetSideBorder(TableBorderSide.Right, width, color);
        public new ITableCellDescriptor BorderBottom(float width = 1f, string color = "#000000") => SetSideBorder(TableBorderSide.Bottom, width, color);
        public ITableCellDescriptor BorderLeft(Action<ITableBorderDescriptor> configure) => SetSideBorder(TableBorderSide.Left, configure);
        public ITableCellDescriptor BorderTop(Action<ITableBorderDescriptor> configure) => SetSideBorder(TableBorderSide.Top, configure);
        public ITableCellDescriptor BorderRight(Action<ITableBorderDescriptor> configure) => SetSideBorder(TableBorderSide.Right, configure);
        public ITableCellDescriptor BorderBottom(Action<ITableBorderDescriptor> configure) => SetSideBorder(TableBorderSide.Bottom, configure);
        public new ITableCellDescriptor CornerRadius(float value)
        {
            if (value < 0f || !float.IsFinite(value)) throw new ArgumentOutOfRangeException(nameof(value));
            _cell.CornerRadius = value;
            return this;
        }
        public new ITableCellDescriptor Padding(float value)
        {
            if (value < 0f || float.IsNaN(value) || float.IsInfinity(value)) throw new ArgumentOutOfRangeException(nameof(value));
            _cell.Padding = value;
            return this;
        }
        public new ITableCellDescriptor Padding(string spacingToken) => Padding(ResolveSpacing(spacingToken));
        public new ITableCellDescriptor Padding(float left, float top, float right, float bottom)
        {
            ValidatePadding(left, nameof(left));
            ValidatePadding(top, nameof(top));
            ValidatePadding(right, nameof(right));
            ValidatePadding(bottom, nameof(bottom));
            _cell.Padding = null;
            _cell.PaddingLeft = left;
            _cell.PaddingTop = top;
            _cell.PaddingRight = right;
            _cell.PaddingBottom = bottom;
            return this;
        }
        public new ITextDescriptor Text(string text)
        {
            _cell.Text = text ?? string.Empty;
            return ApplyTextPolicy(base.Text(_cell.Text));
        }
        public ITextDescriptor Text(object? value, string? format)
        {
            _cell.Text = value is IFormattable formattable ? formattable.ToString(format, System.Globalization.CultureInfo.InvariantCulture) : value?.ToString() ?? string.Empty;
            return ApplyTextPolicy(base.Text(_cell.Text));
        }
        public ITableCellDescriptor Wrap() { _wrapping = TextWrapping.Wrap; return this; }
        public ITableCellDescriptor NoWrap() { _wrapping = TextWrapping.NoWrap; return this; }
        public ITableCellDescriptor Hyphenate() { _wrapping = TextWrapping.Hyphenate; return this; }
        public ITableCellDescriptor Ellipsis() { _ellipsis = true; return this; }

        private string ResolveColor(string color) => _theme.ResolveColor(ValidateColor(color));
        private float ResolveSpacing(string token)
        {
            if (string.IsNullOrWhiteSpace(token)) throw new ArgumentException("A theme spacing token is required.", nameof(token));
            return _theme.Spacing[token];
        }
        private ITableCellDescriptor SetSideBorder(TableBorderSide side, float width, string color)
        {
            if (width < 0f || !float.IsFinite(width)) throw new ArgumentOutOfRangeException(nameof(width));
            var resolved = System.Drawing.ColorTranslator.FromHtml(ResolveColor(color));
            switch (side)
            {
                case TableBorderSide.Left: _cell.BorderLeft = true; _cell.BorderWidthLeft = width; _cell.BorderColorLeft = resolved; break;
                case TableBorderSide.Top: _cell.BorderTop = true; _cell.BorderWidthTop = width; _cell.BorderColorTop = resolved; break;
                case TableBorderSide.Right: _cell.BorderRight = true; _cell.BorderWidthRight = width; _cell.BorderColorRight = resolved; break;
                case TableBorderSide.Bottom: _cell.BorderBottom = true; _cell.BorderWidthBottom = width; _cell.BorderColorBottom = resolved; break;
            }
            return this;
        }
        private ITableCellDescriptor SetSideBorder(TableBorderSide side, Action<ITableBorderDescriptor> configure)
        {
            if (configure == null) throw new ArgumentNullException(nameof(configure));
            var descriptor = new CanonicalTableBorderDescriptor(_theme);
            configure(descriptor);
            TableModels.BorderStyle style = descriptor.Build();
            switch (side)
            {
                case TableBorderSide.Left: _cell.BorderLeft = true; _cell.BorderStyleLeft = style; break;
                case TableBorderSide.Top: _cell.BorderTop = true; _cell.BorderStyleTop = style; break;
                case TableBorderSide.Right: _cell.BorderRight = true; _cell.BorderStyleRight = style; break;
                case TableBorderSide.Bottom: _cell.BorderBottom = true; _cell.BorderStyleBottom = style; break;
            }
            return this;
        }
        private ITextDescriptor ApplyTextPolicy(ITextDescriptor descriptor)
        {
            switch (_wrapping)
            {
                case TextWrapping.NoWrap: descriptor.NoWrap(); break;
                case TextWrapping.Hyphenate: descriptor.Hyphenate(); break;
                default: descriptor.Wrap(); break;
            }
            if (_ellipsis) descriptor.Ellipsis();
            return descriptor;
        }
        private static void ValidatePadding(float value, string name)
        {
            if (value < 0f || !float.IsFinite(value)) throw new ArgumentOutOfRangeException(name);
        }
        private enum TableBorderSide { Left, Top, Right, Bottom }
    }

    private sealed class CanonicalTableBorderDescriptor : ITableBorderDescriptor
    {
        private readonly DocumentTheme _theme;
        private readonly TableModels.BorderStyle _style = new();
        public CanonicalTableBorderDescriptor(DocumentTheme theme) => _theme = theme;
        public void Color(string color) => _style.Color = System.Drawing.ColorTranslator.FromHtml(_theme.ResolveColor(ValidateColor(color)));
        public void Width(float value) { if (value < 0f || !float.IsFinite(value)) throw new ArgumentOutOfRangeException(nameof(value)); _style.Width = value; }
        public void DashPattern(params float[] values)
        {
            if (values == null) throw new ArgumentNullException(nameof(values));
            if (values.Any(value => value <= 0f || !float.IsFinite(value))) throw new ArgumentOutOfRangeException(nameof(values));
            _style.DashPattern = values;
        }
        public void DashPhase(float value) { if (value < 0f || !float.IsFinite(value)) throw new ArgumentOutOfRangeException(nameof(value)); _style.DashPhase = value; }
        public void LineJoin(TableBorderLineJoin value) => _style.LineJoin = value switch
        {
            TableBorderLineJoin.Round => TableModels.BorderLineJoin.Round,
            TableBorderLineJoin.Bevel => TableModels.BorderLineJoin.Bevel,
            _ => TableModels.BorderLineJoin.Miter
        };
        public void LineCap(TableBorderLineCap value) => _style.LineCap = value switch
        {
            TableBorderLineCap.Round => TableModels.BorderLineCap.Round,
            TableBorderLineCap.Square => TableModels.BorderLineCap.Square,
            _ => TableModels.BorderLineCap.Butt
        };
        public void MiterLimit(float value) { if (value <= 0f || !float.IsFinite(value)) throw new ArgumentOutOfRangeException(nameof(value)); _style.MiterLimit = value; }
        public TableModels.BorderStyle Build() => _style.Clone();
    }

    private sealed class CanonicalTableBandingDescriptor : ITableBandingDescriptor
    {
        private readonly DocumentTheme _theme;
        private readonly List<TableModels.BandFill> _fills = new();
        private int _step = 1;
        private TableModels.BorderStyle? _border;
        public CanonicalTableBandingDescriptor(DocumentTheme theme) => _theme = theme;
        public void Step(int value) { if (value <= 0) throw new ArgumentOutOfRangeException(nameof(value)); _step = value; }
        public void Fill(string color, Action<ITableBorderDescriptor>? border = null)
        {
            var fill = new TableModels.BandFill { FillColor = System.Drawing.ColorTranslator.FromHtml(_theme.ResolveColor(ValidateColor(color))) };
            if (border != null)
            {
                var descriptor = new CanonicalTableBorderDescriptor(_theme);
                border(descriptor);
                fill.BorderOverride = descriptor.Build();
            }
            _fills.Add(fill);
        }
        public void Border(Action<ITableBorderDescriptor> configure)
        {
            if (configure == null) throw new ArgumentNullException(nameof(configure));
            var descriptor = new CanonicalTableBorderDescriptor(_theme);
            configure(descriptor);
            _border = descriptor.Build();
        }
        public TableModels.RowBandingSpec ToRowBanding() => new() { Step = _step, Fills = _fills.Select(fill => fill.Clone()).ToList(), BorderOverride = _border?.Clone() };
        public TableModels.ColumnBandingSpec ToColumnBanding() => new() { Step = _step, Fills = _fills.Select(fill => fill.Clone()).ToList(), BorderOverride = _border?.Clone() };
    }

    private static string ValidateColor(string color) => string.IsNullOrWhiteSpace(color) ? throw new ArgumentException("A color is required.", nameof(color)) : color;
}
