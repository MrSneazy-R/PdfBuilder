using PdfBuilder.Elements;
using PdfBuilder.Models;

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

        public TableElement Build()
        {
            if (_table.ColumnDefinitions.Count == 0)
                throw new InvalidOperationException("A table requires at least one column.");
            return Layout.LayoutSplitUtils.CloneTable(_table);
        }

        private void AddRow(Action<ITableRowDescriptor> configure, bool isHeader)
        {
            if (configure == null) throw new ArgumentNullException(nameof(configure));
            var row = new TableRow { IsHeader = isHeader };
            configure(new CanonicalTableRowDescriptor(row, _theme, _componentPath, _pagination, _compositionState));
            if (row.Cells.Count == 0)
                throw new InvalidOperationException("A table row requires at least one cell.");
            _table.Rows.Add(row);
        }

        private string ResolveColor(string color) => _theme.ResolveColor(ValidateColor(color));
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
        public void ConstantColumn(float width)
        {
            if (width <= 0f || float.IsNaN(width) || float.IsInfinity(width)) throw new ArgumentOutOfRangeException(nameof(width));
            _table.ColumnDefinitions.Add(PdfBuilder.Elements.Table.TableColumn.Fixed(width));
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
        public CanonicalTableCellDescriptor(TableCell cell, DocumentTheme theme, List<Type> componentPath, PaginationRegistry pagination, CanonicalCompositionState? compositionState)
            : base(theme, componentPath, pagination, compositionState)
        {
            _cell = cell;
            _theme = theme;
            _cell.ContentBuilder = owner => BuildComponent(owner, "Table cell");
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
            return base.Text(_cell.Text);
        }
        public ITextDescriptor Text(object? value, string? format)
        {
            _cell.Text = value is IFormattable formattable ? formattable.ToString(format, System.Globalization.CultureInfo.InvariantCulture) : value?.ToString() ?? string.Empty;
            return base.Text(_cell.Text);
        }

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
        private static void ValidatePadding(float value, string name)
        {
            if (value < 0f || !float.IsFinite(value)) throw new ArgumentOutOfRangeException(name);
        }
        private enum TableBorderSide { Left, Top, Right, Bottom }
    }

    private static string ValidateColor(string color) => string.IsNullOrWhiteSpace(color) ? throw new ArgumentException("A color is required.", nameof(color)) : color;
}
