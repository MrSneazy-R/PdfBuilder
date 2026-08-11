using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace PdfBuilder.Writer
{
    internal static class AnnotationWriter
    {
        internal sealed class LinkAnnot
        {
            public float X1, Y1, X2, Y2;
            public string? Url;
            public string? Anchor; // resolve later to page/dest
        }

        internal readonly struct Dest
        {
            public readonly float X;
            public readonly float Y;
            public Dest(float x, float y) { X = x; Y = y; }
        }

        /// <summary>
        /// Returns list of annotation object ids to attach to the page.
        /// </summary>
        public static List<int> WriteLinkAnnots(
            PdfStreamWriter w,
            List<LinkAnnot> annots,
            Func<string, int> pageRefIdByAnchor,   // returns page object id (0 if unknown)
            Func<string, Dest> destByAnchor)       // XY for /XYZ
        {
            var ids = new List<int>(annots.Count);

            foreach (var a in annots)
            {
                int internalPageId = 0;
                Dest internalDestination = default;
                if (!string.IsNullOrEmpty(a.Anchor))
                {
                    internalPageId = pageRefIdByAnchor(a.Anchor);
                    if (internalPageId == 0)
                        continue;
                    internalDestination = destByAnchor(a.Anchor);
                }

                int id = w.BeginObject();
                var sb = new StringBuilder();
                sb.Append("<< /Type /Annot /Subtype /Link ");
                sb.AppendFormat(CultureInfo.InvariantCulture,
                    "/Rect [{0} {1} {2} {3}] /Border [0 0 0] ",
                    a.X1, a.Y1, a.X2, a.Y2);

                if (!string.IsNullOrEmpty(a.Url))
                {
                    sb.Append($"/A << /S /URI /URI {PdfStringEncoder.Encode(a.Url)} >> ");
                }
                else if (!string.IsNullOrEmpty(a.Anchor))
                {
                    sb.AppendFormat(CultureInfo.InvariantCulture,
                        "/Dest [{0} 0 R /XYZ {1:0.###} {2:0.###} null] ",
                        internalPageId, internalDestination.X, internalDestination.Y);
                }

                sb.Append(">>");
                w.WriteLine(sb.ToString());
                w.EndObject();
                ids.Add(id);
            }

            return ids;
        }
    }
}
