using PdfBuilder.Elements;
using PdfBuilder.Models;

namespace PdfBuilder.Document.Layout;

internal sealed class CellDrawBuffer
{
    private readonly PdfPage _scratchPage;
    private readonly LayoutOptions _options;

    public CellDrawBuffer(PdfPage sourcePage, LayoutOptions options)
    {
        ArgumentNullException.ThrowIfNull(sourcePage);
        ArgumentNullException.ThrowIfNull(options);

        _options = options;
        _scratchPage = new PdfPage(sourcePage.Width, sourcePage.Height)
        {
            Owner = sourcePage.Owner,
            Pagination = sourcePage.Pagination,
            ProfilerSession = sourcePage.ProfilerSession,
            CompositionPageNumber = sourcePage.CompositionPageNumber,
            LayoutOptions = options,
            TextDefaults = sourcePage.TextDefaults.Clone(),
            Theme = sourcePage.Theme.Clone()
        };
        sourcePage.Owner?.TableLayoutDiagnostics.RecordCellDrawBufferAllocation();
    }

    public PdfElement[] Draw(
        IMeasurable content,
        LayoutMeasurement measurement,
        float contentLeft,
        float contentTop,
        float contentWidth,
        float contentHeight)
    {
        Reset();
        var column = new FlowColumn(0, contentLeft, contentWidth, contentTop, contentTop - contentHeight);
        var drawContext = new LayoutDrawContext(_scratchPage, column, contentLeft, contentTop, contentWidth, _options);
        content.Draw(drawContext, measurement);
        PdfElement[] elements = _scratchPage.ElementList.ToArray();
        Reset();
        return elements;
    }

    private void Reset()
    {
        _scratchPage.ElementList.Clear();
        _scratchPage.HeaderElements.Clear();
        _scratchPage.FooterElements.Clear();
    }

}
