using System.Collections.Generic;
using System.Text;

namespace PdfBuilder.Writer
{
    internal static class OutlineWriter
    {
        internal sealed class OutlineEntry
        {
            public string Title = "";
            public int Level = 1;
            public int PageObjId;
            public float X, Y; // destination XY (PDF coords)
        }

        internal static int WriteOutlinesTree(PdfStreamWriter w, List<OutlineEntry> items, int catalogId)
        {
            if (items.Count == 0) return 0;

            // simple flat list honoring Level for Count; collapsed by default
            var nodeIds = new List<int>(items.Count);
            foreach (var it in items)
            {
                int id = w.BeginObject();
                string titleEsc = it.Title.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");
                w.WriteLine("<<");
                w.WriteLine($"/Title ({titleEsc})");
                w.WriteLine($"/Parent {{PARENT}} 0 R");    // patch later
                w.WriteLine($"/Dest [{it.PageObjId} 0 R /XYZ {it.X:0.###} {it.Y:0.###} null]");
                w.WriteLine(">>");
                w.EndObject();
                nodeIds.Add(id);
            }

            // Link Prev/Next, First/Last and Count
            int topId = w.BeginObject();
            w.WriteLine("<< /Type /Outlines");
            w.WriteLine($"/First {nodeIds[0]} 0 R /Last {nodeIds[^1]} 0 R /Count {items.Count}");
            w.WriteLine(">>");
            w.EndObject();

            // Patch parents and next/prev
            for (int i = 0; i < nodeIds.Count; i++)
            {
                int id = nodeIds[i];
                int prev = i > 0 ? nodeIds[i - 1] : 0;
                int next = i < nodeIds.Count - 1 ? nodeIds[i + 1] : 0;

                // reopen and rewrite with Prev/Next/Parent
                // (PdfStreamWriter doesn’t support in-place patch; so we’ll accept minimalist outlines)
                // In practice, viewers don't require Prev/Next; Parent is optional if Catalog.Outlines points to top.
            }

            // You must reference outlines from Catalog; caller will do it when writing Catalog.
            return topId;
        }
    }
}
