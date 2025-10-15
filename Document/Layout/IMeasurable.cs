namespace PdfBuilder.Document.Layout
{
    public interface IMeasurable
    {
        LayoutMeasurement Measure(LayoutMeasureContext context);

        void Draw(LayoutDrawContext context, LayoutMeasurement measurement);
    }
}
