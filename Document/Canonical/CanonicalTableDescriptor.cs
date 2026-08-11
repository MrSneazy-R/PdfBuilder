using PdfBuilder.Elements;
using PdfBuilder.Models;

namespace PdfBuilder.Document;

public partial class PdfDocument
{
    private sealed class CanonicalTableDescriptor : ITableDescriptor
    {
        private readonly DocumentTheme _theme;
        private readonly TableElement _table = new();

        public CanonicalTableDescriptor(DocumentTheme theme) => _theme = theme;

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
            return _table;
        }

        private void AddRow(Action<ITableRowDescriptor> configure, bool isHeader)
        {
            if (configure == null) throw new ArgumentNullException(nameof(configure));
            var row = new TableRow { IsHeader = isHeader };
            configure(new CanonicalTableRowDescriptor(row, _theme));
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
        public CanonicalTableRowDescriptor(TableRow row, DocumentTheme theme) { _row = row; _theme = theme; }
        public ITableCellDescriptor Cell()
        {
            var cell = new TableCell();
            _row.Cells.Add(cell);
            return new CanonicalTableCellDescriptor(cell, _theme);
        }
    }

    private sealed class CanonicalTableCellDescriptor : ITableCellDescriptor
    {
        private readonly TableCell _cell;
        private readonly DocumentTheme _theme;
        public CanonicalTableCellDescriptor(TableCell cell, DocumentTheme theme) { _cell = cell; _theme = theme; }
        public ITableCellDescriptor AlignLeft() { _cell.HorizontalAlign = HorizontalAlign.Left; return this; }
        public ITableCellDescriptor AlignCenter() { _cell.HorizontalAlign = HorizontalAlign.Center; return this; }
        public ITableCellDescriptor AlignRight() { _cell.HorizontalAlign = HorizontalAlign.Right; return this; }
        public ITableCellDescriptor Background(string color) { _cell.BackgroundColor = System.Drawing.ColorTranslator.FromHtml(ResolveColor(color)); return this; }
        public ITableCellDescriptor Border(float width = 1f, string color = "#000000")
        {
            if (width < 0f || float.IsNaN(width) || float.IsInfinity(width)) throw new ArgumentOutOfRangeException(nameof(width));
            _cell.BorderWidth = width;
            _cell.BorderColor = System.Drawing.ColorTranslator.FromHtml(ResolveColor(color));
            return this;
        }
        public ITableCellDescriptor Padding(float value)
        {
            if (value < 0f || float.IsNaN(value) || float.IsInfinity(value)) throw new ArgumentOutOfRangeException(nameof(value));
            _cell.Padding = value;
            return this;
        }
        public ITextDescriptor Text(string text)
        {
            _cell.Text = text ?? string.Empty;
            return new CanonicalTableTextDescriptor(_cell);
        }
        public ITextDescriptor Text(object? value, string? format)
        {
            _cell.Text = value is IFormattable formattable ? formattable.ToString(format, System.Globalization.CultureInfo.InvariantCulture) : value?.ToString() ?? string.Empty;
            return new CanonicalTableTextDescriptor(_cell);
        }

        private string ResolveColor(string color) => _theme.ResolveColor(ValidateColor(color));
    }

    private sealed class CanonicalTableTextDescriptor : ITextDescriptor
    {
        private readonly TableCell _cell;
        private readonly TextStyleDefaults _style = TextStyleDefaults.CreateOverrides();
        public CanonicalTableTextDescriptor(TableCell cell)
        {
            _cell = cell;
            _cell.CanonicalStyleOverrides = _style;
        }
        public ITextDescriptor Style(string name) { _cell.ThemeStyleName = string.IsNullOrWhiteSpace(name) ? throw new ArgumentException("A style name is required.", nameof(name)) : name; return this; }
        public ITextStyleDescriptor FontFamily(string family) { _style.FontFamily = RequireText(family, nameof(family)); return this; }
        public ITextStyleDescriptor FontSize(float size) { _style.FontSize = Positive(size, nameof(size)); return this; }
        public ITextStyleDescriptor Bold() { _style.Bold = true; return this; }
        public ITextStyleDescriptor Italic() { _style.Italic = true; return this; }
        public ITextStyleDescriptor Color(string color) { _style.Color = RequireText(color, nameof(color)); return this; }
        public ITextStyleDescriptor Highlight(string color) { _style.BackgroundColor = RequireText(color, nameof(color)); return this; }
        public ITextStyleDescriptor LineHeight(float value) { _style.LineHeight = Positive(value, nameof(value)); return this; }
        public ITextStyleDescriptor LetterSpacing(float value) { _style.LetterSpacing = Finite(value, nameof(value)); return this; }
        public ITextStyleDescriptor WordSpacing(float value) { _style.WordSpacing = Finite(value, nameof(value)); return this; }
        public ITextStyleDescriptor Underline() { _style.Underline = true; return this; }
        public ITextStyleDescriptor Strikethrough() { _style.Strikethrough = true; return this; }
        public ITextStyleDescriptor Overline() { _style.Overline = true; return this; }
        public ITextStyleDescriptor Decoration(string? color = null, float? thickness = null, TextDecorationStyle style = TextDecorationStyle.Solid) { SetDecoration(_style, color, thickness, style); return this; }
        public ITextStyleDescriptor Superscript() { _style.Superscript = true; _style.Subscript = false; return this; }
        public ITextStyleDescriptor Subscript() { _style.Subscript = true; _style.Superscript = false; return this; }
        public ITextStyleDescriptor AlignLeft() { _style.Alignment = TextAlignment.Left; return this; }
        public ITextStyleDescriptor AlignCenter() { _style.Alignment = TextAlignment.Center; return this; }
        public ITextStyleDescriptor AlignRight() { _style.Alignment = TextAlignment.Right; return this; }
        public ITextStyleDescriptor Justify() { _style.Alignment = TextAlignment.Justify; return this; }
        public ITextStyleDescriptor Direction(TextDirection direction) { _style.Direction = direction; return this; }
        public ITextStyleDescriptor Wrap() { _style.Wrapping = TextWrapping.Wrap; return this; }
        public ITextStyleDescriptor NoWrap() { _style.Wrapping = TextWrapping.NoWrap; return this; }
        public ITextStyleDescriptor Hyphenate() { _style.Wrapping = TextWrapping.Hyphenate; return this; }
        public ITextStyleDescriptor Ellipsis() { _style.Ellipsis = true; return this; }
        public ITextStyleDescriptor MaximumLines(int value) { _style.MaximumLines = PositiveLines(value); return this; }
        public ITextStyleDescriptor FallbackFonts(params string[] families) { _style.FallbackFonts = ValidateFamilies(families); return this; }
    }

    private static string ValidateColor(string color) => string.IsNullOrWhiteSpace(color) ? throw new ArgumentException("A color is required.", nameof(color)) : color;
}
