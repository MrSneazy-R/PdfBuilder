namespace PdfBuilder.Writer;

/// <summary>
/// Internal generation diagnostics used by stress tests and engineering measurements.
/// A writer instance owns one metrics object; it is never shared between documents.
/// </summary>
internal sealed class PdfGenerationMetrics
{
    public int PagesPlanned { get; set; }
    public int PageContentStreamsWritten { get; set; }
    public int MaximumRetainedPageContentStreams { get; set; }
}
