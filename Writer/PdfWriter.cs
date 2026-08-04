using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using PdfBuilder.Document;
using PdfBuilder.Document.Layout;
using PdfBuilder.Elements;
using PdfBuilder.Models;
using PdfBuilder.Writer.Fonts;

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

        internal PdfGenerationMetrics? LastGenerationMetrics { get; private set; }

        /// <summary>Generates a PDF into a newly allocated byte array.</summary>
        /// <param name="doc">The document to generate.</param>
        public byte[] GenerateBytes(PdfDocument doc) => GenerateBytes(doc, CancellationToken.None);

        /// <summary>Generates a PDF into a newly allocated byte array.</summary>
        /// <param name="doc">The document to generate.</param>
        /// <param name="cancellationToken">Cancels layout planning or page writing.</param>
        public byte[] GenerateBytes(PdfDocument doc, CancellationToken cancellationToken)
        {
            using var ms = new MemoryStream();
            WriteDocument(doc ?? throw new ArgumentNullException(nameof(doc)), ms, DateTime.UtcNow, cancellationToken);
            if (doc.RenderLimits.MaximumOutputBytes is long maximum && ms.Length > maximum)
                throw new PdfRenderLimitException(nameof(PdfRenderLimits.MaximumOutputBytes), $"The generated PDF exceeds the configured {maximum} byte limit.");
            return ms.ToArray();
        }

        /// <summary>Generates a PDF directly into a writable stream without buffering all page content streams.</summary>
        /// <param name="doc">The document to generate.</param>
        /// <param name="destination">The writable stream that receives the PDF.</param>
        public void GenerateStream(PdfDocument doc, Stream destination) => GenerateStream(doc, destination, CancellationToken.None);

        /// <summary>Generates a PDF directly into a writable stream without buffering all page content streams.</summary>
        /// <param name="doc">The document to generate.</param>
        /// <param name="destination">The writable stream that receives the PDF.</param>
        /// <param name="cancellationToken">Cancels layout planning or page writing.</param>
        public void GenerateStream(PdfDocument doc, Stream destination, CancellationToken cancellationToken)
        {
            if (doc == null) throw new ArgumentNullException(nameof(doc));
            if (destination == null) throw new ArgumentNullException(nameof(destination));
            if (!destination.CanWrite) throw new ArgumentException("Destination stream must be writable.", nameof(destination));

            WriteDocument(doc, destination, DateTime.UtcNow, cancellationToken);
            destination.Flush();
        }

        /// <summary>Generates a PDF directly into a file.</summary>
        /// <param name="doc">The document to generate.</param>
        /// <param name="path">The output file path.</param>
        public void Save(PdfDocument doc, string path) => Save(doc, path, CancellationToken.None);

        /// <summary>Generates a PDF directly into a file.</summary>
        /// <param name="doc">The document to generate.</param>
        /// <param name="path">The output file path.</param>
        /// <param name="cancellationToken">Cancels layout planning or page writing.</param>
        public void Save(PdfDocument doc, string path, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Path must be provided.", nameof(path));
            using var fileStream = File.Create(path);
            WriteDocument(doc ?? throw new ArgumentNullException(nameof(doc)), fileStream, DateTime.UtcNow, cancellationToken);
        }

        public IReadOnlyList<PdfPreviewPage> GeneratePreviewImages(PdfDocument doc, int dpi = 144)
        {
            if (doc == null) throw new ArgumentNullException(nameof(doc));
            return new PdfPreviewGenerator().Generate(doc, dpi);
        }

        /// <summary>Generates selected preview images from the already-resolved document layout.</summary>
        public IReadOnlyList<PdfPreviewPage> GeneratePreviewImages(
            PdfDocument doc,
            int dpi,
            IEnumerable<int>? pageNumbers,
            CancellationToken cancellationToken)
        {
            if (doc == null) throw new ArgumentNullException(nameof(doc));
            return new PdfPreviewGenerator().Generate(doc, dpi, pageNumbers, cancellationToken);
        }

        private void WriteDocument(PdfDocument doc, Stream destination, DateTime nowUtc, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (doc.Pages.Count == 0) throw new InvalidOperationException("Document has no pages.");

            // Flowing content, including tables, is resolved by ColumnBuilder before serialization.
            // The writer must consume that resolved document without cloning or repaginating it.
            var laidOut = doc;
            int pageCount = laidOut.Pages.Count;
            var metrics = new PdfGenerationMetrics { PagesPlanned = pageCount };
            LastGenerationMetrics = metrics;

            HeaderFooterLayoutComposer.Prepare(laidOut, nowUtc);

            using var writer = new PdfStreamWriter(destination);
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
            string baseFontRes = string.Join(" ", fontObjId.Select(kv => $"/F{kv.Value} {kv.Value} 0 R"));

            var embeddedFonts = new EmbeddedFontRegistry();
            var renderContext = new PdfRenderContext(fontObjId, embeddedFonts);

            // Images / ExtGState -----------------------------------------------------------------
            var outputOptions = laidOut.OutputOptions ?? new PdfOutputOptions();

            var resources = new PdfResourceManager(outputOptions, doc.RenderLimits);
            var imageResourceMap = PreRegisterImages(laidOut, resources, writer, cancellationToken);
            PreRegisterWatermarks(laidOut, resources, writer);
            PreRegisterSolidRectOpacity(laidOut, resources, writer);
            string xobjRes = resources.BuildXObjectResources();
            string gsRes = resources.BuildExtGStateResources();

            // RESOURCE-PLANNING PASS ---------------------------------------------------------------
            // Render solely to collect font glyphs plus navigation metadata. Content bytes are
            // deliberately discarded here; the write pass below produces one page at a time.
            var pageAnnotations = new List<List<AnnotationWriter.LinkAnnot>>(pageCount);
            var pageAnchors = new List<List<(AnchorElement anchor, float xPdf, float yPdf)>>(pageCount);
            var anchorLookup = new Dictionary<string, (int pageIndex, float xPdf, float yPdf)>(StringComparer.Ordinal);

            for (int i = 0; i < pageCount; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var page = laidOut.Pages[i];
                var pageImgMap = BuildPageImageMap(page, imageResourceMap);

                var (contentBytes, annots, anchors) = BuildContentStream(
                    laidOut,
                    page,
                    i + 1,
                    pageCount,
                    renderContext,
                    fontObjId,
                    pageImgMap,
                    nowUtc,
                    cancellationToken);

                pageAnnotations.Add(annots);
                pageAnchors.Add(anchors);

                foreach (var (anchor, xPdf, yPdf) in anchors)
                {
                    if (!string.IsNullOrWhiteSpace(anchor.Id))
                        anchorLookup[anchor.Id] = (i, xPdf, yPdf);
                }
            }

            laidOut.Pagination?.ApplyPageLookup(anchorLookup);

            var embeddedFontResources = FontResourceWriter.WriteEmbeddedFonts(writer, embeddedFonts);
            string embeddedFontRes = string.Join(" ", embeddedFontResources.Select(kv => $"{kv.Key} {kv.Value} 0 R"));
            string fontRes = CombineFontResources(baseFontRes, embeddedFontRes);

            // Page object references are reserved before /Pages is written. This is deliberately
            // independent of the number of annotation, image, or font objects emitted later.
            int pagesObjId = writer.ReserveObject();
            var pageObjectIds = Enumerable.Range(0, pageCount)
                .Select(_ => writer.ReserveObject())
                .ToArray();

            // /Pages ---------------------------------------------------------------------------------
            writer.BeginReservedObject(pagesObjId);

            string kids = string.Join(" ", pageObjectIds.Select(id => $"{id} 0 R"));
            writer.WriteLine("<<");
            writer.WriteLine(" /Type /Pages");
            writer.WriteLine($" /Kids [{kids}]");
            writer.WriteLine($" /Count {pageCount}");
            writer.WriteLine(">>");
            writer.EndObject();

            // Pages ----------------------------------------------------------------------------------
            for (int i = 0; i < pageCount; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var page = laidOut.Pages[i];

                // WRITE PASS: only the current page's stream is retained at any time.
                var pageImgMap = BuildPageImageMap(page, imageResourceMap);
                var (rawContent, _, _) = BuildContentStream(
                    laidOut,
                    page,
                    i + 1,
                    pageCount,
                    renderContext,
                    fontObjId,
                    pageImgMap,
                    nowUtc,
                    cancellationToken);
                metrics.PageContentStreamsWritten++;
                metrics.MaximumRetainedPageContentStreams = Math.Max(metrics.MaximumRetainedPageContentStreams, 1);

                // a) content stream
                int contentId = writer.BeginObject();
                if (outputOptions.CompressContentStreams && rawContent.Length > 0)
                {
                    var compressed = PdfCompression.Flate(rawContent, outputOptions.ContentCompressionLevel);
                    writer.WriteStream(compressed, ("/Filter", "/FlateDecode"));
                }
                else
                {
                    writer.WriteInlineStream(rawContent);
                }
                writer.EndObject();

                // b) annotations (external + internal anchors)
                List<int> annotIds = new();
                if (pageAnnotations[i].Count > 0)
                {
                    int PageRefByAnchor(string anchor)
                    {
                        if (!anchorLookup.TryGetValue(anchor, out var info)) return 0;
                        return pageObjectIds[info.pageIndex];
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
                int pageId = pageObjectIds[i];
                writer.BeginReservedObject(pageId);
                writer.WriteLine("<<");
                writer.WriteLine(" /Type /Page");
                writer.WriteLine($" /Parent {pagesObjId} 0 R");
                writer.WriteLine($" /MediaBox [0 0 {N(page.Width)} {N(page.Height)}]");
                writer.WriteLine($" /Contents {contentId} 0 R");

                var resSb = new StringBuilder();
                resSb.Append(" /Resources <<");
                if (!string.IsNullOrWhiteSpace(fontRes)) resSb.Append($" /Font << {fontRes} >>");
                if (!string.IsNullOrWhiteSpace(xobjRes)) resSb.Append($" /XObject << {xobjRes} >>");
                if (!string.IsNullOrWhiteSpace(gsRes)) resSb.Append($" /ExtGState << {gsRes} >>");
                resSb.Append(" /ProcSet [/PDF /Text /ImageB /ImageC /ImageI] >>");
                writer.WriteLine(resSb.ToString());

                if (annotIds.Count > 0)
                    writer.WriteLine($" /Annots [{string.Join(" ", annotIds.Select(id => $"{id} 0 R"))}]");

                writer.WriteLine(">>");
                writer.EndObject();

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
                        PageObjId = pageObjectIds[i],
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
            var metadata = laidOut.Metadata ?? new DocumentMetadata();
            int infoId = writer.BeginObject();
            writer.WriteLine("<<");
            if (!string.IsNullOrWhiteSpace(doc.Title))
                writer.WriteLine($"/Title ({Escape(doc.Title!)})");
            if (!string.IsNullOrWhiteSpace(metadata.Author))
                writer.WriteLine($"/Author ({Escape(metadata.Author!)})");
            if (!string.IsNullOrWhiteSpace(metadata.Subject))
                writer.WriteLine($"/Subject ({Escape(metadata.Subject!)})");
            if (!string.IsNullOrWhiteSpace(metadata.Keywords))
                writer.WriteLine($"/Keywords ({Escape(metadata.Keywords!)})");

            string creator = !string.IsNullOrWhiteSpace(metadata.Creator) ? metadata.Creator! : "PdfBuilder";
            string producer = !string.IsNullOrWhiteSpace(metadata.Producer) ? metadata.Producer! : "PdfBuilder";
            writer.WriteLine($"/Creator ({Escape(creator)})");
            writer.WriteLine($"/Producer ({Escape(producer)})");

            DateTime creationDate = metadata.CreatedUtc ?? nowUtc;
            writer.WriteLine($"/CreationDate {FormatPdfDate(creationDate)}");
            if (metadata.ModifiedUtc.HasValue)
                writer.WriteLine($"/ModDate {FormatPdfDate(metadata.ModifiedUtc.Value)}");
            else if (metadata.CreatedUtc.HasValue)
                writer.WriteLine($"/ModDate {FormatPdfDate(creationDate)}");

            writer.WriteLine(">>");
            writer.EndObject();

            // XRef & trailer ------------------------------------------------------------------------
            writer.WriteXRefAndTrailer(catalogId, infoId);

            laidOut.ProfilerSession.Emit(laidOut.LayoutOptions.Profiler);
        }

        // -----------------------------------------------------------------------------------------
        // Helpers
        // -----------------------------------------------------------------------------------------

        private static Dictionary<ImageElement, (int imageObjId, string? gsName)> PreRegisterImages(
            PdfDocument doc,
            PdfResourceManager resources,
            PdfStreamWriter writer,
            CancellationToken cancellationToken)
        {
            var map = new Dictionary<ImageElement, (int imageObjId, string? gsName)>(ReferenceEqualityComparer.Instance);

            foreach (var img in doc.Pages.SelectMany(EnumerateAllElements).OfType<ImageElement>())
            {
                cancellationToken.ThrowIfCancellationRequested();
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
            foreach (var img in EnumerateAllElements(page).OfType<ImageElement>())
            {
                if (globalMap.TryGetValue(img, out var ids))
                    result[img] = ids;
            }
            return result;
        }

        private static string CombineFontResources(string baseEntries, string embeddedEntries)
        {
            bool hasBase = !string.IsNullOrWhiteSpace(baseEntries);
            bool hasEmbedded = !string.IsNullOrWhiteSpace(embeddedEntries);
            if (hasBase && hasEmbedded) return $"{baseEntries.Trim()} {embeddedEntries.Trim()}";
            if (hasBase) return baseEntries.Trim();
            if (hasEmbedded) return embeddedEntries.Trim();
            return string.Empty;
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

        private static void PreRegisterSolidRectOpacity(
            PdfDocument doc,
            PdfResourceManager resources,
            PdfStreamWriter writer)
        {
            foreach (var rectangle in doc.Pages.SelectMany(EnumerateAllElements).OfType<SolidRectElement>())
                rectangle.ExtGStateResourceName = rectangle.Opacity < 0.999f
                    ? resources.EnsureWatermarkExtGState(rectangle.Opacity, writer)
                    : null;
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
            PdfRenderContext context,
            Dictionary<string, int> fontObjId,
            Dictionary<ImageElement, (int imageObjId, string? gsName)> pageImageMap,
            DateTime nowUtc,
            CancellationToken cancellationToken)
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
                    MasterRenderer.AppendWatermark(sb, page, effectiveMaster.Watermark, context, aboveContent: false);
            }

            RenderElements(page.HeaderElements, sb, page, context, pageImageMap, annotations, anchorsOnPage, cancellationToken);
            RenderElements(page.Elements, sb, page, context, pageImageMap, annotations, anchorsOnPage, cancellationToken);
            RenderElements(page.FooterElements, sb, page, context, pageImageMap, annotations, anchorsOnPage, cancellationToken);

            if (effectiveHeaderFooter != null)
                HeaderFooterRenderer.Append(sb, doc, page, effectiveHeaderFooter, context, pageIndex1, pageCount, nowUtc);

            if (effectiveMaster?.Watermark != null && effectiveMaster.Watermark.Layer == WatermarkLayer.AboveContent)
                MasterRenderer.AppendWatermark(sb, page, effectiveMaster.Watermark, context, aboveContent: true);

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

        private static void AppendSolidRect(StringBuilder sb, SolidRectElement rect)
        {
            bool hasFill = !string.IsNullOrWhiteSpace(rect.FillColor);
            bool hasStroke = !string.IsNullOrWhiteSpace(rect.StrokeColor) && rect.StrokeWidth > 0.001f;
            if (!hasFill && !hasStroke)
                return;

            float width = Math.Max(0f, rect.Width);
            float height = Math.Max(0f, rect.Height);
            if (width <= 0f || height <= 0f)
                return;

            sb.Append("q ");
            if (!string.IsNullOrWhiteSpace(rect.ExtGStateResourceName))
                sb.Append($"{rect.ExtGStateResourceName} gs ");

            if (hasFill)
            {
                string fillRgb = TryRgb(rect.FillColor!) ?? "0 0 0";
                sb.Append($"{fillRgb} rg ");
            }

            if (hasStroke)
            {
                string strokeRgb = TryRgb(rect.StrokeColor!) ?? "0 0 0";
                sb.Append($"{strokeRgb} RG {N(rect.StrokeWidth)} w ");
                if (rect.DashPattern != null && rect.DashPattern.Length > 0)
                {
                    sb.Append("[");
                    for (int i = 0; i < rect.DashPattern.Length; i++)
                    {
                        if (i > 0) sb.Append(' ');
                        sb.Append(N(rect.DashPattern[i]));
                    }
                    sb.Append("] 0 d ");
                }
            }

            if (rect.CornerRadius > 0.001f)
            {
                float radius = Math.Min(rect.CornerRadius, Math.Min(width, height) / 2f);
                float curve = radius * 0.55228475f;
                sb.Append($"{N(rect.X + radius)} {N(rect.Y)} m ");
                sb.Append($"{N(rect.X + width - radius)} {N(rect.Y)} l {N(rect.X + width - radius + curve)} {N(rect.Y)} {N(rect.X + width)} {N(rect.Y + radius - curve)} {N(rect.X + width)} {N(rect.Y + radius)} c ");
                sb.Append($"{N(rect.X + width)} {N(rect.Y + height - radius)} l {N(rect.X + width)} {N(rect.Y + height - radius + curve)} {N(rect.X + width - radius + curve)} {N(rect.Y + height)} {N(rect.X + width - radius)} {N(rect.Y + height)} c ");
                sb.Append($"{N(rect.X + radius)} {N(rect.Y + height)} l {N(rect.X + radius - curve)} {N(rect.Y + height)} {N(rect.X)} {N(rect.Y + height - radius + curve)} {N(rect.X)} {N(rect.Y + height - radius)} c ");
                sb.Append($"{N(rect.X)} {N(rect.Y + radius)} l {N(rect.X)} {N(rect.Y + radius - curve)} {N(rect.X + radius - curve)} {N(rect.Y)} {N(rect.X + radius)} {N(rect.Y)} c h ");
            }
            else
                sb.Append($"{N(rect.X)} {N(rect.Y)} {N(width)} {N(height)} re ");
            if (hasFill && hasStroke)
                sb.Append("B ");
            else if (hasFill)
                sb.Append("f ");
            else
                sb.Append("S ");
            sb.Append("Q\n");
        }

        private static IEnumerable<PdfElement> EnumerateAllElements(PdfPage page)
        {
            foreach (var element in page.HeaderElements)
                yield return element;
            foreach (var element in page.Elements)
                yield return element;
            foreach (var element in page.FooterElements)
                yield return element;
        }

        private static void RenderElements(
            IEnumerable<PdfElement> elements,
            StringBuilder sb,
            PdfPage page,
            PdfRenderContext context,
            Dictionary<ImageElement, (int imageObjId, string? gsName)> pageImageMap,
            List<AnnotationWriter.LinkAnnot> annotations,
            List<(AnchorElement anchor, float xPdf, float yPdf)> anchorsOnPage,
            CancellationToken cancellationToken)
        {
            foreach (var element in elements)
            {
                cancellationToken.ThrowIfCancellationRequested();
                switch (element)
                {
                    case TextElement text:
                        TextRenderer.Append(sb, text, page.Height, context);
                        break;

                    case TableElement table:
                        TableRenderer.Append(sb, table, context);
                        break;

                    case ImageElement image:
                        if (pageImageMap.TryGetValue(image, out var ids))
                            ImageRenderer.Append(sb, image, page.Height, ids.imageObjId, ids.gsName);
                        break;

                    case CanvasElement canvas:
                        CanvasRenderer.Append(sb, canvas, page.Height);
                        break;

                    case RichTextElement richText:
                        {
                            var linkRects = new List<RichTextRenderer.LinkRect>();
                            _ = RichTextRenderer.Append(sb, richText, page.Height, context, linkRects);
                            annotations.AddRange(ConvertLinkRects(linkRects));
                            break;
                        }

                    case ListElement list:
                        {
                            var linkRects = new List<RichTextRenderer.LinkRect>();
                            ListRenderer.Append(sb, list, page.Height, context, linkRects);
                            annotations.AddRange(ConvertLinkRects(linkRects));
                            break;
                        }

                    case ChartElement chart:
                        ChartRenderer.Append(sb, chart, context);
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

                    case SolidRectElement solidRect:
                        AppendSolidRect(sb, solidRect);
                        break;
                }
            }
        }

        private static HashSet<string> CollectBaseFonts(PdfDocument doc)
        {
            var fonts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            void AddFont(string? family, bool bold = false, bool italic = false)
            {
                var base14 = FontManager.MapToBase14(FontManager.NormalizeFontKey(family, bold, italic));
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

                foreach (var element in EnumerateAllElements(page))
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

        private static string FormatPdfDate(DateTime value)
        {
            var utc = value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();
            return $"(D:{utc:yyyyMMddHHmmss}Z)";
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





