using PdfBuilder.Document;

namespace PdfBuilder.Writer.Tagging;

internal sealed record TaggedPdfStructureResult(int StructureTreeRootId, int ParentTreeId);

internal static class TaggedPdfStructureWriter
{
    internal static TaggedPdfStructureResult Write(
        PdfStreamWriter writer,
        PdfDocument document,
        IReadOnlyList<int> pageObjectIds,
        IReadOnlyList<IReadOnlyList<TaggedContentItem>> pageItems,
        IReadOnlyList<IReadOnlyList<AnnotationWriter.LinkAnnot>> pageAnnotations)
    {
        IReadOnlyList<PdfSemanticNode> nodes = document.SemanticRegistry.Nodes;
        int rootId = writer.ReserveObject();
        int parentTreeId = writer.ReserveObject();
        int documentNodeId = writer.ReserveObject();
        var nodeObjectIds = nodes.ToDictionary(node => node.Id, _ => writer.ReserveObject());

        var contentByNode = pageItems
            .SelectMany(items => items)
            .GroupBy(item => item.NodeId)
            .ToDictionary(group => group.Key, group => group.OrderBy(item => item.PageIndex).ThenBy(item => item.Mcid).ToArray());
        var annotationsByNode = pageAnnotations
            .SelectMany(items => items)
            .Where(annotation => annotation.SemanticNodeId.HasValue && annotation.ObjectId > 0)
            .GroupBy(annotation => annotation.SemanticNodeId!.Value)
            .ToDictionary(group => group.Key, group => group.OrderBy(annotation => annotation.StructParentKey).ToArray());

        foreach (PdfSemanticNode node in nodes)
        {
            writer.BeginReservedObject(nodeObjectIds[node.Id]);
            writer.WriteLine("<< /Type /StructElem");
            writer.WriteLine($" /S /{PdfSemanticRoleNames.PdfName(node.Role)}");
            writer.WriteLine($" /P {(node.ParentId == 0 ? documentNodeId : nodeObjectIds[node.ParentId])} 0 R");
            if (!string.IsNullOrWhiteSpace(node.AlternativeText))
                writer.WriteLine($" /Alt {PdfStringEncoder.Encode(node.AlternativeText!)}");

            var kids = new List<string>();
            foreach (int childId in OrderedChildren(node, nodes))
                kids.Add($"{nodeObjectIds[childId]} 0 R");
            if (contentByNode.TryGetValue(node.Id, out TaggedContentItem[]? markedContent))
            {
                kids.AddRange(markedContent.Select(item =>
                    $"<< /Type /MCR /Pg {pageObjectIds[item.PageIndex]} 0 R /MCID {item.Mcid} >>"));
            }
            if (annotationsByNode.TryGetValue(node.Id, out AnnotationWriter.LinkAnnot[]? annotations))
            {
                kids.AddRange(annotations.Select(annotation =>
                    $"<< /Type /OBJR /Pg {pageObjectIds[annotation.PageIndex]} 0 R /Obj {annotation.ObjectId} 0 R >>"));
            }
            writer.WriteLine($" /K [{string.Join(" ", kids)}]");
            writer.WriteLine(">>");
            writer.EndObject();
        }

        writer.BeginReservedObject(parentTreeId);
        var numberTree = new List<string>();
        for (int pageIndex = 0; pageIndex < pageItems.Count; pageIndex++)
        {
            IReadOnlyList<TaggedContentItem> items = pageItems[pageIndex];
            int count = items.Count == 0 ? 0 : items.Max(item => item.Mcid) + 1;
            string[] parents = Enumerable.Repeat("null", count).ToArray();
            foreach (TaggedContentItem item in items)
                parents[item.Mcid] = $"{nodeObjectIds[item.NodeId]} 0 R";
            numberTree.Add($"{pageIndex} [{string.Join(" ", parents)}]");
        }
        foreach (AnnotationWriter.LinkAnnot annotation in pageAnnotations.SelectMany(items => items)
                     .Where(annotation => annotation.StructParentKey.HasValue && annotation.SemanticNodeId.HasValue))
        {
            numberTree.Add($"{annotation.StructParentKey!.Value} {nodeObjectIds[annotation.SemanticNodeId!.Value]} 0 R");
        }
        writer.WriteLine($"<< /Nums [{string.Join(" ", numberTree)}] >>");
        writer.EndObject();

        IEnumerable<PdfSemanticNode> topLevel = nodes
            .Where(node => node.ParentId == 0)
            .OrderBy(node => node.ReadingOrder ?? int.MaxValue)
            .ThenBy(node => node.Sequence);
        writer.BeginReservedObject(documentNodeId);
        writer.WriteLine("<< /Type /StructElem /S /Document");
        writer.WriteLine($" /P {rootId} 0 R");
        writer.WriteLine($" /K [{string.Join(" ", topLevel.Select(node => $"{nodeObjectIds[node.Id]} 0 R"))}]");
        writer.WriteLine(">>");
        writer.EndObject();

        writer.BeginReservedObject(rootId);
        writer.WriteLine("<< /Type /StructTreeRoot");
        writer.WriteLine($" /K [{documentNodeId} 0 R]");
        writer.WriteLine($" /ParentTree {parentTreeId} 0 R");
        if (document.Tagging.RoleMap.Count > 0)
        {
            string roleMap = string.Join(" ", document.Tagging.RoleMap.OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => $"{PdfNameEncoder.Encode(pair.Key)} {PdfNameEncoder.Encode(pair.Value)}"));
            writer.WriteLine($" /RoleMap << {roleMap} >>");
        }
        writer.WriteLine(">>");
        writer.EndObject();

        return new TaggedPdfStructureResult(rootId, parentTreeId);
    }

    private static IEnumerable<int> OrderedChildren(PdfSemanticNode node, IReadOnlyList<PdfSemanticNode> nodes)
        => node.Children
            .Select(id => nodes[id - 1])
            .OrderBy(child => child.ReadingOrder ?? int.MaxValue)
            .ThenBy(child => child.Sequence)
            .Select(child => child.Id);
}
