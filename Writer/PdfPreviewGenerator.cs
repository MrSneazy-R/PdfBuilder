using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PdfBuilder.Document;
using PdfBuilder.Elements;
using PdfBuilder.Models;
using PdfBuilder.TextShaping;
using SkiaSharp;

namespace PdfBuilder.Writer
{
    public sealed class PdfPreviewPage
    {
        public PdfPreviewPage(int pageNumber, int pixelWidth, int pixelHeight, byte[] imageData)
        {
            if (pixelWidth <= 0) throw new ArgumentOutOfRangeException(nameof(pixelWidth));
            if (pixelHeight <= 0) throw new ArgumentOutOfRangeException(nameof(pixelHeight));
            PageNumber = pageNumber;
            Width = pixelWidth;
            Height = pixelHeight;
            ImageData = imageData ?? throw new ArgumentNullException(nameof(imageData));
        }

        public int PageNumber { get; }
        public int Width { get; }
        public int Height { get; }
        public byte[] ImageData { get; }

        public Stream AsStream() => new MemoryStream(ImageData, writable: false);
    }

    public sealed class PdfPreviewGenerator
    {
        public IReadOnlyList<PdfPreviewPage> Generate(PdfDocument document, int dpi = 144)
            => Generate(document, dpi, null, CancellationToken.None);

        /// <summary>Renders selected one-based pages from an already-resolved document layout.</summary>
        public IReadOnlyList<PdfPreviewPage> Generate(
            PdfDocument document,
            int dpi,
            IEnumerable<int>? pageNumbers,
            CancellationToken cancellationToken)
        {
            if (document == null) throw new ArgumentNullException(nameof(document));
            if (dpi <= 0) throw new ArgumentOutOfRangeException(nameof(dpi), "DPI must be positive.");

            // Preview uses the same already-resolved layout as PDF serialization.
            var laidOut = document;
            var requestedPages = pageNumbers?.Distinct().OrderBy(number => number).ToArray()
                ?? Enumerable.Range(1, laidOut.Pages.Count).ToArray();
            if (requestedPages.Any(number => number < 1 || number > laidOut.Pages.Count))
                throw new ArgumentOutOfRangeException(nameof(pageNumbers), "Page numbers are one-based and must exist in the document.");

            var pages = new List<PdfPreviewPage>(requestedPages.Length);

            foreach (var pageNumber in requestedPages)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var page = laidOut.Pages[pageNumber - 1];
                pages.Add(RenderPage(laidOut, page, pageNumber, dpi));
            }

            return pages;
        }

        private PdfPreviewPage RenderPage(PdfDocument doc, PdfPage page, int pageNumber, int dpi)
        {
            float scale = dpi / 72f;
            int pixelWidth = Math.Max(1, (int)Math.Ceiling(page.Width * scale));
            int pixelHeight = Math.Max(1, (int)Math.Ceiling(page.Height * scale));

            var info = new SKImageInfo(pixelWidth, pixelHeight, SKColorType.Rgba8888, SKAlphaType.Premul);
            using var surface = SKSurface.Create(info);
            var canvas = surface.Canvas;
            canvas.Clear(ParseColor(page.BackgroundColor) ?? SKColors.White);

            canvas.Save();
            canvas.Scale(scale, scale);

            HeaderFooterSpec effectiveHeaderFooter = page.HeaderFooterOverride ?? doc.HeaderFooter;
            PageContext pageContext = PageContextFactory.Create(page, pageNumber, doc.Pages.Count, effectiveHeaderFooter);

            DrawPageBackground(canvas, doc, page);
            DrawElements(canvas, page.HeaderElements, page, pageContext, doc.Pagination);
            DrawElements(canvas, page.Elements, page, pageContext, doc.Pagination);
            DrawElements(canvas, page.FooterElements, page, pageContext, doc.Pagination);

            canvas.Restore();

            using var image = surface.Snapshot();
            using var data = image.Encode(SKEncodedImageFormat.Png, quality: 90);
            return new PdfPreviewPage(pageNumber, pixelWidth, pixelHeight, data.ToArray());
        }

        private void DrawPageBackground(SKCanvas canvas, PdfDocument doc, PdfPage page)
        {
            var master = page.MasterOverride ?? doc.Master;
            var color = ParseColor(master?.BackgroundColor);
            if (color == null)
                return;

            using var paint = new SKPaint { Color = color.Value, Style = SKPaintStyle.Fill, IsAntialias = true };
            canvas.DrawRect(new SKRect(0f, 0f, page.Width, page.Height), paint);
        }

        private void DrawElements(
            SKCanvas canvas,
            IEnumerable<PdfElement> elements,
            PdfPage page,
            PageContext pageContext,
            PaginationRegistry pagination)
        {
            if (elements == null)
                return;

            foreach (PdfElement element in elements.OrderBy(element => element is DebugRectangleElement ? 1 : 0))
            {
                switch (element)
                {
                    case TextElement text:
                        DrawTextElement(canvas, text, page.Height, pageContext, pagination);
                        break;
                    case ImageElement image:
                        DrawImageElement(canvas, image, page.Height);
                        break;
                    case ClipGroupElement group:
                        canvas.Save();
                        canvas.ClipRect(new SKRect(
                            group.X,
                            page.Height - group.Y - group.Height,
                            group.X + group.Width,
                            page.Height - group.Y));
                        DrawElements(canvas, group.Children, page, pageContext, pagination);
                        canvas.Restore();
                        break;
                    case DebugRectangleElement rectangle:
                        DrawDebugRectangle(canvas, rectangle, page.Height);
                        break;
                    default:
                        break;
                }
            }
        }

        private static void DrawDebugRectangle(SKCanvas canvas, DebugRectangleElement rectangle, float pageHeight)
        {
            if (rectangle.Width <= 0f || rectangle.Height <= 0f)
                return;

            var rect = new SKRect(
                rectangle.X,
                pageHeight - rectangle.Y - rectangle.Height,
                rectangle.X + rectangle.Width,
                pageHeight - rectangle.Y);
            byte alpha = (byte)Math.Round(255f * Math.Clamp(rectangle.Opacity, 0f, 1f));

            if (ParseColor(rectangle.FillColor) is SKColor fill)
            {
                using var fillPaint = new SKPaint
                {
                    Color = fill.WithAlpha(alpha),
                    Style = SKPaintStyle.Fill,
                    IsAntialias = true
                };
                canvas.DrawRect(rect, fillPaint);
            }

            using var strokePaint = new SKPaint
            {
                Color = (ParseColor(rectangle.StrokeColor) ?? SKColors.Red).WithAlpha(alpha),
                Style = SKPaintStyle.Stroke,
                StrokeWidth = Math.Max(0.1f, rectangle.StrokeWidth),
                IsAntialias = true
            };
            if (rectangle.DashPattern is { Length: > 0 })
                strokePaint.PathEffect = SKPathEffect.CreateDash(rectangle.DashPattern, 0f);
            canvas.DrawRect(rect, strokePaint);
        }

        private void DrawTextElement(
            SKCanvas canvas,
            TextElement element,
            float pageHeight,
            PageContext pageContext,
            PaginationRegistry pagination)
        {
            if (element == null)
                return;

            if (element.PageTextTemplate != null || element.PageReferenceAnchorId != null)
            {
                DrawResolvedFinalText(canvas, element, pageHeight, pageContext, pagination);
                return;
            }

            float innerWidth = element.MaxWidth ?? 0f;
            var paragraph = TextElementLayouter.Layout(element, innerWidth);

            int startLine = Math.Clamp(element.ShapedStartLine, 0, Math.Max(paragraph.Lines.Count - 1, 0));
            int remaining = paragraph.Lines.Count - startLine;
            int lineCount = element.ShapedLineCount > 0 ? Math.Min(element.ShapedLineCount, remaining) : remaining;
            lineCount = Math.Max(1, lineCount);
            var lines = paragraph.Lines.Skip(startLine).Take(lineCount).ToList();
            if (lines.Count == 0)
                return;

            float padL = element.PaddingLeft ?? 0f;
            float padR = element.PaddingRight ?? 0f;
            float padT = element.PaddingTop ?? 0f;
            float padB = element.PaddingBottom ?? 0f;

            float maxLineWidth = lines.Max(l => l.Width);
            float textBlockWidth = element.MaxWidth ?? maxLineWidth;
            float boxWidth = textBlockWidth + padL + padR;

            var baselines = new List<float>(lines.Count);
            float baseline = element.Y;
            foreach (var line in lines)
            {
                baselines.Add(baseline);
                baseline -= line.LineHeight;
            }

            float topY = baselines[0] + lines[0].Ascent + padT;
            float bottomY = baselines[^1] - lines[^1].Descent - padB;

            DrawTextBackground(canvas, element, textBlockWidth, boxWidth, baselines[0], baselines[^1], padL, padR, padT, padB, topY, bottomY, pageHeight);

            var textColor = ApplyOpacity(ParseColor(element.Color) ?? SKColors.Black, element.Opacity);

            for (int i = 0; i < lines.Count; i++)
            {
                var line = lines[i];
                float baselineY = pageHeight - baselines[i];

                float effectiveLineWidth = line.Width;

                float lineX = element.X;
                if (element.Alignment == TextAlignment.Center)
                    lineX += (textBlockWidth - effectiveLineWidth) / 2f;
                else if (element.Alignment == TextAlignment.Right)
                    lineX += textBlockWidth - effectiveLineWidth;

                bool isRtl = element.FlowDirection == FlowDirection.RightToLeft;
                float cursorX = isRtl ? lineX + effectiveLineWidth : lineX;

                foreach (var run in line.Runs)
                {
                    if (run.Glyphs.Count == 0)
                        continue;

                    float runAdvance = run.Width;
                    float runOriginX = cursorX;
                    if (isRtl)
                        runOriginX -= runAdvance;

                    DrawRun(canvas, run, runOriginX, baselineY, textColor, -element.Rotation);

                    if (!isRtl)
                        cursorX += runAdvance;
                    else
                        cursorX = runOriginX;
                }

                DrawDecorations(canvas, element, lineX, line.Width, baselineY, textColor);
            }
        }

        private void DrawResolvedFinalText(
            SKCanvas canvas,
            TextElement element,
            float pageHeight,
            PageContext pageContext,
            PaginationRegistry pagination)
        {
            string measurementText = element.Text;
            string? pageTemplate = element.PageTextTemplate;
            string? referenceAnchor = element.PageReferenceAnchorId;
            ShapedParagraph? measurementLayout = element.ShapedLayout;
            int measurementStartLine = element.ShapedStartLine;
            int measurementLineCount = element.ShapedLineCount;

            try
            {
                element.Text = pageTemplate != null
                    ? PageTextFormatter.Resolve(pageTemplate, pageContext)
                    : pagination.TryGetPageNumber(referenceAnchor!, out int pageNumber)
                        ? PageReferenceFormatter.Resolve(element.PageReferenceFormat!, pageNumber)
                        : element.PageReferencePendingText ?? "…";
                element.PageTextTemplate = null;
                element.PageReferenceAnchorId = null;
                element.ShapedLayout = null;
                element.ShapedStartLine = 0;
                element.ShapedLineCount = 0;
                DrawTextElement(canvas, element, pageHeight, pageContext, pagination);
            }
            finally
            {
                element.Text = measurementText;
                element.PageTextTemplate = pageTemplate;
                element.PageReferenceAnchorId = referenceAnchor;
                element.ShapedLayout = measurementLayout;
                element.ShapedStartLine = measurementStartLine;
                element.ShapedLineCount = measurementLineCount;
            }
        }

        private void DrawImageElement(SKCanvas canvas, ImageElement element, float pageHeight)
        {
            if (element.ImageData == null || element.ImageData.Length == 0)
                return;

            using var bitmap = SKBitmap.Decode(element.ImageData);
            if (bitmap == null)
                return;

            float width = element.Width > 0 ? element.Width : bitmap.Width;
            float height = element.Height > 0 ? element.Height : bitmap.Height;

            var destRect = new SKRect(element.X, pageHeight - element.Y, element.X + width, pageHeight - element.Y + height);

            if (Math.Abs(element.Rotation) > 0.0001f)
            {
                canvas.Save();
                canvas.Translate(destRect.MidX, destRect.MidY);
                canvas.RotateDegrees(-element.Rotation);
                var rotatedRect = new SKRect(-width / 2f, -height / 2f, width / 2f, height / 2f);
                using var paint = new SKPaint { IsAntialias = true, FilterQuality = SKFilterQuality.Medium };
                canvas.DrawBitmap(bitmap, rotatedRect, paint);
                canvas.Restore();
            }
            else
            {
                using var paint = new SKPaint { IsAntialias = true, FilterQuality = SKFilterQuality.Medium };
                canvas.DrawBitmap(bitmap, destRect, paint);
            }
        }

        private void DrawRun(SKCanvas canvas, ShapedRun run, float originX, float baselineY, SKColor color, float rotationDegrees)
        {
            using var paint = new SKPaint
            {
                Typeface = run.Typeface,
                TextSize = run.FontSize,
                Color = color,
                IsAntialias = true,
                SubpixelText = true,
                TextEncoding = SKTextEncoding.Utf16,
                HintingLevel = SKPaintHinting.NoHinting,
                LcdRenderText = true
            };

            if (Math.Abs(rotationDegrees) > 0.0001f)
            {
                canvas.Save();
                canvas.Translate(originX, baselineY);
                canvas.RotateDegrees(rotationDegrees);
                canvas.DrawText(run.Text, 0f, 0f, paint);
                canvas.Restore();
            }
            else
            {
                canvas.DrawText(run.Text, originX, baselineY, paint);
            }
        }

        private void DrawTextBackground(SKCanvas canvas, TextElement element, float textBlockWidth, float boxWidth, float firstBaseline, float lastBaseline, float padL, float padR, float padT, float padB, float topY, float bottomY, float pageHeight)
        {
            if (string.IsNullOrWhiteSpace(element.BackgroundColor) &&
                (string.IsNullOrWhiteSpace(element.BackgroundBorderColor) || (element.BackgroundBorderWidth ?? 0f) <= 0f))
            {
                return;
            }

            float left = element.X - padL;
            float right = left + boxWidth;
            float top = pageHeight - topY;
            float bottom = pageHeight - bottomY;
            var rect = new SKRect(left, bottom, right, top);

            var roundRect = BuildRoundRect(rect, element);

            if (!string.IsNullOrWhiteSpace(element.BackgroundColor))
            {
                var fill = ParseColor(element.BackgroundColor);
                if (fill != null)
                {
                    using var paint = new SKPaint { Color = fill.Value, Style = SKPaintStyle.Fill, IsAntialias = true };
                    canvas.DrawRoundRect(roundRect, paint);
                }
            }

            if (!string.IsNullOrWhiteSpace(element.BackgroundBorderColor) && (element.BackgroundBorderWidth ?? 0f) > 0f)
            {
                var stroke = ParseColor(element.BackgroundBorderColor);
                if (stroke != null)
                {
                    using var paint = new SKPaint
                    {
                        Color = stroke.Value,
                        Style = SKPaintStyle.Stroke,
                        StrokeWidth = Math.Max(0.25f, element.BackgroundBorderWidth ?? 1f),
                        IsAntialias = true
                    };
                    canvas.DrawRoundRect(roundRect, paint);
                }
            }
        }

        private void DrawDecorations(SKCanvas canvas, TextElement element, float lineX, float lineWidth, float baseline, SKColor textColor)
        {
            if (!element.Underline && !element.Strikethrough && !element.Overline)
                return;

            float underlineY = baseline + Math.Max(1f, element.FontSize * 0.08f);
            float strikeY = baseline - element.FontSize * 0.30f;
            float overlineY = baseline - element.FontSize * 0.90f;
            float strokeWidth = element.DecorationThickness ?? Math.Max(0.7f, element.FontSize * 0.05f);
            var decorationColor = ParseColor(element.DecorationColor) ?? textColor;
            decorationColor = ApplyOpacity(decorationColor, element.Opacity);

            var style = element.DecorationStyle;

            if (element.Underline)
                DrawDecoration(canvas, lineX, underlineY, lineX + lineWidth, underlineY, decorationColor, strokeWidth, style);
            if (element.Strikethrough)
                DrawDecoration(canvas, lineX, strikeY, lineX + lineWidth, strikeY, decorationColor, strokeWidth, style);
            if (element.Overline)
                DrawDecoration(canvas, lineX, overlineY, lineX + lineWidth, overlineY, decorationColor, strokeWidth, style);
        }

        private void DrawDecoration(SKCanvas canvas, float x1, float y1, float x2, float y2, SKColor color, float width, TextDecorationStyle style)
        {
            if (style == TextDecorationStyle.Double)
            {
                float offset = Math.Max(width, 0.5f);
                float halfWidth = width * 0.6f;
                DrawDecoration(canvas, x1, y1 + offset, x2, y2 + offset, color, halfWidth, TextDecorationStyle.Solid);
                DrawDecoration(canvas, x1, y1 - offset, x2, y2 - offset, color, halfWidth, TextDecorationStyle.Solid);
                return;
            }

            using var paint = new SKPaint
            {
                Color = color,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = width,
                IsAntialias = true,
                StrokeCap = SKStrokeCap.Butt
            };

            switch (style)
            {
                case TextDecorationStyle.Dotted:
                    paint.PathEffect = SKPathEffect.CreateDash(new[] { width, width }, 0f);
                    break;
                case TextDecorationStyle.Dashed:
                    paint.PathEffect = SKPathEffect.CreateDash(new[] { width * 4f, width * 2f }, 0f);
                    break;
            }

            canvas.DrawLine(x1, y1, x2, y2, paint);
        }

        private static SKRoundRect BuildRoundRect(SKRect rect, TextElement element)
        {
            var roundRect = new SKRoundRect();
            float uniform = element.BackgroundCornerRadius ?? 0f;

            float tl = element.BackgroundCornerRadiusTopLeft ?? uniform;
            float tr = element.BackgroundCornerRadiusTopRight ?? uniform;
            float br = element.BackgroundCornerRadiusBottomRight ?? uniform;
            float bl = element.BackgroundCornerRadiusBottomLeft ?? uniform;

            var radii = new[]
            {
                new SKPoint(tl, tl),
                new SKPoint(tr, tr),
                new SKPoint(br, br),
                new SKPoint(bl, bl)
            };

            roundRect.SetRectRadii(rect, radii);
            return roundRect;
        }

        private static SKColor ApplyOpacity(SKColor color, float opacity)
        {
            float clamped = Math.Clamp(opacity, 0f, 1f);
            return color.WithAlpha((byte)Math.Round(color.Alpha * clamped));
        }

        private static SKColor? ParseColor(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            string normalized = value.Trim();
            if (normalized.StartsWith("#", StringComparison.Ordinal))
            {
                normalized = normalized[1..];
                if (normalized.Length == 3)
                {
                    normalized = new string(new[]
                    {
                        normalized[0], normalized[0],
                        normalized[1], normalized[1],
                        normalized[2], normalized[2]
                    });
                }

                if (normalized.Length == 6 &&
                    int.TryParse(normalized[..2], System.Globalization.NumberStyles.HexNumber, null, out var r) &&
                    int.TryParse(normalized.Substring(2, 2), System.Globalization.NumberStyles.HexNumber, null, out var g) &&
                    int.TryParse(normalized.Substring(4, 2), System.Globalization.NumberStyles.HexNumber, null, out var b))
                {
                    return new SKColor((byte)r, (byte)g, (byte)b);
                }
            }

            return normalized.Equals("black", StringComparison.OrdinalIgnoreCase)
                ? SKColors.Black
                : normalized.Equals("white", StringComparison.OrdinalIgnoreCase)
                    ? SKColors.White
                    : null;
        }
    }
}
