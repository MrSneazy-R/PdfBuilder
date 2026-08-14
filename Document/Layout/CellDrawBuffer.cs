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
        NormalizeTextBaselines(_scratchPage.Elements);
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

    private static void NormalizeTextBaselines(IEnumerable<PdfElement> elements)
    {
        foreach (PdfElement element in elements)
        {
            switch (element)
            {
                case TextElement text when text.ShapedLayout is { Lines.Count: > 0 } layout:
                    int textLine = Math.Clamp(text.ShapedStartLine, 0, layout.Lines.Count - 1);
                    text.Y -= layout.Lines[textLine].Ascent;
                    break;
                case RichTextElement richText when richText.ShapedLayout is { Lines.Count: > 0 } layout:
                    int richTextLine = Math.Clamp(richText.ShapedStartLine, 0, layout.Lines.Count - 1);
                    richText.Y -= layout.Lines[richTextLine].Ascent;
                    break;
                case ClipGroupElement clipGroup:
                    NormalizeTextBaselines(clipGroup.Children);
                    break;
            }
        }
    }
}
