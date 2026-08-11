using PdfBuilder.Elements;

namespace PdfBuilder.Document;

/// <summary>Represents a container that can receive canonical content.</summary>
public interface IContainer
{
    /// <summary>Composes reusable model-independent content.</summary>
    IContainer Component(IPdfComponent component);
    /// <summary>Composes reusable content using a strongly typed model.</summary>
    IContainer Component<TModel>(IPdfComponent<TModel> component, TModel model);
    /// <summary>Applies uniform inner padding in points.</summary>
    IContainer Padding(float value);
    /// <summary>Applies uniform inner padding resolved from the current document theme.</summary>
    IContainer Padding(string spacingToken);
    /// <summary>Applies per-side inner padding in points.</summary>
    IContainer Padding(float left, float top, float right, float bottom);
    /// <summary>Applies uniform outer margin in points.</summary>
    IContainer Margin(float value);
    /// <summary>Applies uniform outer margin resolved from the current document theme.</summary>
    IContainer Margin(string spacingToken);
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
    /// <summary>Associates a source label with this container for layout diagnostics.</summary>
    IContainer DebugLabel(string label);
    /// <summary>Forces subsequent content onto a new page.</summary>
    IContainer PageBreak();
    /// <summary>Adds text and returns its style descriptor.</summary>
    ITextDescriptor Text(string text);
    /// <summary>Adds text containing final-pagination tokens such as current page and total pages.</summary>
    ITextDescriptor PageText(string template);
    /// <summary>Adds a rich-text paragraph with independently styled spans.</summary>
    void RichText(Action<IRichTextDescriptor> configure);
    /// <summary>Adds a raster image without exposing PDF coordinates or image elements.</summary>
    IImageDescriptor Image(byte[] data, float width, float height);
    /// <summary>Adds sanitised inline SVG markup without exposing image elements.</summary>
    void Svg(string markup, float width, float height);
    /// <summary>Adds a vector QR Code or Code 128 barcode.</summary>
    void Barcode(string value, BarcodeKind kind = BarcodeKind.QrCode, float moduleSize = 2f, int quietZone = 4);
    /// <summary>Adds a vector chart using PdfColor rather than System.Drawing types.</summary>
    void Chart(Action<IChartDescriptor> configure);
    /// <summary>Adds a flowing table that participates in normal layout and pagination.</summary>
    void Table(Action<ITableDescriptor> configure);
    /// <summary>Adds text resolved when the container is rendered.</summary>
    [Obsolete("Use PageText with PageTextTokens for final-pagination values.")]
    ITextDescriptor Text(Func<string> text);
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
    /// <summary>Sets spacing between column items from the current document theme.</summary>
    void Spacing(string spacingToken);
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
    /// <summary>Sets the gap between grid rows from the current document theme.</summary>
    void RowSpacing(string spacingToken);
    /// <summary>Sets the gap between grid columns in points.</summary>
    void ColumnSpacing(float value);
    /// <summary>Sets the gap between grid columns from the current document theme.</summary>
    void ColumnSpacing(string spacingToken);
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
