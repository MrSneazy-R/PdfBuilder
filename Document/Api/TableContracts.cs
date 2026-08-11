namespace PdfBuilder.Document;

/// <summary>Configures a basic flowing table.</summary>
public interface ITableDescriptor
{
    /// <summary>Configures constant and relative columns.</summary>
    void Columns(Action<ITableColumnsDescriptor> configure);
    /// <summary>Adds the repeating header row.</summary>
    void Header(Action<ITableRowDescriptor> configure);
    /// <summary>Adds a body row.</summary>
    void Row(Action<ITableRowDescriptor> configure);
    /// <summary>Adds a table footer row. Footer rows are rendered after body rows.</summary>
    void Footer(Action<ITableRowDescriptor> configure);
    /// <summary>Controls whether header rows repeat after table continuation.</summary>
    void RepeatHeaders(bool value = true);
    /// <summary>Controls whether footer rows repeat on table segments before the logical end.</summary>
    void RepeatFooters(TableFooterRepeatMode mode);
    /// <summary>Sets the minimum body-row counts allowed at the beginning and end of a table segment.</summary>
    void WidowOrphanRows(int minimumAtPageStart, int minimumAtPageEnd);
    /// <summary>Enables controlled continuation for oversized body rows. Rows remain atomic by default.</summary>
    void AllowRowSplitting(bool value = true);
    /// <summary>Sets uniform cell padding in points.</summary>
    void CellPadding(float value);
    /// <summary>Sets the table border.</summary>
    void Border(float width = 1f, string color = "#000000");
    /// <summary>Sets the header background colour.</summary>
    void HeaderBackground(string color);
    /// <summary>Configures alternating row bands.</summary>
    void RowBanding(Action<ITableBandingDescriptor> configure);
    /// <summary>Configures alternating column bands.</summary>
    void ColumnBanding(Action<ITableBandingDescriptor> configure);
    /// <summary>Controls whether adjacent cell borders are drawn separately or collapsed.</summary>
    void BorderCollapse(TableBorderCollapseMode mode);
    /// <summary>Configures the outer table border independently from inner grid lines.</summary>
    void OuterBorder(Action<ITableBorderDescriptor> configure);
    /// <summary>Configures inner table grid lines independently from the outer border.</summary>
    void InnerBorder(Action<ITableBorderDescriptor> configure);
    /// <summary>Sets a uniform radius for the four outer table corners.</summary>
    void CornerRadius(float value);
}

/// <summary>Configures table columns.</summary>
public interface ITableColumnsDescriptor
{
    /// <summary>Adds a proportional column.</summary>
    void RelativeColumn(float weight = 1f);
    /// <summary>Adds a proportional column constrained by optional minimum and maximum widths.</summary>
    void RelativeColumn(float weight, float? minWidth, float? maxWidth);
    /// <summary>Adds a fixed-width column in points.</summary>
    void ConstantColumn(float width);
    /// <summary>Adds a fixed-width column constrained by optional minimum and maximum widths.</summary>
    void FixedColumn(float width, float? minWidth = null, float? maxWidth = null);
    /// <summary>Adds a content-aware column constrained by optional minimum and maximum widths.</summary>
    void AutoColumn(float? minWidth = null, float? maxWidth = null);
}

/// <summary>Configures a table row.</summary>
public interface ITableRowDescriptor
{
    /// <summary>Places the row at a zero-based position within its header, body, or footer group.</summary>
    ITableRowDescriptor Position(int rowIndex);
    /// <summary>Keeps this row with the following row when a valid page break is selected.</summary>
    ITableRowDescriptor KeepWithNext();
    /// <summary>Sets the row background colour.</summary>
    ITableRowDescriptor Background(string color);
    /// <summary>Sets an exact row height in points.</summary>
    ITableRowDescriptor Height(float value);
    /// <summary>Overrides the table-level row-splitting policy for this row.</summary>
    ITableRowDescriptor AllowSplit(bool value = true);
    /// <summary>Adds a cell.</summary>
    ITableCellDescriptor Cell();
}

/// <summary>Configures a table cell using the normal canonical container surface.</summary>
public interface ITableCellDescriptor : IContainer
{
    /// <summary>Places the cell at a zero-based logical column.</summary>
    ITableCellDescriptor Position(int columnIndex);
    /// <summary>Spans the cell across the specified number of logical columns.</summary>
    ITableCellDescriptor ColumnSpan(int value);
    /// <summary>Spans the cell across the specified number of logical rows.</summary>
    ITableCellDescriptor RowSpan(int value);
    /// <summary>Aligns cell content to the left.</summary>
    new ITableCellDescriptor AlignLeft();
    /// <summary>Centers cell content.</summary>
    new ITableCellDescriptor AlignCenter();
    /// <summary>Aligns cell content to the right.</summary>
    new ITableCellDescriptor AlignRight();
    /// <summary>Aligns cell content to the top.</summary>
    new ITableCellDescriptor AlignTop();
    /// <summary>Aligns cell content vertically in the middle.</summary>
    new ITableCellDescriptor AlignMiddle();
    /// <summary>Aligns cell content to the bottom.</summary>
    new ITableCellDescriptor AlignBottom();
    /// <summary>Sets the cell background colour.</summary>
    new ITableCellDescriptor Background(string color);
    /// <summary>Sets a border around the cell.</summary>
    new ITableCellDescriptor Border(float width = 1f, string color = "#000000");
    /// <summary>Sets the cell's left border.</summary>
    new ITableCellDescriptor BorderLeft(float width = 1f, string color = "#000000");
    /// <summary>Sets the cell's top border.</summary>
    new ITableCellDescriptor BorderTop(float width = 1f, string color = "#000000");
    /// <summary>Sets the cell's right border.</summary>
    new ITableCellDescriptor BorderRight(float width = 1f, string color = "#000000");
    /// <summary>Sets the cell's bottom border.</summary>
    new ITableCellDescriptor BorderBottom(float width = 1f, string color = "#000000");
    /// <summary>Configures the cell's left border.</summary>
    ITableCellDescriptor BorderLeft(Action<ITableBorderDescriptor> configure);
    /// <summary>Configures the cell's top border.</summary>
    ITableCellDescriptor BorderTop(Action<ITableBorderDescriptor> configure);
    /// <summary>Configures the cell's right border.</summary>
    ITableCellDescriptor BorderRight(Action<ITableBorderDescriptor> configure);
    /// <summary>Configures the cell's bottom border.</summary>
    ITableCellDescriptor BorderBottom(Action<ITableBorderDescriptor> configure);
    /// <summary>Rounds the cell background and border corners.</summary>
    new ITableCellDescriptor CornerRadius(float value);
    /// <summary>Sets uniform cell padding in points.</summary>
    new ITableCellDescriptor Padding(float value);
    /// <summary>Sets uniform cell padding from the current document theme.</summary>
    new ITableCellDescriptor Padding(string spacingToken);
    /// <summary>Sets per-side cell padding in points.</summary>
    new ITableCellDescriptor Padding(float left, float top, float right, float bottom);
    /// <summary>Adds text to the cell.</summary>
    new ITextDescriptor Text(string text);
    /// <summary>Adds a formatted value to the cell.</summary>
    ITextDescriptor Text(object? value, string? format);
    /// <summary>Enables ordinary wrapping for text added through the cell convenience API.</summary>
    ITableCellDescriptor Wrap();
    /// <summary>Disables wrapping for text added through the cell convenience API.</summary>
    ITableCellDescriptor NoWrap();
    /// <summary>Enables hyphenation for text added through the cell convenience API.</summary>
    ITableCellDescriptor Hyphenate();
    /// <summary>Ellipsizes constrained text added through the cell convenience API.</summary>
    ITableCellDescriptor Ellipsis();
}

/// <summary>Configures a table border without exposing drawing-system types.</summary>
public interface ITableBorderDescriptor
{
    void Color(string color);
    void Width(float value);
    void DashPattern(params float[] values);
    void DashPhase(float value);
    void LineJoin(TableBorderLineJoin value);
    void LineCap(TableBorderLineCap value);
    void MiterLimit(float value);
}

/// <summary>Configures repeating table band fills and their optional border overrides.</summary>
public interface ITableBandingDescriptor
{
    void Step(int value);
    void Fill(string color, Action<ITableBorderDescriptor>? border = null);
    void Border(Action<ITableBorderDescriptor> configure);
}

/// <summary>Defines table-footer repetition across continued table segments.</summary>
public enum TableFooterRepeatMode { Never, EveryPage, ContinuationPages }

/// <summary>Defines how adjacent cell borders are painted.</summary>
public enum TableBorderCollapseMode { Separate, Collapse }

public enum TableBorderLineJoin { Miter, Round, Bevel }
public enum TableBorderLineCap { Butt, Round, Square }
