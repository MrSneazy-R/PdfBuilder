namespace PdfBuilder.Models;

/// <summary>Read-only diagnostics captured for the most recent PDF generation.</summary>
public sealed class PdfGenerationMetrics
{
    internal PdfGenerationMetrics() { }
    public int PagesPlanned { get; internal set; }
    public int PageContentStreamsWritten { get; internal set; }
    public int MaximumRetainedPageContentStreams { get; internal set; }
    public int ObjectsWritten { get; internal set; }
    public int BaseFontResources { get; internal set; }
    public int EmbeddedFontResources { get; internal set; }
    public int ImageReferences { get; internal set; }
    public int UniqueImageResources { get; internal set; }
    public int ExtGStateResources { get; internal set; }
    public long TableMeasurementCount { get; internal set; }
    public long TableRowMeasurementCount { get; internal set; }
    public long TableCellMeasurementCount { get; internal set; }
    public long TableCloneCount { get; internal set; }
    public long TableRowCloneCount { get; internal set; }
    public long ContentFactoryInvocationCount { get; internal set; }
    public long TableCellDrawBufferAllocationCount { get; internal set; }
    public long OutputBytes { get; internal set; }
    public TimeSpan Elapsed { get; internal set; }
}
