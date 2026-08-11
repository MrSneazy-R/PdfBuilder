using System.Collections.Generic;

namespace PdfBuilder.Writer
{
    internal static class OutlineWriter
    {
        internal sealed class OutlineEntry
        {
            public string Title = "";
            public int Level = 1;
            public int PageObjId;
            public float X, Y;
        }

        internal static int WriteOutlinesTree(PdfStreamWriter w, List<OutlineEntry> items, int catalogId)
        {
            if (items.Count == 0) return 0;

            var root = new OutlineNode(null, 0);
            var stack = new List<OutlineNode>();
            foreach (OutlineEntry item in items)
            {
                int level = Math.Max(1, item.Level);
                while (stack.Count > 0 && stack[^1].Level >= level)
                    stack.RemoveAt(stack.Count - 1);

                OutlineNode parent = stack.Count > 0 ? stack[^1] : root;
                var node = new OutlineNode(item, level) { Parent = parent };
                parent.Children.Add(node);
                stack.Add(node);
            }

            int topId = w.ReserveObject();
            root.ObjectId = topId;
            var allNodes = Flatten(root.Children).ToList();
            foreach (OutlineNode node in allNodes)
                node.ObjectId = w.ReserveObject();

            w.BeginReservedObject(topId);
            w.WriteLine("<< /Type /Outlines");
            w.WriteLine($"/First {root.Children[0].ObjectId} 0 R /Last {root.Children[^1].ObjectId} 0 R /Count {allNodes.Count}");
            w.WriteLine(">>");
            w.EndObject();

            foreach (OutlineNode node in allNodes)
            {
                OutlineEntry item = node.Entry!;
                int siblingIndex = node.Parent!.Children.IndexOf(node);
                w.BeginReservedObject(node.ObjectId);
                w.WriteLine("<<");
                w.WriteLine($"/Title {PdfStringEncoder.Encode(item.Title)}");
                w.WriteLine($"/Parent {node.Parent.ObjectId} 0 R");
                if (siblingIndex > 0) w.WriteLine($"/Prev {node.Parent.Children[siblingIndex - 1].ObjectId} 0 R");
                if (siblingIndex < node.Parent.Children.Count - 1) w.WriteLine($"/Next {node.Parent.Children[siblingIndex + 1].ObjectId} 0 R");
                if (node.Children.Count > 0)
                {
                    w.WriteLine($"/First {node.Children[0].ObjectId} 0 R");
                    w.WriteLine($"/Last {node.Children[^1].ObjectId} 0 R");
                    w.WriteLine($"/Count {Flatten(node.Children).Count()}");
                }
                w.WriteLine($"/Dest [{item.PageObjId} 0 R /XYZ {item.X:0.###} {item.Y:0.###} null]");
                w.WriteLine(">>");
                w.EndObject();
            }

            return topId;
        }

        private static IEnumerable<OutlineNode> Flatten(IEnumerable<OutlineNode> nodes)
        {
            foreach (OutlineNode node in nodes)
            {
                yield return node;
                foreach (OutlineNode child in Flatten(node.Children))
                    yield return child;
            }
        }

        private sealed class OutlineNode
        {
            internal OutlineNode(OutlineEntry? entry, int level)
            {
                Entry = entry;
                Level = level;
            }

            internal OutlineEntry? Entry { get; }
            internal int Level { get; }
            internal int ObjectId { get; set; }
            internal OutlineNode? Parent { get; set; }
            internal List<OutlineNode> Children { get; } = new();
        }
    }
}
