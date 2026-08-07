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

            // Write the outline root first so every child can reference a valid parent object.
            // PdfStreamWriter assigns sequential object ids, allowing the child range to be
            // determined before those objects are emitted.
            int topId = w.BeginObject();
            int firstNodeId = topId + 1;
            int lastNodeId = topId + items.Count;
            w.WriteLine("<< /Type /Outlines");
            w.WriteLine($"/First {firstNodeId} 0 R /Last {lastNodeId} 0 R /Count {items.Count}");
            w.WriteLine(">>");
            w.EndObject();

            for (int index = 0; index < items.Count; index++)
            {
                var item = items[index];
                int id = w.BeginObject();
                w.WriteLine("<<");
                w.WriteLine($"/Title {PdfStringEncoder.Encode(item.Title)}");
                w.WriteLine($"/Parent {topId} 0 R");
                if (index > 0) w.WriteLine($"/Prev {id - 1} 0 R");
                if (index < items.Count - 1) w.WriteLine($"/Next {id + 1} 0 R");
                w.WriteLine($"/Dest [{item.PageObjId} 0 R /XYZ {item.X:0.###} {item.Y:0.###} null]");
                w.WriteLine(">>");
                w.EndObject();
            }

            return topId;
        }
    }
}
