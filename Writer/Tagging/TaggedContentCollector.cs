using System.Text;
using PdfBuilder.Document;

namespace PdfBuilder.Writer.Tagging;

internal sealed class TaggedContentCollector
{
    private readonly int _pageIndex;
    private readonly PdfSemanticRegistry _registry;
    private int _nextMcid;

    internal TaggedContentCollector(int pageIndex, PdfSemanticRegistry registry)
    {
        _pageIndex = pageIndex;
        _registry = registry;
    }

    internal List<TaggedContentItem> Items { get; } = new();

    internal bool Begin(PdfElement element, int? inheritedNodeId, StringBuilder content, bool suppressed)
    {
        if (suppressed || !IsDrawable(element))
            return false;

        int? nodeId = element.SemanticNodeId ?? inheritedNodeId;
        if (element.IsSemanticArtifact || !nodeId.HasValue || nodeId <= 0 || nodeId > _registry.Nodes.Count)
        {
            content.Append("/Artifact BMC\n");
            return true;
        }

        PdfSemanticNode node = _registry.Nodes[nodeId.Value - 1];
        int mcid = _nextMcid++;
        content.Append($"/{PdfSemanticRoleNames.PdfName(node.Role)} <</MCID {mcid}>> BDC\n");
        Items.Add(new TaggedContentItem(_pageIndex, mcid, node.Id));
        return true;
    }

    internal static void End(StringBuilder content) => content.Append("EMC\n");

    private static bool IsDrawable(PdfElement element)
        => element is not AnchorElement and not LinkRectElement;
}

internal sealed record TaggedContentItem(int PageIndex, int Mcid, int NodeId);
