using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using PdfBuilder.Document;
using PdfBuilder.Elements;
using PdfBuilder.Models;

namespace PdfBuilder.Writer
{
    /// <summary>
    /// High level writer that consumes the builder models, renders them via the renderers, and
    /// emits a standards-compliant PDF stream (xref table, catalog, outlines, annotations, etc.).
    /// </summary>
    public class PdfWriter
    {
        private static readonly IFormatProvider Inv = CultureInfo.InvariantCulture;
        private static string N(double v) => v.ToString("0.###", Inv);

        public byte[] GenerateBytes(PdfDocument doc)
        {
            if (doc == null) throw new ArgumentNullException(nameof(doc));
            if (doc.Pages.Count == 0) throw new InvalidOperationException("Document has no pages.");

            // Allow table paginator to split long tables across pages before we render.
            var laidOut = TablePaginator.Paginate(doc);
            int pageCount = laidOut.Pages.Count;
            if (pageCount == 0) throw new InvalidOperationException("Document has no pages after pagination.");

            DateTime nowUtc = DateTime.UtcNow;

            using var ms = new MemoryStream();
            using var writer = new PdfStreamWriter(ms);
            writer.WriteHeader("1.6");

            // Fonts (base-14 Type1) ---------------------------------------------------------------
            var baseFonts = CollectBaseFonts(laidOut);
            var fontObjId = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var baseFont in baseFonts)
            {
                int id = writer.BeginObject();
                // Base-14 Type1 font with WinAnsiEncoding so 8-bit CP1252 text renders correctly
                writer.WriteLine($"<< /Type /Font /Subtype /Type1 /BaseFont /{baseFont} /Encoding /WinAnsiEncoding >>");
                writer.EndObject();
                fontObjId[baseFont] = id;
            }
            string fontRes = string.Join(" ", fontObjId.Select(kv => $"/F{kv.Value} {kv.Value} 0 R"));

            // Images / ExtGState -----------------------------------------------------------------
            var resources = new PdfResourceManager();
            var imageResourceMap = PreRegisterImages(laidOut, resources, writer);
            PreRegisterWatermarks(laidOut, resources, writer);
            string xobjRes = resources.BuildXObjectResources();
            string gsRes = resources.BuildExtGStateResources();

            // PREPASS: render each page to a byte[] and collect annotations/anchors --------------
            var preContent = new List<byte[]>(pageCount);
            var pageAnnotations = new List<List<AnnotationWriter.LinkAnnot>>(pageCount);
            var pageAnchors = new List<List<(AnchorElement anchor, float xPdf, float yPdf)>>(pageCount);
            var anchorLookup = new Dictionary<string, (int pageIndex, float xPdf, float yPdf)>(StringComparer.Ordinal);

            for (int i = 0; i < pageCount; i++)
            {
                var page = laidOut.Pages[i];
                var pageImgMap = BuildPageImageMap(page, imageResourceMap);

                var (contentBytes, annots, anchors) = BuildContentStream(
                    laidOut,
                    page,
                    i + 1,
                    pageCount,
                    fontObjId,
                    pageImgMap,
                    nowUtc);

                preContent.Add(contentBytes);
                pageAnnotations.Add(annots);
                pageAnchors.Add(anchors);

                foreach (var (anchor, xPdf, yPdf) in anchors)
                {
                    if (!string.IsNullOrWhiteSpace(anchor.Id))
                        anchorLookup[anchor.Id] = (i, xPdf, yPdf);
                }
            }

            // Annotation counts -> predict page object ids (content + annots interleave) ---------
            var annCount = pageAnnotations.Select(list => list.Count).ToArray();
            var cumAnn = new int[pageCount];
            int running = 0;
            for (int i = 0; i < pageCount; i++)
            {
                running += annCount[i];
                cumAnn[i] = running;
            }

            // /Pages ---------------------------------------------------------------------------------
            int pagesObjId = writer.BeginObject();
            var predictedPageIds = new List<int>(pageCount);
            for (int i = 0; i < pageCount; i++)
                predictedPageIds.Add(pagesObjId + 2 * (i + 1) + cumAnn[i]);

            string kids = string.Join(" ", predictedPageIds.Select(id => $"{id} 0 R"));
            writer.WriteLine("<<");
            writer.WriteLine(" /Type /Pages");
            writer.WriteLine($" /Kids [{kids}]");
            writer.WriteLine($" /Count {pageCount}");
            writer.WriteLine(">>");
            writer.EndObject();

            // Pages ----------------------------------------------------------------------------------
            var actualPageIds = new List<int>(pageCount);

            for (int i = 0; i < pageCount; i++)
            {
                var page = laidOut.Pages[i];

                // a) content stream
                int contentId = writer.BeginObject();
                writer.WriteInlineStream(preContent[i]);
                writer.EndObject();

                // b) annotations (external + internal anchors)
                List<int> annotIds = new();
                if (pageAnnotations[i].Count > 0)
                {
                    int PageRefByAnchor(string anchor)
                    {
                        if (!anchorLookup.TryGetValue(anchor, out var info)) return 0;
                        return predictedPageIds[info.pageIndex];
                    }

                    AnnotationWriter.Dest DestByAnchor(string anchor)
                    {
                        if (!anchorLookup.TryGetValue(anchor, out var info))
                            return new AnnotationWriter.Dest(0, 0);
                        return new AnnotationWriter.Dest(info.xPdf, info.yPdf);
                    }

                    annotIds = AnnotationWriter.WriteLinkAnnots(writer, pageAnnotations[i], PageRefByAnchor, DestByAnchor);
                }

                // c) page dictionary
                int pageId = writer.BeginObject();
                writer.WriteLine("<<");
                writer.WriteLine(" /Type /Page");
                writer.WriteLine($" /Parent {pagesObjId} 0 R");
                writer.WriteLine($" /MediaBox [0 0 {N(page.Width)} {N(page.Height)}]");
                writer.WriteLine($" /Contents {contentId} 0 R");

                var resSb = new StringBuilder();
                resSb.Append(" /Resources <<");
                if (fontObjId.Count > 0) resSb.Append($" /Font << {fontRes} >>");
                if (!string.IsNullOrWhiteSpace(xobjRes)) resSb.Append($" /XObject << {xobjRes} >>");
                if (!string.IsNullOrWhiteSpace(gsRes)) resSb.Append($" /ExtGState << {gsRes} >>");
                resSb.Append(" /ProcSet [/PDF /Text /ImageB /ImageC /ImageI] >>");
                writer.WriteLine(resSb.ToString());

                if (annotIds.Count > 0)
                    writer.WriteLine($" /Annots [{string.Join(" ", annotIds.Select(id => $"{id} 0 R"))}]");

                writer.WriteLine(">>");
                writer.EndObject();

                actualPageIds.Add(pageId);
            }

            // Outlines (bookmarks) -------------------------------------------------------------------
            var outlineItems = new List<OutlineWriter.OutlineEntry>();
            for (int i = 0; i < pageCount; i++)
            {
                foreach (var (anchor, xPdf, yPdf) in pageAnchors[i])
                {
                    if (string.IsNullOrWhiteSpace(anchor.Title)) continue;
                    outlineItems.Add(new OutlineWriter.OutlineEntry
                    {
                        Title = anchor.Title!,
                        Level = Math.Max(1, anchor.Level),
                        PageObjId = actualPageIds[i],
                        X = xPdf,
                        Y = yPdf
                    });
                }
            }

            int outlinesId = outlineItems.Count > 0
                ? OutlineWriter.WriteOutlinesTree(writer, outlineItems, 0)
                : 0;

            // Catalog -------------------------------------------------------------------------------
            int catalogId = writer.BeginObject();
            if (outlinesId != 0)
                writer.WriteLine($"<< /Type /Catalog /Pages {pagesObjId} 0 R /Outlines {outlinesId} 0 R >>");
            else
                writer.WriteLine($"<< /Type /Catalog /Pages {pagesObjId} 0 R >>");
            writer.EndObject();

            // Info ----------------------------------------------------------------------------------
            int infoId = writer.BeginObject();
            writer.WriteLine("<<");
            if (!string.IsNullOrWhiteSpace(doc.Title))
                writer.WriteLine($"/Title ({Escape(doc.Title!)})");
            writer.WriteLine($"/Producer (PdfBuilder)");
            writer.WriteLine($"/Creator (PdfBuilder)");
            writer.WriteLine($"/CreationDate (D:{nowUtc:yyyyMMddHHmmss}Z)");
            writer.WriteLine(">>");
            writer.EndObject();

            // XRef & trailer ------------------------------------------------------------------------
            writer.WriteXRefAndTrailer(catalogId, infoId);

            return ms.ToArray();
        }

        // -----------------------------------------------------------------------------------------
        // Helpers
        // -----------------------------------------------------------------------------------------

        private static Dictionary<ImageElement, (int imageObjId, string? gsName)> PreRegisterImages(
            PdfDocument doc,
            PdfResourceManager resources,
            PdfStreamWriter writer)
        {
            var map = new Dictionary<ImageElement, (int imageObjId, string? gsName)>(ReferenceEqualityComparer.Instance);

            foreach (var img in doc.Pages.SelectMany(p => p.Elements).OfType<ImageElement>())
            {
                var (imageObj, _, gsName, _) = resources.EnsureImageXObject(writer, img);
                map[img] = (imageObj, gsName);
            }

            return map;
        }

        private static Dictionary<ImageElement, (int imageObjId, string? gsName)> BuildPageImageMap(
            PdfPage page,
            Dictionary<ImageElement, (int imageObjId, string? gsName)> globalMap)
        {
            var result = new Dictionary<ImageElement, (int imageObjId, string? gsName)>(ReferenceEqualityComparer.Instance);
            foreach (var img in page.Elements.OfType<ImageElement>())
            {
                if (globalMap.TryGetValue(img, out var ids))
                    result[img] = ids;
            }
            return result;
        }

        private static void PreRegisterWatermarks(
            PdfDocument doc,
            PdfResourceManager resources,
            PdfStreamWriter writer)
        {
            foreach (var wm in EnumerateWatermarks(doc))
            {
                if (wm.Opacity < 0.999f)
                {
                    wm.ExtGStateResourceName = resources.EnsureWatermarkExtGState(wm.Opacity, writer);
                }
                else
                {
                    wm.ExtGStateResourceName = null;
                }
            }
        }

        private static IEnumerable<WatermarkSpec> EnumerateWatermarks(PdfDocument doc)
        {
            if (doc.Master?.Watermark != null)
                yield return doc.Master.Watermark;

            foreach (var page in doc.Pages)
            {
                if (page.MasterOverride?.Watermark != null)
                    yield return page.MasterOverride.Watermark;
            }
        }

        private static (byte[] content,
                         List<AnnotationWriter.LinkAnnot> annotations,
                         List<(AnchorElement anchor, float xPdf, float yPdf)> anchors) BuildContentStream(
            PdfDocument doc,
            PdfPage page,
            int pageIndex1,
            int pageCount,
            Dictionary<string, int> fontObjId,
            Dictionary<ImageElement, (int imageObjId, string? gsName)> pageImageMap,
            DateTime nowUtc)
        {
            var sb = new StringBuilder();
            var annotations = new List<AnnotationWriter.LinkAnnot>();
            var anchorsOnPage = new List<(AnchorElement anchor, float xPdf, float yPdf)>();

            var effectiveMaster = page.MasterOverride ?? doc.Master;
            var effectiveHeaderFooter = page.HeaderFooterOverride ?? doc.HeaderFooter;

            if (effectiveMaster != null)
            {
                MasterRenderer.AppendBackground(sb, page, effectiveMaster);
                if (effectiveMaster.Watermark != null && effectiveMaster.Watermark.Layer == WatermarkLayer.BehindContent)
                    MasterRenderer.AppendWatermark(sb, page, effectiveMaster.Watermark, fontObjId, aboveContent: false);
            }

            foreach (var element in page.Elements)
            {
                switch (element)
                {
                    case TextElement text:
                        TextRenderer.Append(sb, text, page.Height, fontObjId);
                        break;

                    case TableElement table:
                        TableRenderer.Append(sb, table, fontObjId);
                        break;

                    case ImageElement image:
                        if (pageImageMap.TryGetValue(image, out var ids))
                            ImageRenderer.Append(sb, image, page.Height, ids.imageObjId, ids.gsName);
                        break;

                    case RichTextElement richText:
                    {
                        var linkRects = new List<RichTextRenderer.LinkRect>();
                        _ = RichTextRenderer.Append(sb, richText, page.Height, fontObjId, linkRects);
                        annotations.AddRange(ConvertLinkRects(linkRects));
                        break;
                    }

                    case ListElement list:
                    {
                        var linkRects = new List<RichTextRenderer.LinkRect>();
                        ListRenderer.Append(sb, list, page.Height, fontObjId, linkRects);
                        annotations.AddRange(ConvertLinkRects(linkRects));
                        break;
                    }

                    case ChartElement chart:
                        ChartRenderer.Append(sb, chart, fontObjId);
                        break;

                    case UnderlineElement underline:
                        AppendUnderline(sb, underline);
                        break;

                    case AnchorElement anchor:
                        anchorsOnPage.Add((anchor, anchor.X, anchor.Y));
                        break;

                    case LinkRectElement linkRect:
                        annotations.Add(ConvertLinkRect(linkRect));
                        break;
                }
            }

            if (effectiveHeaderFooter != null)
                HeaderFooterRenderer.Append(sb, doc, page, effectiveHeaderFooter, fontObjId, pageIndex1, pageCount, nowUtc);

            if (effectiveMaster?.Watermark != null && effectiveMaster.Watermark.Layer == WatermarkLayer.AboveContent)
                MasterRenderer.AppendWatermark(sb, page, effectiveMaster.Watermark, fontObjId, aboveContent: true);

            return (Encoding.ASCII.GetBytes(sb.ToString()), annotations, anchorsOnPage);
        }

        private static IEnumerable<AnnotationWriter.LinkAnnot> ConvertLinkRects(IEnumerable<RichTextRenderer.LinkRect> rects)
        {
            foreach (var rect in rects)
            {
                float y1 = Math.Min(rect.Y1, rect.Y2);
                float y2 = Math.Max(rect.Y1, rect.Y2);
                yield return new AnnotationWriter.LinkAnnot
                {
                    X1 = rect.X1,
                    Y1 = y1,
                    X2 = rect.X2,
                    Y2 = y2,
                    Url = rect.Url,
                    Anchor = rect.Anchor
                };
            }
        }

        private static AnnotationWriter.LinkAnnot ConvertLinkRect(LinkRectElement linkRect)
        {
            float x1 = linkRect.X;
            float x2 = linkRect.X + linkRect.Width;
            float top = linkRect.Y;
            float bottom = linkRect.Y - linkRect.Height;
            if (bottom > top) (bottom, top) = (top, bottom);

            return new AnnotationWriter.LinkAnnot
            {
                X1 = Math.Min(x1, x2),
                X2 = Math.Max(x1, x2),
                Y1 = bottom,
                Y2 = top,
                Url = linkRect.Url,
                Anchor = linkRect.Anchor
            };
        }

        private static void AppendUnderline(StringBuilder sb, UnderlineElement line)
        {
            string rgb = TryRgb(line.Color) ?? "0 0 0";
            double radians = line.Rotation * Math.PI / 180.0;
            float x2 = line.X + (float)(line.Width * Math.Cos(radians));
            float y2 = line.Y + (float)(line.Width * Math.Sin(radians));

            sb.Append("q ");
            if (line.Style == LineStyle.Dashed)
                sb.Append("[3 3] 0 d ");

            sb.Append($"{rgb} RG {N(line.Thickness)} w {N(line.X)} {N(line.Y)} m {N(x2)} {N(y2)} l S Q\n");
        }

        private static HashSet<string> CollectBaseFonts(PdfDocument doc)
        {
            var fonts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            void AddFont(string? family, bool bold = false, bool italic = false)
            {
                var base14 = MapToBase14(FontManager.NormalizeFontKey(family, bold, italic));
                fonts.Add(base14);
            }

            void AddFallbacks(IEnumerable<string>? names)
            {
                if (names == null) return;
                foreach (var name in names)
                    AddFont(name);
            }

            // Document-level header/footer + master watermark fonts
            if (doc.HeaderFooter != null)
                AddFont(doc.HeaderFooter.FontFamily);
            if (doc.Master?.Watermark != null)
                AddFont(doc.Master.Watermark.FontFamily);

            foreach (var page in doc.Pages)
            {
                var pageHF = page.HeaderFooterOverride ?? doc.HeaderFooter;
                if (pageHF != null)
                    AddFont(pageHF.FontFamily);

                var pageMaster = page.MasterOverride ?? doc.Master;
                if (pageMaster?.Watermark != null)
                    AddFont(pageMaster.Watermark.FontFamily);

                foreach (var element in page.Elements)
                {
                    switch (element)
                    {
                        case TextElement text:
                            fonts.Add(TextRenderer.PickBaseFont(text));
                            break;

                        case RichTextElement richText:
                            foreach (var run in richText.Runs)
                                AddFont(run.FontFamily, run.Bold, run.Italic);
                            break;

                        case ListElement list:
                            AddFont(list.FontFamily);
                            foreach (var run in EnumerateListRuns(list.Items))
                                AddFont(run.FontFamily, run.Bold, run.Italic);
                            break;

                        case TableElement table:
                            AddFont(table.DefaultFont);
                            if (table.DefaultTextStyle != null)
                            {
                                AddFont(table.DefaultTextStyle.FontFamily, table.DefaultTextStyle.Bold, table.DefaultTextStyle.Italic);
                                AddFallbacks(table.DefaultTextStyle.FallbackFonts);
                            }

                            foreach (var column in table.ColumnStyles)
                                AddFont(column.Font);

                            foreach (var cell in table.Rows.SelectMany(r => r.Cells))
                            {
                                AddFont(cell.Font, cell.Bold, cell.Italic);

                                if (cell.TextStyle != null)
                                {
                                    AddFont(cell.TextStyle.FontFamily, cell.TextStyle.Bold, cell.TextStyle.Italic);
                                    AddFallbacks(cell.TextStyle.FallbackFonts);
                                }

                                if (cell.TextRuns.Count > 0)
                                {
                                    foreach (var inline in cell.TextRuns)
                                    {
                                        if (inline?.Style != null)
                                        {
                                            AddFont(inline.Style.FontFamily, inline.Style.Bold, inline.Style.Italic);
                                            AddFallbacks(inline.Style.FallbackFonts);
                                        }

                                        if (inline?.FallbackFonts != null)
                                        {
                                            foreach (var fallback in inline.FallbackFonts)
                                                AddFont(fallback);
                                        }
                                    }
                                }
                            }
                            break;

                        case ChartElement chart:
                            AddFont(chart.TitleFont);
                            AddFont(chart.Font);
                            AddFont(chart.LegendFont);
                            break;
                    }
                }
            }

            if (fonts.Count == 0)
                fonts.Add("Helvetica");

            return fonts;

            static IEnumerable<RichRun> EnumerateListRuns(IEnumerable<ListItem> items)
            {
                foreach (var item in items)
                {
                    foreach (var run in item.Content)
                        yield return run;
                    foreach (var child in EnumerateListRuns(item.Children))
                        yield return child;
                }
            }
        }

        private static string MapToBase14(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return "Helvetica";
            string normalized = key.Trim();
            string lower = normalized.ToLowerInvariant();

            static bool Has(string text, string token) => text.Contains(token, StringComparison.OrdinalIgnoreCase);

            if (lower.Contains("courier"))
            {
                bool bold = Has(normalized, "Bold");
                bool italic = Has(normalized, "Oblique") || Has(normalized, "Italic");
                if (bold && italic) return "Courier-BoldOblique";
                if (bold) return "Courier-Bold";
                if (italic) return "Courier-Oblique";
                return "Courier";
            }

            if (lower.Contains("times"))
            {
                bool bold = Has(normalized, "Bold");
                bool italic = Has(normalized, "Italic") || Has(normalized, "Oblique");
                if (bold && italic) return "Times-BoldItalic";
                if (bold) return "Times-Bold";
                if (italic) return "Times-Italic";
                return "Times-Roman";
            }

            // Treat Arial and other sans-serif families as Helvetica
            bool boldSans = Has(normalized, "Bold");
            bool italicSans = Has(normalized, "Italic") || Has(normalized, "Oblique");
            if (boldSans && italicSans) return "Helvetica-BoldOblique";
            if (boldSans) return "Helvetica-Bold";
            if (italicSans) return "Helvetica-Oblique";
            return "Helvetica";
        }

        private static string Escape(string value) =>
            string.IsNullOrEmpty(value)
                ? string.Empty
                : value.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");

        private static string? TryRgb(string hex)
        {
            if (string.IsNullOrWhiteSpace(hex)) return null;
            hex = hex.Trim();
            if (hex.StartsWith("#")) hex = hex[1..];
            if (hex.Length == 3)
            {
                hex = new string(new[]
                {
                    hex[0], hex[0],
                    hex[1], hex[1],
                    hex[2], hex[2]
                });
            }
            if (hex.Length == 6 &&
                int.TryParse(hex[..2], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var r) &&
                int.TryParse(hex.Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var g) &&
                int.TryParse(hex.Substring(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var b))
            {
                return $"{(r / 255.0).ToString("0.###", Inv)} {(g / 255.0).ToString("0.###", Inv)} {(b / 255.0).ToString("0.###", Inv)}";
            }
            return null;
        }
    }
}


