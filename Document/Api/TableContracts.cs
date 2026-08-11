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

/// <summary>Configures basic table-cell content and decoration.</summary>
public interface ITableCellDescriptor
{
    /// <summary>Aligns cell content to the left.</summary>
    ITableCellDescriptor AlignLeft();
    /// <summary>Centers cell content.</summary>
    ITableCellDescriptor AlignCenter();
    /// <summary>Aligns cell content to the right.</summary>
    ITableCellDescriptor AlignRight();
    /// <summary>Sets the cell background colour.</summary>
    ITableCellDescriptor Background(string color);
    /// <summary>Sets a border around the cell.</summary>
    ITableCellDescriptor Border(float width = 1f, string color = "#000000");
    /// <summary>Sets uniform cell padding in points.</summary>
    ITableCellDescriptor Padding(float value);
    /// <summary>Adds text to the cell.</summary>
    ITextDescriptor Text(string text);
    /// <summary>Adds a formatted value to the cell.</summary>
    ITextDescriptor Text(object? value, string? format);
}
