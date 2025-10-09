// --- PdfWriter.cs (full) ---
using PdfBuilder.Document;
using PdfBuilder.Elements;
using PdfBuilder.Models;
using PdfBuilder.Writer;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;

namespace PdfBuilder.Writer
{
    public class PdfWriter
    {
        private static readonly IFormatProvider Inv = CultureInfo.InvariantCulture;
        private static string N(double v) => v.ToString("0.###", Inv);

        public byte[] GenerateBytes(PdfDocument doc)
        {
            if (doc == null || doc.Pages.Count == 0)
                throw new InvalidOperationException("Document has no pages.");

            // Pre-layout pagination (tables etc.)
            var laidOut = TablePaginator.Paginate(doc);
            int pageCount = laidOut.Pages.Count;

            using var ms = new MemoryStream();
            using var w = new PdfStreamWriter(ms);

            // PDF header
            w.WriteHeader("1.4");

            // --- 1) Fonts (collect from all elements) ---
            var neededBaseFonts = CollectAllBaseFonts(laidOut);
            var fontObjId = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var baseFont in neededBaseFonts)
            {
                int id = w.BeginObject();
                w.WriteLine($"<< /Type /Font /Subtype /Type1 /BaseFont /{baseFont} >>");
                w.EndObject();
                fontObjId[baseFont] = id;
            }
            string fontRes = string.Join(" ", fontObjId.Select(kv => $"/F{kv.Value} {kv.Value} 0 R"));

            // --- 2) Images via PdfResourceManager (PNG/JPEG, SMask + ExtGState) ---
            var resources = new PdfResourceManager();

            // Pre-register all images so that all image/palette/SMask/GS objects are created
            var allImages = laidOut.Pages.SelectMany(p => p.Elements).OfType<ImageElement>().ToList();
            var elementToImageIds = new Dictionary<ImageElement, (int imageObjId, int? gsObjId)>(RefEq<ImageElement>.Instance);

            foreach (var el in allImages)
            {
                var (imId, _smaskId, gsId, _name) = resources.EnsureImageXObject(w, el);
                elementToImageIds[el] = (imId, gsId);
            }

            // Build resource dictionaries for XObjects and ExtGState
            string xobjRes = resources.BuildXObjectResources();   // "/Im5 5 0 R /Im9 9 0 R ..."
            string gsRes = resources.BuildExtGStateResources();   // "/GS7 7 0 R ..."

            // --- 3) PREPASS: build content bytes, collect link annots & anchors per page ---
            var preContent = new List<byte[]>(pageCount);
            var pageAnnots = new List<List<AnnotationWriter.LinkAnnot>>(pageCount);
            var anchorMap = new Dictionary<string, (int pageIndex, float xPdf, float yPdf)>(StringComparer.Ordinal);

            for (int i = 0; i < pageCount; i++)
            {
                var page = laidOut.Pages[i];

                // Build a per-page image map (Element -> (imageObjId, gsObjId))
                var pageImgMap = new Dictionary<ImageElement, (int imageObjId, int? gsObjId)>(RefEq<ImageElement>.Instance);
                foreach (var img in page.Elements.OfType<ImageElement>())
                {
                    if (elementToImageIds.TryGetValue(img, out var ids))
                        pageImgMap[img] = ids;
                }

                var (bytes, annots, anchorsOnPage) =
                    BuildContentStream(laidOut, page, i + 1, pageCount, fontObjId, pageImgMap);

                preContent.Add(bytes);
                pageAnnots.Add(annots);

                foreach (var (a, xPdf, yPdf) in anchorsOnPage)
                {
                    if (!string.IsNullOrWhiteSpace(a.Id))
                        anchorMap[a.Id] = (i, xPdf, yPdf);
                }
            }

            // Count annots per page & cumulative totals (needed to predict page object ids)
            var annCount = pageAnnots.Select(lst => lst.Count).ToArray();
            var cumAnn = new int[pageCount];
            int running = 0;
            for (int i = 0; i < pageCount; i++)
            {
                running += annCount[i];
                cumAnn[i] = running; // cum up to and including i
            }

            // --- 4) Create /Pages object now and PREDICT child page object ids ---
            // Page i dictionary id will be: pagesObjId + 2*(i+1) + sum_{k=0..i} annCount[k]
            int pagesObjId = w.BeginObject();
            var predictedPageIds = new List<int>(pageCount);
            for (int i = 0; i < pageCount; i++)
                predictedPageIds.Add(pagesObjId + 2 * (i + 1) + cumAnn[i]);

            string kids = string.Join(" ", predictedPageIds.Select(id => $"{id} 0 R"));
            w.WriteLine("<<");
            w.WriteLine(" /Type /Pages");
            w.WriteLine($" /Kids [{kids}]");
            w.WriteLine($" /Count {pageCount}");
            w.WriteLine(">>");
            w.EndObject();

            // --- 5) For each page: write content, then that page's annots, then page dictionary ---
            var actualPageIds = new List<int>(pageCount);

            for (int i = 0; i < pageCount; i++)
            {
                var page = laidOut.Pages[i];

                // a) Content stream
                int contentId = w.BeginObject();
                w.WriteInlineStream(preContent[i]);   // already ASCII bytes
                w.EndObject(); // This object id is: pagesObjId + (2*i + 1) + sum_{k< i} annCount[k]

                // b) Annotation objects for this page (so their ids are just after the content)
                List<int> annotIds = new();
                if (pageAnnots[i].Count > 0)
                {
                    int PageRefByAnchor(string anchor)
                    {
                        if (!anchorMap.TryGetValue(anchor, out var tuple)) return 0;
                        int idx = tuple.pageIndex;
                        // Page dict id for idx:
                        return pagesObjId + 2 * (idx + 1) + cumAnn[idx];
                    }
                    AnnotationWriter.Dest DestByAnchor(string anchor)
                    {
                        if (!anchorMap.TryGetValue(anchor, out var tuple)) return new AnnotationWriter.Dest(0, 0);
                        return new AnnotationWriter.Dest(tuple.xPdf, tuple.yPdf);
                    }

                    annotIds = AnnotationWriter.WriteLinkAnnots(w, pageAnnots[i], PageRefByAnchor, DestByAnchor);
                }

                // c) Page dictionary (this id MUST match predictedPageIds[i])
                int pageId = w.BeginObject();
                w.WriteLine("<<");
                w.WriteLine(" /Type /Page");
                w.WriteLine($" /Parent {pagesObjId} 0 R");
                w.WriteLine($" /MediaBox [0 0 {N(page.Width)} {N(page.Height)}]");
                w.WriteLine($" /Contents {contentId} 0 R");

                // Resources: Fonts + XObjects (images) + ExtGState (opacity) + ProcSet
                var resSb = new StringBuilder();
                resSb.Append(" /Resources <<");
                if (fontObjId.Count > 0) resSb.Append($" /Font << {fontRes} >>");
                if (!string.IsNullOrWhiteSpace(xobjRes)) resSb.Append($" /XObject << {xobjRes} >>");
                if (!string.IsNullOrWhiteSpace(gsRes)) resSb.Append($" /ExtGState << {gsRes} >>");
                resSb.Append(" /ProcSet [/PDF /Text /ImageB /ImageC /ImageI] >>");
                w.WriteLine(resSb.ToString());

                if (annotIds.Count > 0)
                    w.WriteLine($" /Annots [{string.Join(" ", annotIds.Select(id => $"{id} 0 R"))}]");

                w.WriteLine(">>");
                w.EndObject();

                actualPageIds.Add(pageId);
            }

            // --- 6) Outlines (Bookmarks) ---
            var outlineItems = new List<OutlineWriter.OutlineEntry>();
            for (int i = 0; i < pageCount; i++)
            {
                foreach (var a in laidOut.Pages[i].Elements.OfType<AnchorElement>())
                {
                    if (!string.IsNullOrWhiteSpace(a.Title))
                    {
                        // Use the already-computed destination in PDF coords
                        if (!anchorMap.TryGetValue(a.Id ?? "", out var d))
                            d = (i, a.X, laidOut.Pages[i].Height - a.Y);

                        outlineItems.Add(new OutlineWriter.OutlineEntry
                        {
                            Title = a.Title!,
                            Level = Math.Max(1, a.Level),
                            PageObjId = predictedPageIds[i], // matches what we wrote
                            X = d.xPdf,
                            Y = d.yPdf
                        });
                    }
                }
            }

            int outlinesId = 0;
            if (outlineItems.Count > 0)
                outlinesId = OutlineWriter.WriteOutlinesTree(w, outlineItems, 0);

            // --- 7) Catalog ---
            int catalogId = w.BeginObject();
            if (outlinesId != 0)
                w.WriteLine($"<< /Type /Catalog /Pages {pagesObjId} 0 R /Outlines {outlinesId} 0 R >>");
            else
                w.WriteLine($"<< /Type /Catalog /Pages {pagesObjId} 0 R >>");
            w.EndObject();

            // --- 8) xref + trailer ---
            w.WriteXRefAndTrailer(catalogId);

            return ms.ToArray();
        }

        // ---------- Content stream (render + collect links/anchors; no objects written here) ----------
        private static (byte[] content,
                       List<AnnotationWriter.LinkAnnot> annots,
                       List<(AnchorElement anchor, float xPdf, float yPdf)> anchorsOnPage)
        BuildContentStream(
            PdfDocument doc,
            PdfPage page,
            int pageIndex1,
            int pageCount,
            Dictionary<string, int> fontObjId,
            Dictionary<ImageElement, (int imageObjId, int? gsObjId)> pageImageMap)
        {
            var sb = new StringBuilder();
            var pageLinks = new List<AnnotationWriter.LinkAnnot>();
            var pageAnchors = new List<(AnchorElement, float, float)>();

            // Resolve effective master + header/footer
            var effectiveMaster = page.MasterOverride ?? doc.Master;
            var effectiveHF = page.HeaderFooterOverride ?? doc.HeaderFooter;

            // 1) Master background (behind content) + watermark behind
            MasterRenderer.AppendBackground(sb, page, effectiveMaster);
            if (effectiveMaster?.Watermark != null &&
                effectiveMaster.Watermark.Layer == WatermarkLayer.BehindContent)
            {
                MasterRenderer.AppendWatermark(sb, page, effectiveMaster.Watermark, fontObjId, aboveContent: false);
            }

            // 2) Page content (tables, text, images, rich text, lists, anchors, link rects)
            foreach (var el in page.Elements)
            {
                switch (el)
                {
                    case TextElement t:
                        TextRenderer.Append(sb, t, page.Height, fontObjId);
                        break;

                    case TableElement tbl:
                        TableRenderer.Append(sb, tbl, fontObjId);
                        break;

                    case ImageElement img:
                        if (pageImageMap.TryGetValue(img, out var ids))
                            ImageRenderer.Append(sb, img, page.Height, ids.imageObjId, ids.gsObjId);
                        break;

                    case RichTextElement rt:
                    {
                        var linkRects = new List<RichTextRenderer.LinkRect>();
                        RichTextRenderer.Append(sb, rt, page.Height, fontObjId, linkRects);
                        foreach (var lr in linkRects)
                        {
                            // Convert to PDF coords (annotations are bottom-up)
                            float x1 = lr.X1, x2 = lr.X2;
                            float y1 = page.Height - lr.Y2;
                            float y2 = page.Height - lr.Y1;
                            pageLinks.Add(new AnnotationWriter.LinkAnnot
                            {
                                X1 = x1,
                                Y1 = y1,
                                X2 = x2,
                                Y2 = y2,
                                Url = lr.Url,
                                Anchor = lr.Anchor
                            });
                        }
                    }
                    break;

                    case ListElement list:
                    {
                        var linkRects = new List<RichTextRenderer.LinkRect>();
                        ListRenderer.Append(sb, list, page.Height, fontObjId, linkRects);
                        foreach (var lr in linkRects)
                        {
                            float x1 = lr.X1, x2 = lr.X2;
                            float y1 = page.Height - lr.Y2;
                            float y2 = page.Height - lr.Y1;
                            pageLinks.Add(new AnnotationWriter.LinkAnnot
                            {
                                X1 = x1,
                                Y1 = y1,
                                X2 = x2,
                                Y2 = y2,
                                Url = lr.Url,
                                Anchor = lr.Anchor
                            });
                        }
                    }
                    break;

                    case LinkRectElement r:
                    {
                        // r.Y is top-left; convert to PDF coords
                        float x1 = r.X;
                        float x2 = r.X + r.Width;
                        float y2 = page.Height - r.Y;
                        float y1 = page.Height - (r.Y + r.Height);
                        pageLinks.Add(new AnnotationWriter.LinkAnnot { X1 = x1, Y1 = y1, X2 = x2, Y2 = y2, Url = r.Url, Anchor = r.Anchor });
                    }
                    break;

                    case AnchorElement a:
                    {
                        // Store destination in PDF coords for /XYZ
                        float xPdf = a.X;
                        float yPdf = page.Height - a.Y;
                        pageAnchors.Add((a, xPdf, yPdf));
                    }
                    break;
                }
            }

            // 3) Headers / Footers
            if (effectiveHF != null)
            {
                HeaderFooterRenderer.Append(sb, doc, page, effectiveHF, fontObjId,
                    pageIndex1, pageCount, DateTime.UtcNow);
            }

            // 4) Foreground watermark (if configured)
            if (effectiveMaster?.Watermark != null &&
                effectiveMaster.Watermark.Layer == WatermarkLayer.AboveContent)
            {
                MasterRenderer.AppendWatermark(sb, page, effectiveMaster.Watermark, fontObjId, aboveContent: true);
            }

            return (Encoding.ASCII.GetBytes(sb.ToString()), pageLinks, pageAnchors);
        }

        // ---------- Collect fonts needed by Text + Table + Header/Footer + RichText + List + Watermark ----------
        private static HashSet<string> CollectAllBaseFonts(PdfDocument doc)
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            static string Base14(string family, bool bold = false, bool italic = false)
            {
                bool mono = family.Equals("Courier", StringComparison.OrdinalIgnoreCase);
                if (mono)
                {
                    if (bold && italic) return "Courier-BoldOblique";
                    if (bold) return "Courier-Bold";
                    if (italic) return "Courier-Oblique";
                    return "Courier";
                }
                if (bold && italic) return "Helvetica-BoldOblique";
                if (bold) return "Helvetica-Bold";
                if (italic) return "Helvetica-Oblique";
                return "Helvetica";
            }

            // From Text
            foreach (var t in doc.Pages.SelectMany(p => p.Elements).OfType<TextElement>())
                set.Add(TextRenderer.PickBaseFont(t));

            // From Table (cells + headers)
            foreach (var tbl in doc.Pages.SelectMany(p => p.Elements).OfType<TableElement>())
            {
                foreach (var row in tbl.Rows)
                {
                    foreach (var cell in row.Cells)
                    {
                        bool mono = string.Equals(cell.Font, "Courier", StringComparison.OrdinalIgnoreCase);
                        string fam = mono ? "Courier" : "Helvetica";
                        set.Add(Base14(fam)); // extend if/when you add bold/italic to cells
                    }
                }
            }

            // From RichText runs
            foreach (var rt in doc.Pages.SelectMany(p => p.Elements).OfType<RichTextElement>())
            {
                foreach (var r in rt.Runs)
                {
                    string fam = string.IsNullOrWhiteSpace(r.FontFamily) ? "Helvetica" : r.FontFamily;
                    set.Add(Base14(fam, r.Bold, r.Italic));
                }
            }

            // From List (marker/content font)
            foreach (var li in doc.Pages.SelectMany(p => p.Elements).OfType<ListElement>())
                set.Add(Base14(li.FontFamily));

            // From Header/Footer (document defaults + per-page overrides)
            void AddHF(HeaderFooterSpec hf)
            {
                var fam = string.IsNullOrWhiteSpace(hf.FontFamily) ? "Helvetica" : hf.FontFamily.Trim();
                set.Add(Base14(fam));
            }
            if (doc.HeaderFooter != null) AddHF(doc.HeaderFooter);
            foreach (var p in doc.Pages)
                if (p.HeaderFooterOverride != null) AddHF(p.HeaderFooterOverride);

            // From Watermark text
            if (doc.Master?.Watermark?.Text != null)
            {
                string fam = string.IsNullOrWhiteSpace(doc.Master.Watermark.FontFamily) ? "Helvetica" : doc.Master.Watermark.FontFamily;
                set.Add(Base14(fam));
            }
            foreach (var p in doc.Pages)
            {
                var wm = p.MasterOverride?.Watermark;
                if (wm?.Text != null)
                {
                    string fam = string.IsNullOrWhiteSpace(wm.FontFamily) ? "Helvetica" : wm.FontFamily;
                    set.Add(Base14(fam));
                }
            }

            if (set.Count == 0) set.Add("Helvetica");
            return set;
        }

        internal sealed class RefEq<T> : IEqualityComparer<T> where T : class
        {
            public static readonly RefEq<T> Instance = new();
            public bool Equals(T x, T y) => ReferenceEquals(x, y);
            public int GetHashCode(T obj) => RuntimeHelpers.GetHashCode(obj);
        }
    }
}
