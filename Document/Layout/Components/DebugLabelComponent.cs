namespace PdfBuilder.Document.Layout.Components;

/// <summary>Associates a stable caller label with a component without altering its layout.</summary>
internal sealed class DebugLabelComponent : IMeasurable
{
    public DebugLabelComponent(string label, IMeasurable child)
    {
        Label = string.IsNullOrWhiteSpace(label) ? throw new ArgumentException("A debug label is required.", nameof(label)) : label;
        Child = child ?? throw new ArgumentNullException(nameof(child));
    }

    public string Label { get; }
    public IMeasurable Child { get; }

    public LayoutMeasurement Measure(LayoutMeasureContext context) => Child.Measure(context);

    public void Draw(LayoutDrawContext context, LayoutMeasurement measurement) => Child.Draw(context, measurement);
}
