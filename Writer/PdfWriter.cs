using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using PdfBuilder.Document;
using PdfBuilder.Document.Layout;
using PdfBuilder.Elements;
using PdfBuilder.Fonts;
using PdfBuilder.Models;
using PdfBuilder.Writer.Fonts;
using PdfBuilder.Writer.Tagging;

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

        /// <summary>Gets read-only diagnostics for the most recent generation performed by this writer.</summary>
        public PdfGenerationMetrics? LastGenerationMetrics { get; private set; }

        /// <summary>Generates a PDF into a newly allocated byte array.</summary>
        /// <param name="doc">The document to generate.</param>
        public byte[] GenerateBytes(PdfDocument doc) => GenerateBytes(doc, CancellationToken.None);

        /// <summary>Generates a PDF into a newly allocated byte array.</summary>
        /// <param name="doc">The document to generate.</param>
        /// <param name="cancellationToken">Cancels layout planning or page writing.</param>
        public byte[] GenerateBytes(PdfDocument doc, CancellationToken cancellationToken)
        {
            using var ms = new MemoryStream();
            PrepareAndWriteDocument(doc ?? throw new ArgumentNullException(nameof(doc)), ms, DateTimeOffset.Now, cancellationToken);
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

            PrepareAndWriteDocument(doc, destination, DateTimeOffset.Now, cancellationToken);
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
            PrepareAndWriteDocument(doc ?? throw new ArgumentNullException(nameof(doc)), fileStream, DateTimeOffset.Now, cancellationToken);
        }

        public IReadOnlyList<PdfPreviewPage> GeneratePreviewImages(PdfDocument doc, int dpi = 144)
        {
            if (doc == null) throw new ArgumentNullException(nameof(doc));
            lock (doc.GenerationSyncRoot)
            {
                HeaderFooterLayoutComposer.Prepare(doc, ResolveCreationDate(doc, DateTimeOffset.Now).UtcDateTime, CancellationToken.None);
                return new PdfPreviewGenerator().Generate(doc, dpi);
            }
        }

        /// <summary>Generates selected preview images from the already-resolved document layout.</summary>
        public IReadOnlyList<PdfPreviewPage> GeneratePreviewImages(
            PdfDocument doc,
            int dpi,
            IEnumerable<int>? pageNumbers,
            CancellationToken cancellationToken)
        {
            if (doc == null) throw new ArgumentNullException(nameof(doc));
            lock (doc.GenerationSyncRoot)
            {
                HeaderFooterLayoutComposer.Prepare(doc, ResolveCreationDate(doc, DateTimeOffset.Now).UtcDateTime, cancellationToken);
                return new PdfPreviewGenerator().Generate(doc, dpi, pageNumbers, cancellationToken);
            }
        }

        private void PrepareAndWriteDocument(PdfDocument doc, Stream destination, DateTimeOffset now, CancellationToken cancellationToken)
        {
            lock (doc.GenerationSyncRoot)
            {
                var stopwatch = Stopwatch.StartNew();
                cancellationToken.ThrowIfCancellationRequested();
                HeaderFooterLayoutComposer.Prepare(doc, ResolveCreationDate(doc, now).UtcDateTime, cancellationToken);
                using var tracking = new PdfWriteTrackingStream(destination, doc.RenderLimits.MaximumOutputBytes);
                WriteResolvedDocument(doc, tracking, now, cancellationToken);
                tracking.Flush();
                stopwatch.Stop();
                if (LastGenerationMetrics != null)
                {
                    LastGenerationMetrics.OutputBytes = tracking.BytesWritten;
                    LastGenerationMetrics.Elapsed = stopwatch.Elapsed;
                    doc.LastGenerationMetrics = LastGenerationMetrics;
                }
            }
        }

        private static DateTimeOffset ResolveCreationDate(PdfDocument doc, DateTimeOffset now)
            => doc.GenerationOptions.CreationTime
                ?? doc.Metadata.CreatedUtc
                ?? (doc.GenerationOptions.Deterministic ? DateTimeOffset.UnixEpoch : now);

        private void WriteResolvedDocument(PdfDocument doc, Stream destination, DateTimeOffset now, CancellationToken cancellationToken)
        {
            using var fontSnapshotScope = FontCatalog.EnterSnapshot(doc.FontSnapshot);
            cancellationToken.ThrowIfCancellationRequested();
            if (doc.Pages.Count == 0) throw new InvalidOperationException("Document has no pages.");

            // Flowing content, including tables, is resolved by ColumnBuilder before serialization.
            // The writer must consume that resolved document without cloning or repaginating it.
            var laidOut = doc;
            int pageCount = laidOut.Pages.Count;
            var metrics = new PdfGenerationMetrics { PagesPlanned = pageCount };
            doc.TableLayoutDiagnostics.CopyTo(metrics);
            LastGenerationMetrics = metrics;

            var generationOptions = laidOut.GenerationOptions;
            var metadata = laidOut.Metadata ?? new DocumentMetadata();
            generationOptions.Validate();
            metadata.Validate(doc.RenderLimits.MaximumMetadataCharacters, doc.RenderLimits.MaximumXmpBytes);
            if (laidOut.Tagging.Enabled && string.IsNullOrWhiteSpace(metadata.Language))
                throw new InvalidOperationException("Tagged PDF output requires an explicit BCP 47 document language. Configure document.Tagged(tag => tag.Language(...)).");
            var outputOptions = laidOut.OutputOptions ?? new PdfOutputOptions();
            outputOptions.Validate();
            DateTimeOffset creationDate = generationOptions.CreationTime
                ?? metadata.CreatedUtc
                ?? (generationOptions.Deterministic ? DateTimeOffset.UnixEpoch : now);
            DateTimeOffset modificationDate = generationOptions.ModificationTime
                ?? metadata.ModifiedUtc
                ?? creationDate;

            using var writer = new PdfStreamWriter(destination);
            writer.WriteHeader(outputOptions.VersionToken);

            // Fonts (base-14 Type1) ---------------------------------------------------------------
            var baseFonts = CollectBaseFonts(laidOut);
            var fontObjId = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var baseFont in baseFonts.OrderBy(name => name, StringComparer.Ordinal))
            {
                int id = writer.BeginObject();
                // Base-14 Type1 font with WinAnsiEncoding so 8-bit CP1252 text renders correctly
                writer.WriteLine($"<< /Type /Font /Subtype /Type1 /BaseFont {PdfNameEncoder.Encode(baseFont)} /Encoding /WinAnsiEncoding >>");
                writer.EndObject();
                fontObjId[baseFont] = id;
            }
            metrics.BaseFontResources = fontObjId.Count;
            string baseFontRes = string.Join(" ", fontObjId.Select(kv => $"/F{kv.Value} {kv.Value} 0 R"));

            var embeddedFonts = new EmbeddedFontRegistry();
            var renderContext = new PdfRenderContext(fontObjId, embeddedFonts, laidOut.Pagination);

            // Images / ExtGState -----------------------------------------------------------------
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
            var pageTaggedContent = Enumerable.Range(0, pageCount)
                .Select(_ => (IReadOnlyList<TaggedContentItem>)Array.Empty<TaggedContentItem>())
                .ToList();
            var (anchorLookup, pageAnchors) = CollectNavigationAnchors(laidOut);
            laidOut.Pagination?.ApplyPageLookup(anchorLookup);
            laidOut.NavigationDiagnostics.Clear();

            for (int i = 0; i < pageCount; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var page = laidOut.Pages[i];
                var pageImgMap = BuildPageImageMap(page, imageResourceMap);

                var (contentBytes, annots, _, _) = BuildContentStream(
                    laidOut,
                    page,
                    i + 1,
                    pageCount,
                    renderContext,
                    fontObjId,
                    pageImgMap,
                    creationDate.UtcDateTime,
                    cancellationToken);

                pageAnnotations.Add(annots);
            }

            RecordBrokenNavigationDiagnostics(laidOut, pageAnnotations, anchorLookup);
            if (laidOut.Tagging.Enabled)
            {
                int nextStructParent = pageCount;
                for (int pageIndex = 0; pageIndex < pageAnnotations.Count; pageIndex++)
                {
                    foreach (AnnotationWriter.LinkAnnot annotation in pageAnnotations[pageIndex])
                    {
                        annotation.PageIndex = pageIndex;
                        if (annotation.SemanticNodeId.HasValue)
                            annotation.StructParentKey = nextStructParent++;
                    }
                }
            }

            var embeddedFontResources = FontResourceWriter.WriteEmbeddedFonts(writer, embeddedFonts);
            metrics.EmbeddedFontResources = embeddedFontResources.Count;
            metrics.ImageReferences = imageResourceMap.Count;
            metrics.UniqueImageResources = resources.UniqueImageCount;
            metrics.ExtGStateResources = resources.ExtGStateCount;
            string embeddedFontRes = string.Join(" ", embeddedFontResources
                .OrderBy(entry => entry.Key, StringComparer.Ordinal)
                .Select(kv => $"{kv.Key} {kv.Value} 0 R"));
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
                var (rawContent, _, _, taggedContent) = BuildContentStream(
                    laidOut,
                    page,
                    i + 1,
                    pageCount,
                    renderContext,
                    fontObjId,
                    pageImgMap,
                    creationDate.UtcDateTime,
                    cancellationToken);
                pageTaggedContent[i] = taggedContent;
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
                if (laidOut.Tagging.Enabled)
                {
                    writer.WriteLine($" /StructParents {i}");
                    writer.WriteLine(" /Tabs /S");
                }

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

            TaggedPdfStructureResult? taggedStructure = laidOut.Tagging.Enabled
                ? TaggedPdfStructureWriter.Write(writer, laidOut, pageObjectIds, pageTaggedContent, pageAnnotations)
                : null;

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

            int xmpMetadataId = 0;
            if (!string.IsNullOrWhiteSpace(metadata.CustomXmp))
            {
                xmpMetadataId = writer.BeginObject();
                writer.WriteStream(
                    Encoding.UTF8.GetBytes(metadata.CustomXmp!),
                    ("Type", "/Metadata"),
                    ("Subtype", "/XML"));
                writer.EndObject();
            }

            int outputIntentId = 0;
            if (laidOut.OutputIntent is { } outputIntent)
            {
                int profileId = writer.BeginObject();
                string alternate = outputIntent.Components switch
                {
                    1 => "/DeviceGray",
                    3 => "/DeviceRGB",
                    4 => "/DeviceCMYK",
                    _ => throw new InvalidOperationException("The output-intent ICC profile has an unsupported colour-component count.")
                };
                writer.WriteStream(
                    outputIntent.GetProfileBytes(),
                    ("N", outputIntent.Components.ToString(CultureInfo.InvariantCulture)),
                    ("Alternate", alternate));
                writer.EndObject();

                outputIntentId = writer.BeginObject();
                writer.WriteLine("<< /Type /OutputIntent /S /GTS_PDFA1");
                writer.WriteLine($" /OutputConditionIdentifier {PdfStringEncoder.Encode(outputIntent.Identifier)}");
                if (!string.IsNullOrWhiteSpace(outputIntent.Info))
                    writer.WriteLine($" /Info {PdfStringEncoder.Encode(outputIntent.Info!)}");
                writer.WriteLine($" /RegistryName {PdfStringEncoder.Encode(outputIntent.RegistryName)}");
                writer.WriteLine($" /DestOutputProfile {profileId} 0 R");
                writer.WriteLine(">>");
                writer.EndObject();
            }

            // Catalog -------------------------------------------------------------------------------
            int catalogId = writer.BeginObject();
            writer.WriteLine("<<");
            writer.WriteLine($" /Type /Catalog /Pages {pagesObjId} 0 R");
            if (outlinesId != 0)
                writer.WriteLine($" /Outlines {outlinesId} 0 R");
            if (!string.IsNullOrWhiteSpace(metadata.Language))
                writer.WriteLine($" /Lang {PdfStringEncoder.Encode(metadata.Language!)}");
            if (xmpMetadataId != 0)
                writer.WriteLine($" /Metadata {xmpMetadataId} 0 R");
            if (outputIntentId != 0)
                writer.WriteLine($" /OutputIntents [{outputIntentId} 0 R]");
            if (taggedStructure != null)
            {
                writer.WriteLine($" /StructTreeRoot {taggedStructure.StructureTreeRootId} 0 R");
                writer.WriteLine(" /MarkInfo << /Marked true >>");
                writer.WriteLine(" /ViewerPreferences << /DisplayDocTitle true >>");
            }
            writer.WriteLine(">>");
            writer.EndObject();

            // Info ----------------------------------------------------------------------------------
            int infoId = writer.BeginObject();
            writer.WriteLine("<<");
            string? documentTitle = !string.IsNullOrWhiteSpace(doc.Title) ? doc.Title : metadata.Title;
            if (!string.IsNullOrWhiteSpace(documentTitle))
                writer.WriteLine($"/Title {PdfStringEncoder.Encode(documentTitle!)}");
            if (!string.IsNullOrWhiteSpace(metadata.Author))
                writer.WriteLine($"/Author {PdfStringEncoder.Encode(metadata.Author!)}");
            if (!string.IsNullOrWhiteSpace(metadata.Subject))
                writer.WriteLine($"/Subject {PdfStringEncoder.Encode(metadata.Subject!)}");
            if (!string.IsNullOrWhiteSpace(metadata.Keywords))
                writer.WriteLine($"/Keywords {PdfStringEncoder.Encode(metadata.Keywords!)}");

            string creator = !string.IsNullOrWhiteSpace(metadata.Creator) ? metadata.Creator! : "PdfBuilder";
            string producer = !string.IsNullOrWhiteSpace(metadata.Producer) ? metadata.Producer! : "PdfBuilder";
            writer.WriteLine($"/Creator {PdfStringEncoder.Encode(creator)}");
            writer.WriteLine($"/Producer {PdfStringEncoder.Encode(producer)}");
            writer.WriteLine($"/CreationDate {PdfDateEncoder.Encode(creationDate)}");
            writer.WriteLine($"/ModDate {PdfDateEncoder.Encode(modificationDate)}");

            writer.WriteLine(">>");
            writer.EndObject();

            // XRef & trailer ------------------------------------------------------------------------
            writer.WriteXRefAndTrailer(catalogId, infoId, BuildDocumentId(laidOut, generationOptions));

            metrics.ObjectsWritten = writer.ObjectCount;

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
                         List<(AnchorElement anchor, float xPdf, float yPdf)> anchors,
                         IReadOnlyList<TaggedContentItem> taggedContent) BuildContentStream(
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
            TaggedContentCollector? tagging = doc.Tagging.Enabled
                ? new TaggedContentCollector(pageIndex1 - 1, doc.SemanticRegistry)
                : null;

            var effectiveMaster = page.MasterOverride ?? doc.Master;
            var effectiveHeaderFooter = page.HeaderFooterOverride ?? doc.HeaderFooter;
            var pageContext = PageContextFactory.Create(page, pageIndex1, pageCount, effectiveHeaderFooter);

            if (effectiveMaster != null)
            {
                AppendArtifact(sb, () => MasterRenderer.AppendBackground(sb, page, effectiveMaster), doc.Tagging.Enabled);
                if (effectiveMaster.Watermark != null && effectiveMaster.Watermark.Layer == WatermarkLayer.BehindContent)
                    AppendArtifact(sb, () => MasterRenderer.AppendWatermark(sb, page, effectiveMaster.Watermark, context, aboveContent: false), doc.Tagging.Enabled);
            }

            RenderElements(page.HeaderElements, sb, page, context, pageContext, pageImageMap, annotations, anchorsOnPage, cancellationToken, tagging);
            RenderElements(page.Elements, sb, page, context, pageContext, pageImageMap, annotations, anchorsOnPage, cancellationToken, tagging);
            RenderElements(page.FooterElements, sb, page, context, pageContext, pageImageMap, annotations, anchorsOnPage, cancellationToken, tagging);

            if (effectiveHeaderFooter != null)
                AppendArtifact(sb, () => HeaderFooterRenderer.Append(sb, doc, page, effectiveHeaderFooter, context, pageIndex1, pageCount, nowUtc), doc.Tagging.Enabled);

            if (effectiveMaster?.Watermark != null && effectiveMaster.Watermark.Layer == WatermarkLayer.AboveContent)
                AppendArtifact(sb, () => MasterRenderer.AppendWatermark(sb, page, effectiveMaster.Watermark, context, aboveContent: true), doc.Tagging.Enabled);

            IReadOnlyList<TaggedContentItem> taggedItems = tagging?.Items is { } items
                ? items
                : Array.Empty<TaggedContentItem>();
            return (Encoding.ASCII.GetBytes(sb.ToString()), annotations, anchorsOnPage, taggedItems);
        }

        private static void AppendArtifact(StringBuilder content, Action render, bool taggingEnabled)
        {
            int start = content.Length;
            if (taggingEnabled) content.Append("/Artifact BMC\n");
            render();
            if (taggingEnabled && content.Length > start + "/Artifact BMC\n".Length)
                content.Append("EMC\n");
            else if (taggingEnabled)
                content.Length = start;
        }

        private static IEnumerable<AnnotationWriter.LinkAnnot> ConvertLinkRects(
            IEnumerable<RichTextRenderer.LinkRect> rects,
            int? semanticNodeId)
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
                    Url = rect.Url == null ? null : NavigationUriPolicy.ValidateExternal(rect.Url),
                    Anchor = rect.Anchor,
                    SemanticNodeId = semanticNodeId
                };
            }
        }

        private static AnnotationWriter.LinkAnnot ConvertLinkRect(LinkRectElement linkRect, int? semanticNodeId)
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
                Url = linkRect.Url == null ? null : NavigationUriPolicy.ValidateExternal(linkRect.Url),
                Anchor = linkRect.Anchor,
                SemanticNodeId = semanticNodeId
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
                foreach (PdfElement nested in EnumerateElement(element)) yield return nested;
            foreach (var element in page.Elements)
                foreach (PdfElement nested in EnumerateElement(element)) yield return nested;
            foreach (var element in page.FooterElements)
                foreach (PdfElement nested in EnumerateElement(element)) yield return nested;

            static IEnumerable<PdfElement> EnumerateElement(PdfElement element)
            {
                yield return element;
                if (element is not ClipGroupElement group) yield break;
                foreach (PdfElement child in group.Children)
                    foreach (PdfElement nested in EnumerateElement(child))
                        yield return nested;
            }
        }

        private static (
            Dictionary<string, (int pageIndex, float xPdf, float yPdf)> Lookup,
            List<List<(AnchorElement anchor, float xPdf, float yPdf)>> PageAnchors)
            CollectNavigationAnchors(PdfDocument document)
        {
            var lookup = new Dictionary<string, (int pageIndex, float xPdf, float yPdf)>(StringComparer.Ordinal);
            var pageAnchors = new List<List<(AnchorElement anchor, float xPdf, float yPdf)>>(document.Pages.Count);

            for (int pageIndex = 0; pageIndex < document.Pages.Count; pageIndex++)
            {
                var anchors = new List<(AnchorElement anchor, float xPdf, float yPdf)>();
                foreach (AnchorElement anchor in EnumerateAllElements(document.Pages[pageIndex]).OfType<AnchorElement>())
                {
                    if (string.IsNullOrWhiteSpace(anchor.Id))
                        throw new PdfNavigationException("A rendered navigation anchor has an empty id.");
                    if (!lookup.TryAdd(anchor.Id, (pageIndex, anchor.X, anchor.Y)))
                        throw new PdfNavigationException($"Duplicate rendered navigation anchor id '{anchor.Id}'. Anchor ids must be unique within a document.");
                    anchors.Add((anchor, anchor.X, anchor.Y));
                }
                pageAnchors.Add(anchors);
            }

            return (lookup, pageAnchors);
        }

        private static void RecordBrokenNavigationDiagnostics(
            PdfDocument document,
            IEnumerable<IEnumerable<AnnotationWriter.LinkAnnot>> pageAnnotations,
            IReadOnlyDictionary<string, (int pageIndex, float xPdf, float yPdf)> anchorLookup)
        {
            var missing = new HashSet<string>(StringComparer.Ordinal);
            foreach (AnnotationWriter.LinkAnnot annotation in pageAnnotations.SelectMany(page => page))
            {
                if (!string.IsNullOrWhiteSpace(annotation.Anchor) && !anchorLookup.ContainsKey(annotation.Anchor))
                    missing.Add(annotation.Anchor);
            }

            foreach (TextElement reference in document.Pages
                .SelectMany(EnumerateAllElements)
                .OfType<TextElement>()
                .Where(element => !string.IsNullOrWhiteSpace(element.PageReferenceAnchorId)))
            {
                if (!anchorLookup.ContainsKey(reference.PageReferenceAnchorId!))
                    missing.Add(reference.PageReferenceAnchorId!);
            }

            foreach (string target in missing.OrderBy(value => value, StringComparer.Ordinal))
            {
                document.NavigationDiagnostics.Add(
                    "PDFNAV001",
                    $"Internal navigation target '{target}' was not found. The link was omitted and page references retain their pending text.",
                    target);
            }
        }

        private static void RenderElements(
            IEnumerable<PdfElement> elements,
            StringBuilder sb,
            PdfPage page,
            PdfRenderContext context,
            PageContext pageContext,
            Dictionary<ImageElement, (int imageObjId, string? gsName)> pageImageMap,
            List<AnnotationWriter.LinkAnnot> annotations,
            List<(AnchorElement anchor, float xPdf, float yPdf)> anchorsOnPage,
            CancellationToken cancellationToken,
            TaggedContentCollector? tagging,
            bool taggingSuppressed = false,
            int? inheritedSemanticNodeId = null)
        {
            foreach (var element in elements)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int? effectiveSemanticNodeId = element.SemanticNodeId ?? inheritedSemanticNodeId;
                bool tagStarted = tagging?.Begin(element, effectiveSemanticNodeId, sb, taggingSuppressed) == true;
                try
                {
                    switch (element)
                    {
                        case TextElement text:
                            TextRenderer.Append(sb, text, page.Height, context, pageContext);
                            break;

                        case TableSegmentElement tableSegment:
                            TableRenderer.Append(sb, tableSegment, context);
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
                                annotations.AddRange(ConvertLinkRects(linkRects, effectiveSemanticNodeId));
                                break;
                            }

                        case ListElement list:
                            {
                                var linkRects = new List<RichTextRenderer.LinkRect>();
                                ListRenderer.Append(sb, list, page.Height, context, linkRects);
                                annotations.AddRange(ConvertLinkRects(linkRects, effectiveSemanticNodeId));
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
                            annotations.Add(ConvertLinkRect(linkRect, effectiveSemanticNodeId));
                            break;

                        case SolidRectElement solidRect:
                            AppendSolidRect(sb, solidRect);
                            break;

                        case DebugRectangleElement debugRectangle:
                            AppendDebugRectangle(sb, debugRectangle);
                            break;

                        case ClipGroupElement clipGroup:
                            sb.Append("q ");
                            sb.Append($"{N(clipGroup.X)} {N(clipGroup.Y)} {N(clipGroup.Width)} {N(clipGroup.Height)} re W n\n");
                            RenderElements(
                                clipGroup.Children,
                                sb,
                                page,
                                context,
                                pageContext,
                                pageImageMap,
                                annotations,
                                anchorsOnPage,
                                cancellationToken,
                                tagging,
                                taggingSuppressed || tagStarted,
                                effectiveSemanticNodeId);
                            sb.Append("Q\n");
                            break;
                    }
                }
                finally
                {
                    if (tagStarted)
                        TaggedContentCollector.End(sb);
                }
            }
        }

        private static void AppendDebugRectangle(StringBuilder sb, DebugRectangleElement rect)
        {
            float width = Math.Max(0f, rect.Width);
            float height = Math.Max(0f, rect.Height);
            if (width <= 0f || height <= 0f)
                return;

            string strokeRgb = TryRgb(rect.StrokeColor) ?? "1 0 0";
            sb.Append($"q {strokeRgb} RG {N(Math.Max(0.1f, rect.StrokeWidth))} w ");
            if (rect.DashPattern is { Length: > 0 })
            {
                sb.Append('[');
                for (int i = 0; i < rect.DashPattern.Length; i++)
                {
                    if (i > 0) sb.Append(' ');
                    sb.Append(N(rect.DashPattern[i]));
                }
                sb.Append("] 0 d ");
            }
            if (!string.IsNullOrWhiteSpace(rect.FillColor))
                sb.Append($"{TryRgb(rect.FillColor) ?? "1 0.9 0.9"} rg ");
            sb.Append($"{N(rect.X)} {N(rect.Y)} {N(width)} {N(height)} re ");
            sb.Append(string.IsNullOrWhiteSpace(rect.FillColor) ? "S Q\n" : "B Q\n");
        }

        private static HashSet<string> CollectBaseFonts(PdfDocument doc)
        {
            var fonts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            void AddFont(string? family, bool bold = false, bool italic = false)
            {
                if (!string.IsNullOrWhiteSpace(family))
                {
                    var style = new SkiaSharp.SKFontStyle(
                        bold ? SkiaSharp.SKFontStyleWeight.Bold : SkiaSharp.SKFontStyleWeight.Normal,
                        SkiaSharp.SKFontStyleWidth.Normal,
                        italic ? SkiaSharp.SKFontStyleSlant.Italic : SkiaSharp.SKFontStyleSlant.Upright);
                    if (FontCatalog.CurrentSnapshot.Resolve(family, style) != null)
                        return;
                }
                var base14 = FontManager.MapToBase14(FontManager.NormalizeFontKey(family, bold, italic));
                fonts.Add(base14);
            }

            void AddFallbacks(IEnumerable<string>? names)
            {
                if (names == null) return;
                foreach (var name in names)
                    AddFont(name);
            }

            void AddTableFonts(TableElement table, IEnumerable<TableRow> rows)
            {
                AddFont(table.DefaultFont);
                if (table.DefaultTextStyle != null)
                {
                    AddFont(table.DefaultTextStyle.FontFamily, table.DefaultTextStyle.Bold, table.DefaultTextStyle.Italic);
                    AddFallbacks(table.DefaultTextStyle.FallbackFonts);
                }

                foreach (TableColumnStyle column in table.ColumnStyles)
                    AddFont(column.Font);

                foreach (TableCell cell in rows.SelectMany(row => row.Cells))
                {
                    AddFont(cell.Font, cell.Bold, cell.Italic);
                    if (cell.TextStyle != null)
                    {
                        AddFont(cell.TextStyle.FontFamily, cell.TextStyle.Bold, cell.TextStyle.Italic);
                        AddFallbacks(cell.TextStyle.FallbackFonts);
                    }

                    foreach (PdfBuilder.Elements.Table.InlineRun inline in cell.TextRuns)
                    {
                        if (inline?.Style != null)
                        {
                            AddFont(inline.Style.FontFamily, inline.Style.Bold, inline.Style.Italic);
                            AddFallbacks(inline.Style.FallbackFonts);
                        }

                        AddFallbacks(inline?.FallbackFonts);
                    }
                }
            }

            // Document-level header/footer + master watermark fonts
            if (UsesLegacyHeaderFooterText(doc.HeaderFooter))
                AddFont(doc.HeaderFooter.FontFamily);
            if (!string.IsNullOrEmpty(doc.Master?.Watermark?.Text))
                AddFont(doc.Master.Watermark.FontFamily);

            foreach (var page in doc.Pages)
            {
                var pageHF = page.HeaderFooterOverride ?? doc.HeaderFooter;
                if (UsesLegacyHeaderFooterText(pageHF))
                    AddFont(pageHF.FontFamily);

                var pageMaster = page.MasterOverride ?? doc.Master;
                if (!string.IsNullOrEmpty(pageMaster?.Watermark?.Text))
                    AddFont(pageMaster.Watermark.FontFamily);

                foreach (var element in EnumerateAllElements(page))
                {
                    switch (element)
                    {
                        case TextElement text:
                            AddFont(text.FontFamily, text.Bold, text.Italic);
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

                        case TableSegmentElement tableSegment:
                            AddTableFonts(tableSegment.SourceTable, tableSegment.Rows.Select(row => row.Row));
                            break;

                        case TableElement table:
                            AddTableFonts(table, table.Rows);
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
                return fonts;

            return fonts;

            static bool UsesLegacyHeaderFooterText(HeaderFooterSpec? spec)
                => spec != null &&
                   (!string.IsNullOrEmpty(spec.HeaderTemplate) ||
                    !string.IsNullOrEmpty(spec.FooterTemplate) ||
                    !string.IsNullOrEmpty(spec.FirstPageHeaderTemplate) ||
                    !string.IsNullOrEmpty(spec.FirstPageFooterTemplate) ||
                    spec.HeaderLayout != null ||
                    spec.FooterLayout != null);

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

        private static string BuildDocumentId(PdfDocument document, PdfGenerationOptions options)
        {
            if (!string.IsNullOrWhiteSpace(options.DocumentIdentifier))
                return options.DocumentIdentifier.Trim().ToUpperInvariant();
            string seed = options.DocumentIdSeed ?? string.Join("|",
                document.Title ?? string.Empty,
                document.Metadata.Author ?? string.Empty,
                document.Metadata.Subject ?? string.Empty,
                document.Pages.Count.ToString(CultureInfo.InvariantCulture),
                string.Join(";", document.Pages.Select(page => $"{N(page.Width)}x{N(page.Height)}:{page.Elements.Count}")));
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(seed)));
        }

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





