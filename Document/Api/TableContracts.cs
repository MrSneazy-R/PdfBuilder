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
    /// <summary>Sets uniform cell padding in points.</summary>
    void CellPadding(float value);
    /// <summary>Sets the table border.</summary>
    void Border(float width = 1f, string color = "#000000");
    /// <summary>Sets the header background colour.</summary>
    void HeaderBackground(string color);
}

/// <summary>Configures table columns.</summary>
public interface ITableColumnsDescriptor
{
    /// <summary>Adds a proportional column.</summary>
    void RelativeColumn(float weight = 1f);
    /// <summary>Adds a fixed-width column in points.</summary>
    void ConstantColumn(float width);
}

/// <summary>Configures a table row.</summary>
public interface ITableRowDescriptor
{
    /// <summary>Adds a cell.</summary>
    ITableCellDescriptor Cell();
}

/// <summary>Configures a table cell using the normal canonical container surface.</summary>
public interface ITableCellDescriptor : IContainer
{
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
}
