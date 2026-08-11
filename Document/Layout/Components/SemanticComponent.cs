using PdfBuilder.Elements;

namespace PdfBuilder.Document.Layout.Components;

internal sealed class SemanticComponent : IMeasurable
{
    private readonly IMeasurable _child;
    private readonly PdfSemanticDescriptor? _descriptor;
    private readonly bool _artifact;

    internal SemanticComponent(IMeasurable child, PdfSemanticDescriptor? descriptor, bool artifact)
    {
        _child = child ?? throw new ArgumentNullException(nameof(child));
        _descriptor = descriptor;
        _artifact = artifact;
    }

    public LayoutMeasurement Measure(LayoutMeasureContext context)
    {
        LayoutMeasurement inner = _child.Measure(context);
        return new LayoutMeasurement(
            inner.MarginTop,
            inner.ContentHeight,
            inner.MarginBottom,
            inner.UsedWidth,
            new SemanticMetadata(inner),
            inner.AvoidBreakInside,
            inner.Result,
            inner.Remainder == null ? null : new SemanticComponent(inner.Remainder, _descriptor, _artifact));
    }

    public void Draw(LayoutDrawContext context, LayoutMeasurement measurement)
    {
        if (measurement.Metadata is not SemanticMetadata metadata)
            throw new InvalidOperationException("Semantic measurement metadata missing.");

        int bodyStart = context.Page.ElementList.Count;
        int headerStart = context.Page.HeaderElements.Count;
        int footerStart = context.Page.FooterElements.Count;
        PdfSemanticNode? node = null;
        IDisposable? scope = null;
        if (!_artifact && _descriptor != null && context.Page.Owner?.Tagging.Enabled == true)
        {
            node = context.Page.Owner.SemanticRegistry.GetOrCreate(_descriptor);
            scope = context.Page.Owner.SemanticRegistry.Enter(node.Id);
        }

        try
        {
            _child.Draw(context, metadata.Inner);
        }
        finally
        {
            scope?.Dispose();
        }

        Mark(context.Page.ElementList, bodyStart, node?.Id);
        Mark(context.Page.HeaderElements, headerStart, node?.Id);
        Mark(context.Page.FooterElements, footerStart, node?.Id);
    }

    private void Mark(IReadOnlyList<PdfElement> elements, int start, int? nodeId)
    {
        for (int index = start; index < elements.Count; index++)
        {
            PdfElement element = elements[index];
            if (element.SemanticNodeId.HasValue || element.IsSemanticArtifact)
                continue;
            if (_artifact || element is SolidRectElement or DebugRectangleElement)
                element.IsSemanticArtifact = true;
            else
                element.SemanticNodeId = nodeId;
        }
    }

    private sealed record SemanticMetadata(LayoutMeasurement Inner);
}
