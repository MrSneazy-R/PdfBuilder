namespace PdfBuilder.Document.Layout.Components;

internal interface IPageAwareMeasurable
{
    string DiagnosticPath { get; }
}

internal sealed class PageVisibilityComponent : IMeasurable, IPageAwareMeasurable
{
    private readonly IMeasurable _child;
    private readonly PageVisibilityRule _rule;

    internal PageVisibilityComponent(IMeasurable child, PageVisibilityRule rule, string diagnosticPath)
    {
        _child = child ?? throw new ArgumentNullException(nameof(child));
        _rule = rule?.Clone() ?? throw new ArgumentNullException(nameof(rule));
        DiagnosticPath = string.IsNullOrWhiteSpace(diagnosticPath) ? "Page-aware content" : diagnosticPath;
    }

    public string DiagnosticPath { get; }

    public LayoutMeasurement Measure(LayoutMeasureContext context)
    {
        (int currentPage, int totalPages) = ResolvePageNumbers(context.Page);
        if (_rule.LastPageOnly && currentPage < totalPages)
            return LayoutMeasurement.Wrap(context.AvailableWidth);
        if (!_rule.Matches(currentPage, totalPages))
            return new LayoutMeasurement(0f, 0f, 0f, 0f, new VisibilityMeasurement(false, null));

        LayoutMeasurement child = _child.Measure(context);
        IMeasurable? remainder = child.Remainder == null
            ? null
            : new PageVisibilityComponent(child.Remainder, _rule, DiagnosticPath);
        return new LayoutMeasurement(
            child.MarginTop,
            child.ContentHeight,
            child.MarginBottom,
            child.UsedWidth,
            new VisibilityMeasurement(true, child),
            child.AvoidBreakInside,
            child.Result,
            remainder);
    }

    public void Draw(LayoutDrawContext context, LayoutMeasurement measurement)
    {
        if (measurement.Metadata is VisibilityMeasurement { Visible: true, Child: not null } visibility)
            _child.Draw(context, visibility.Child);
    }

    private static (int CurrentPage, int TotalPages) ResolvePageNumbers(PdfBuilder.Models.PdfPage page)
    {
        if (HeaderFooterRenderScope.TryGetCurrent(out HeaderFooterRenderContext repeatedContent))
            return (repeatedContent.PageContext.CurrentPage, repeatedContent.PageContext.TotalPages);

        int currentPage = Math.Max(1, page.CompositionPageNumber);
        int totalPages = Math.Max(currentPage, page.Owner?.CompositionTotalPagesHint ?? currentPage);
        return (currentPage, totalPages);
    }

    private sealed record VisibilityMeasurement(bool Visible, LayoutMeasurement? Child);
}
