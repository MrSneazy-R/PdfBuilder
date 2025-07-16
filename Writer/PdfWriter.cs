using PdfBuilder.Document;
using PdfBuilder.Elements;
using PdfBuilder.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using static System.Net.Mime.MediaTypeNames;

namespace PdfBuilder.Writer
{
    public class PdfWriter
    {
        private static readonly IFormatProvider PdfCulture = System.Globalization.CultureInfo.InvariantCulture;
        private string PdfNumber(float value) => value.ToString("0.###", PdfCulture);
        private string PdfNumber(double value) => value.ToString("0.###", PdfCulture);
        private string PdfNumber(int value) => value.ToString(PdfCulture);

        private StreamWriter _writer;
        private MemoryStream _output;
        private List<long> _objOffsets;
        private int _currentObjId;

        private Dictionary<string, (int ObjId, int Width, int Height, string MimeType, byte[] Data)> _imageResources = new();
        private int _imageNextObjId = 500;
        private Dictionary<float, int> _opacityExtGStates = new(); // Key = opacity (0..1), Value = object ID
        private int _extGStateNextObjId = 800; // Start high (after fonts/images)

        private FontManager _fontManager;

        public byte[] GenerateBytes(PdfDocument doc)
        {
            
            _fontManager = new FontManager(startingObjectId: 100);
            var usedFonts = GetUsedFontKeys(doc);
            foreach (var fontKey in usedFonts)
                _fontManager.RegisterFont(fontKey);

            _output = new MemoryStream();
            // Use ASCII, no BOM, leave stream open so we can write raw bytes as well.
            _writer = new StreamWriter(_output, Encoding.ASCII, 1024, leaveOpen: true);
            _writer.WriteLine("%PDF-1.4");

            // ---------- IMAGE RESOURCE MAPPING ----------
            _imageResources.Clear();
            foreach (var page in doc.Pages)
                foreach (var img in page.Elements.OfType<ImageElement>())
                {
                    string key = img.ImageId ?? Convert.ToBase64String(System.Security.Cryptography.SHA1.Create().ComputeHash(img.ImageData));
                    if (!_imageResources.ContainsKey(key))
                    {
                        var (w, h) = ImageHeaderParser.GetDimensions(img.ImageData);
                        int ObjId = _imageNextObjId++;
                        string mimeType = img.MimeType;
                        if (string.IsNullOrEmpty(mimeType))
                        {
                            if (ImageHeaderParser.IsJpeg(img.ImageData)) mimeType = "image/jpeg";
                            else if (ImageHeaderParser.IsPng(img.ImageData)) mimeType = "image/png";
                            else mimeType = "application/octet-stream";
                        }
                        _imageResources[key] = (ObjId, w, h, mimeType, img.ImageData);
                    }
                    img.ImageId = key;
                }

            _opacityExtGStates.Clear();
            foreach (var page in doc.Pages)
                foreach (var img in page.Elements.OfType<ImageElement>())
                {
                    if (img.Opacity < 1.0f && img.Opacity > 0f && !_opacityExtGStates.ContainsKey(img.Opacity))
                        _opacityExtGStates[img.Opacity] = _extGStateNextObjId++;
                }
            // --- 1. Calculate object IDs ---
            int objId = 1;
            int catalogId = objId++;
            int pagesId = objId++;
            var pageObjIds = new List<int>();
            var contentObjIds = new List<int>();

            for (int i = 0; i < doc.Pages.Count; i++)
            {
                pageObjIds.Add(objId++);
                contentObjIds.Add(objId++);
            }

            var fontIds = _fontManager.FontMap.Values.ToList();
            int maxFontId = fontIds.Count > 0 ? fontIds.Max() : 0;
            int highestObjId = Math.Max(objId - 1, maxFontId);

            // -- 2. Prepare _objOffsets for every object id 0..highestObjId --
            int maxImageObjId = _imageResources.Values.Select(x => x.ObjId).DefaultIfEmpty(0).Max();
            int maxExtGStateObjId = _opacityExtGStates.Values.DefaultIfEmpty(0).Max();
            highestObjId = new[] { objId - 1, maxFontId, maxImageObjId, maxExtGStateObjId }.Max();
            _objOffsets = Enumerable.Repeat(0L, highestObjId + 1).ToList();

            // Helper for writing and tracking offsets
            void WriteObject(int id, Action<StreamWriter> writeAction)
            {
                _writer.Flush();
                _objOffsets[id] = _output.Position;
                writeAction(_writer);
                _writer.Flush();
                _writer.WriteLine("endobj");
                _writer.Flush();
            }

            // 1. Catalog
            WriteObject(catalogId, writer => {
                writer.WriteLine($"{catalogId} 0 obj");
                writer.WriteLine($"<< /Type /Catalog /Pages {pagesId} 0 R >>");
            });

            // 2. Pages
            WriteObject(pagesId, writer => {
                writer.WriteLine($"{pagesId} 0 obj");
                writer.WriteLine($"<< /Type /Pages /Kids [{string.Join(" ", pageObjIds.Select(id => $"{id} 0 R"))}] /Count {doc.Pages.Count} >>");
            });
         

            // Pages and contents
            for (int i = 0; i < doc.Pages.Count; i++)
            {
                var page = doc.Pages[i];
                int pageObjId = pageObjIds[i];
                int contentObjId = contentObjIds[i];

                string fontRes = BuildFontResourcesForPage(page);
                string imageRes = BuildImageResourcesForPage(page);

                // Page object
                WriteObject(pageObjId, writer => {
                    string fontResourceBlock = BuildFontResourcesForPage(page);
                    string extGStateBlock = BuildExtGStateResourcesForPage(page);
                    string imageResourceBlock = BuildImageResourcesForPage(page);
                    writer.WriteLine($"{pageObjId} 0 obj");
                    writer.WriteLine(
                        $"<< /Type /Page /Parent {pagesId} 0 R /MediaBox [0 0 {page.Width} {page.Height}] /Contents {contentObjId} 0 R /Resources << /Font << {fontResourceBlock} >> /XObject << {imageResourceBlock} >> {extGStateBlock} >> >>"
                    );
                });

                // Content stream
                WriteObject(contentObjId, writer => {
                    string contentText = BuildContentStream(page.Elements);
                    byte[] contentBytes = Encoding.ASCII.GetBytes(contentText);

                    writer.WriteLine($"{contentObjId} 0 obj");
                    writer.WriteLine($"<< /Length {contentBytes.Length} >>");
                    writer.WriteLine("stream");
                    writer.Flush(); // Ensure raw byte write is after the newline
                    _output.Write(contentBytes, 0, contentBytes.Length);
                    writer.Write("\nendstream\n");
                });
            }

            // Font objects
            foreach (var font in _fontManager.FontMap)
            {
                int fontObjId = font.Value;
                WriteObject(fontObjId, writer => {
                    writer.WriteLine($"{fontObjId} 0 obj");
                    writer.WriteLine($"<< /Type /Font /Subtype /Type1 /BaseFont {_fontManager.ResolveBaseFontName(font.Key)} >>");
                });
            }

            foreach (var kv in _imageResources)
            {
                var (ObjId, width, height, mime, data) = kv.Value;
                WriteObject(ObjId, writer =>
                {
                    writer.WriteLine($"{ObjId} 0 obj");
                    if (mime == "image/jpeg" || mime == "image/jpg")
                    {
                        // Always specify ColorSpace for JPEG!
                        writer.WriteLine($"<< /Type /XObject /Subtype /Image /Width {width} /Height {height} /ColorSpace /DeviceRGB /Filter /DCTDecode /Length {data.Length} >>");
                    }
                    else
                    {
                        writer.WriteLine($"<< /Type /XObject /Subtype /Image /Width {width} /Height {height} /ColorSpace /DeviceRGB /BitsPerComponent 8 /Length {data.Length} /Filter /FlateDecode >>");
                    }
                    writer.Flush(); // Make sure all header text is written

                    // --- Write stream header and binary image data correctly ---
                    // Write "stream\n" as raw ASCII bytes (NOT with StreamWriter to avoid platform newlines!)
                    _output.Write(Encoding.ASCII.GetBytes("stream\n"), 0, "stream\n".Length);

                    // Write image data (raw binary)
                    _output.Write(data, 0, data.Length);

                    // Write "\nendstream\n" as raw ASCII bytes
                    _output.Write(Encoding.ASCII.GetBytes("\nendstream\n"), 0, "\nendstream\n".Length);
                });
            }
            foreach (var kv in _opacityExtGStates)
            {
                float opacity = kv.Key;
                int ObjId = kv.Value;
                WriteObject(ObjId, writer =>
                {
                    writer.WriteLine($"{ObjId} 0 obj");
                    writer.WriteLine($"<< /Type /ExtGState /ca {opacity.ToString("0.###", PdfCulture)} /CA {opacity.ToString("0.###", PdfCulture)} >>");
                });
            }

            _writer.Flush();
            // Write image XObjects

            long xrefStart = _output.Position;

            // --- Write full xref for 0..highestObjId ---
            _writer.WriteLine("xref");
            _writer.WriteLine($"0 {_objOffsets.Count}");
            for (int i = 0; i < _objOffsets.Count; i++)
            {
                long offset = _objOffsets[i];
                if (i == 0 || offset == 0)
                    _writer.WriteLine("0000000000 65535 f ");
                else
                    _writer.WriteLine($"{offset:D10} 00000 n ");
            }

            _writer.WriteLine($"trailer << /Size {_objOffsets.Count} /Root {catalogId} 0 R >>");
            _writer.WriteLine("startxref");
            _writer.WriteLine(xrefStart);
            _writer.WriteLine("%%EOF");
            _writer.Flush();

            _output.Position = 0;
            return _output.ToArray();
        }


        // Returns just the /F100 100 0 R /F101 101 0 R ... keys, not nested << >>
        private string BuildFontResourcesForPage(PdfPage page)
        {
            var fontRefs = new StringBuilder();
            var used = new HashSet<string>();

            void AddFontForKey(string key)
            {
                if (!used.Contains(key) && _fontManager.FontMap.TryGetValue(key, out int fontObjId))
                {
                    fontRefs.Append($"/F{fontObjId} {fontObjId} 0 R ");
                    used.Add(key);
                }
            }

            // Text elements
            foreach (var elem in page.Elements.OfType<TextElement>())
            {
                AddFontForKey(FontManager.NormalizeFontKey(elem.FontFamily, elem.Bold, elem.Italic));
            }

            // Table elements
            foreach (var table in page.Elements.OfType<TableElement>())
            {
                // Header
                foreach (var cell in table.HeaderCells)
                    AddFontForKey(FontManager.NormalizeFontKey(
                        cell.FontFamily ?? table.HeaderFontFamily ?? table.FontFamily ?? "Helvetica",
                        cell.Bold,
                        cell.Italic
                    ));

                // Body
                foreach (var row in table.Rows)
                    foreach (var cell in row)
                        AddFontForKey(FontManager.NormalizeFontKey(
                            cell.FontFamily ?? table.FontFamily ?? "Helvetica",
                            cell.Bold,
                            cell.Italic
                        ));

                // Footer
                if (table.FooterCells != null)
                    foreach (var cell in table.FooterCells)
                        AddFontForKey(FontManager.NormalizeFontKey(
                            cell.FontFamily ?? table.FooterFontFamily ?? table.FontFamily ?? "Helvetica",
                            cell.Bold,
                            cell.Italic
                        ));
            }

            return fontRefs.ToString().Trim();
        }



        private string BuildImageResourcesForPage(PdfPage page)
        {
            var sb = new StringBuilder();
            var used = new HashSet<string>();
            foreach (var img in page.Elements.OfType<ImageElement>())
            {
                if (!_imageResources.TryGetValue(img.ImageId, out var res))
                    continue;
                var resourceName = $"Im{res.ObjId}";
                if (!used.Contains(resourceName))
                {
                    sb.Append($"/{resourceName} {res.ObjId} 0 R ");
                    used.Add(resourceName);
                }
                img.PdfResourceName = resourceName;
            }
            return sb.ToString().Trim();
        }


        private string BuildContentStream(IEnumerable<PdfElement> elements)
        {
            var sb = new StringBuilder();

            foreach (var elem in elements)
            {
                switch (elem)
                {
                    case TextElement txt:
                        var text = ($"Writing text '{txt.Text.Substring(0, Math.Min(txt.Text.Length, 20))}' at Y = {txt.Y}");
                        Console.WriteLine(text); // Debug output
                        AppendTextElement(sb, txt);
                        break;

                    case UnderlineElement line:
                        AppendUnderlineElement(sb, line);
                        break;

                    case ImageElement img:
                        AppendImageElement(sb, img);
                        break;
                    case TableElement table: 
                        AppendTableElement(sb, table);
                        break;
                }
            }

            return sb.ToString();
        }

        private void AppendTextElement(StringBuilder sb, TextElement txt)
        {
            if (!_fontManager.FontMap.TryGetValue(GetFontKey(txt), out int fontId))
                return;

            float padLeft   = txt.PaddingLeft   ?? 0;
            float padRight  = txt.PaddingRight  ?? 0;
            float padTop    = txt.PaddingTop    ?? 0;
            float padBottom = txt.PaddingBottom ?? 0;
            float maxWidth  = txt.MaxWidth ?? 400;
            var lines = PdfLayoutUtils.WrapText(txt.Text, maxWidth, txt.FontSize);

            // For correct box drawing, need to pass in all paddings/margins
            if (!string.IsNullOrWhiteSpace(txt.BackgroundColor) ||
                (!string.IsNullOrWhiteSpace(txt.BackgroundBorderColor) && txt.BackgroundBorderWidth > 0))
            {
                AppendBackgroundBox(sb, txt, lines);
            }

            float currentY = txt.Y;
            float baselineOffset = txt.BaselineOffset ?? 0;

            foreach (var line in lines)
            {
                float xPos = CalculateXPosition(txt, line);
                float yPos = currentY + baselineOffset;

                sb.AppendLine("BT");
                sb.AppendLine($"/F{fontId} {txt.FontSize} Tf");
                sb.AppendLine($"{ConvertColor(txt.Color)} rg");

                if (txt.Rotation != 0)
                    AppendRotation(sb, xPos, yPos, txt.Rotation);
                else if (txt.Italic)
                    sb.AppendLine($"1 {PdfNumber(Math.Tan(15 * Math.PI / 180))} 0 1 {PdfNumber(xPos)} {PdfNumber(yPos)} Tm");
                else
                    sb.AppendLine($"{PdfNumber(xPos)} {PdfNumber(yPos)} Td");

                if (txt.Underline)
                    AppendUnderline(sb, xPos, yPos, PdfLayoutUtils.EstimateTextWidth(line, FontManager.NormalizeFontKey(txt.FontFamily, txt.Bold, txt.Italic), txt.FontSize, txt.Monospace), txt, fontId);

                // Strikethrough
                if (txt.Strikethrough)
                {
                    float strikeY = yPos - txt.FontSize * txt.LineHeight * 0.32f;
                    sb.AppendLine("ET");
                    sb.AppendLine($"{ConvertColor(txt.Color)} RG");
                    sb.AppendLine($"{PdfNumber(xPos)} {PdfNumber(strikeY)} m {PdfNumber(xPos + PdfLayoutUtils.EstimateTextWidth(line, FontManager.NormalizeFontKey(txt.FontFamily, txt.Bold, txt.Italic), txt.FontSize, txt.Monospace))} {PdfNumber(strikeY)} l S");
                    sb.AppendLine("BT");
                    sb.AppendLine($"/F{fontId} {txt.FontSize} Tf");
                    sb.AppendLine($"{ConvertColor(txt.Color)} rg");
                    sb.AppendLine($"{PdfNumber(xPos)} {PdfNumber(yPos)} Td");
                }

                // Overline
                if (txt.Overline)
                {
                    float overlineY = yPos + txt.FontSize * 0.22f;
                    sb.AppendLine("ET");
                    sb.AppendLine($"{ConvertColor(txt.Color)} RG");
                    sb.AppendLine($"{PdfNumber(xPos)} {PdfNumber(overlineY)} m {PdfNumber(xPos + PdfLayoutUtils.EstimateTextWidth(line, FontManager.NormalizeFontKey(txt.FontFamily, txt.Bold, txt.Italic), txt.FontSize, txt.Monospace))} {PdfNumber(overlineY)} l S");
                    sb.AppendLine("BT");
                    sb.AppendLine($"/F{fontId} {txt.FontSize} Tf");
                    sb.AppendLine($"{ConvertColor(txt.Color)} rg");
                    sb.AppendLine($"{PdfNumber(xPos)} {PdfNumber(yPos)} Td");
                }

                sb.AppendLine($"({EscapePdfText(line)}) Tj");
                sb.AppendLine("ET");
                currentY -= txt.FontSize * txt.LineHeight;
            }
        }
        private const float PageHeight = 842f;

        private void AppendImageElement(StringBuilder sb, ImageElement img)
        {
            if (!_imageResources.TryGetValue(img.ImageId, out var res))
                return;

            float naturalW = res.Width;
            float naturalH = res.Height;

            float renderW = img.Width > 0 ? img.Width : naturalW;
            float renderH = img.Height > 0 ? img.Height : naturalH;

            if (img.Width > 0 && img.Height == 0)
                renderH = img.Width * naturalH / naturalW;
            else if (img.Height > 0 && img.Width == 0)
                renderW = img.Height * naturalW / naturalH;

            float padLeft = img.PaddingLeft ?? 0;
            float padRight = img.PaddingRight ?? 0;
            float padTop = img.PaddingTop ?? 0;
            float padBottom = img.PaddingBottom ?? 0;

            float boxX = img.X - padLeft;
            float boxY = img.Y - padTop;
            float boxW = renderW + padLeft + padRight;
            float boxH = renderH + padTop + padBottom;
            float flippedBoxY = PageHeight - boxY - boxH;

            float cornerRadius = img.CornerRadius ?? 0;

            // Begin graphics state
            sb.AppendLine("q");

            // --- 1. CLIPPING (rounded/circle/ellipse only) ---
            if (img.ClipShape == ImageClipShape.Circle || img.ClipShape == ImageClipShape.Ellipse || img.ClipShape == ImageClipShape.RoundedRect)
            {
                if (img.ClipShape == ImageClipShape.Circle)
                {
                    float cx = img.X + renderW / 2;
                    float cy = PageHeight - (img.Y + renderH / 2);
                    float r = Math.Min(renderW, renderH) / 2;
                    AppendEllipsePath(sb, cx, cy, r, r);
                }
                else if (img.ClipShape == ImageClipShape.Ellipse)
                {
                    float cx = img.X + renderW / 2;
                    float cy = PageHeight - (img.Y + renderH / 2);
                    float rx = renderW / 2;
                    float ry = renderH / 2;
                    AppendEllipsePath(sb, cx, cy, rx, ry);
                }
                else if (img.ClipShape == ImageClipShape.RoundedRect)
                {
                    AppendRoundedRect(sb, img.X, PageHeight - img.Y - renderH, renderW, renderH, img.CornerRadius ?? 12);
                }
                sb.AppendLine("W n");
            }

            // --- 2. OPACITY ---
            int gsId = 0;
            bool needsOpacity = img.Opacity < 1.0f && img.Opacity > 0f && _opacityExtGStates.TryGetValue(img.Opacity, out gsId);
            if (needsOpacity)
                sb.AppendLine($"/GS_{gsId} gs");

            // --- 3. ROTATION ---
            if (img.Rotation != 0)
            {
                float cx = img.X + renderW / 2;
                float cy = img.Y + renderH / 2;
                float flippedCy = PageHeight - cy;
                double rad = img.Rotation * Math.PI / 180.0;
                double cos = Math.Cos(rad);
                double sin = Math.Sin(rad);

                sb.AppendLine($"{PdfNumber(cos)} {PdfNumber(sin)} {PdfNumber(-sin)} {PdfNumber(cos)} {PdfNumber(cx)} {PdfNumber(flippedCy)} cm");
                sb.AppendLine($"{PdfNumber(-renderW / 2)} {PdfNumber(-renderH / 2)} {PdfNumber(renderW)} {PdfNumber(renderH)} cm");
            }
            else
            {
                sb.AppendLine($"{PdfNumber(renderW)} 0 0 {PdfNumber(renderH)} {PdfNumber(img.X)} {PdfNumber(PageHeight - img.Y - renderH)} cm");
            }

            // --- 4. SHADOW (draw first, under image) ---
            if (!string.IsNullOrWhiteSpace(img.ShadowColor) && (img.ShadowOffsetX.HasValue || img.ShadowOffsetY.HasValue))
            {
                float shadowX = boxX + (img.ShadowOffsetX ?? 0);
                float shadowY = boxY + (img.ShadowOffsetY ?? 0);
                float flippedShadowY = PageHeight - shadowY - boxH;

                sb.AppendLine("q");
                sb.AppendLine($"{ConvertColor(img.ShadowColor)} rg");
                if (cornerRadius > 0)
                    AppendRoundedRect(sb, shadowX, flippedShadowY, boxW, boxH, cornerRadius);
                else
                    sb.AppendLine($"{PdfNumber(shadowX)} {PdfNumber(flippedShadowY)} {PdfNumber(boxW)} {PdfNumber(boxH)} re");
                sb.AppendLine("f");
                sb.AppendLine("Q");
            }

            // --- 5. DRAW IMAGE ---
            sb.AppendLine($"/{img.PdfResourceName} Do");

            // --- 6. BORDER (draw last, on top of image) ---
            if (!string.IsNullOrEmpty(img.BorderColor) && img.BorderWidth > 0)
            {
                sb.AppendLine($"{ConvertColor(img.BorderColor)} RG");
                sb.AppendLine($"{PdfNumber(img.BorderWidth.Value)} w");
                if (cornerRadius > 0)
                    AppendRoundedRect(sb, boxX, flippedBoxY, boxW, boxH, cornerRadius);
                else
                    sb.AppendLine($"{PdfNumber(boxX)} {PdfNumber(flippedBoxY)} {PdfNumber(boxW)} {PdfNumber(boxH)} re");
                sb.AppendLine("S");
            }

            // Restore graphics state
            sb.AppendLine("Q");
        }
        private void AppendTableElement(StringBuilder sb, TableElement table)
        {
            float x = table.X;
            float y = table.Y;

            int cols = table.Columns.Count;
            if (cols == 0) return;

            float[] colWidths = new float[cols];
            float totalConstant = 0f;
            float totalWeight = 0f;
            for (int i = 0; i < cols; i++)
            {
                var def = table.Columns[i];
                if (def.IsConstant) totalConstant += def.Value;
                else totalWeight += def.Value;
            }

            float remainingWidth = table.Width - totalConstant;
            for (int i = 0; i < cols; i++)
            {
                var def = table.Columns[i];
                colWidths[i] = def.IsConstant ? def.Value :
                    (totalWeight > 0 ? def.Value / totalWeight * remainingWidth : 0f);
            }

            float curY = y;

            // ====== HEADER ======
            float headerHeight = table.HeaderRowHeight ?? 28;
            float curX = x;
            int headerCol = 0;

            while (headerCol < table.HeaderCells.Count)
            {
                var cell = table.HeaderCells[headerCol];
                int span = Math.Max(1, cell.ColSpan);
                float width = colWidths.Skip(headerCol).Take(span).Sum();

                DrawTableCell(sb, cell, curX, curY, width, headerHeight, table, isHeader: true);

                curX += width;
                headerCol += span;
            }

            curY -= headerHeight;

            // ====== BODY ======
            foreach (var row in table.Rows)
            {
                float rowHeight = 0f;
                int colIndex = 0;

                // First pass to measure max height
                foreach (var cell in row)
                {
                    int span = Math.Max(1, cell.ColSpan);
                    float width = colWidths.Skip(colIndex).Take(span).Sum();

                    float fontSize = cell.FontSize ?? table.FontSize ?? 12f;
                    string fontKey = FontManager.NormalizeFontKey(cell.FontFamily ?? table.FontFamily ?? "Helvetica", cell.Bold);

                    float padL = cell.PaddingLeft ?? 6;
                    float padR = cell.PaddingRight ?? 6;
                    float padT = cell.PaddingTop ?? 4;
                    float padB = cell.PaddingBottom ?? 4;

                    float maxTextWidth = width - padL - padR;
                    int lineCount = (cell.WrapText || table.WrapText)
                        ? WrapText(cell.Text ?? "", fontKey, fontSize, maxTextWidth).Count
                        : 1;

                    float height = padT + padB + (lineCount * fontSize * 1.2f);
                    rowHeight = Math.Max(rowHeight, height);

                    colIndex += span;
                }

                // Second pass to draw
                curX = x;
                colIndex = 0;
                foreach (var cell in row)
                {
                    int span = Math.Max(1, cell.ColSpan);
                    float width = colWidths.Skip(colIndex).Take(span).Sum();

                    DrawTableCell(sb, cell, curX, curY, width, rowHeight, table, isHeader: false);

                    curX += width;
                    colIndex += span;
                }

                curY -= rowHeight;
            }


            // ====== FOOTER ======
            if (table.FooterCells != null && table.FooterCells.Count > 0)
            {
                float footerHeight = 0f;
                int col = 0;
                int i = 0;

                // First pass to measure height
                while (col < cols && i < table.FooterCells.Count)
                {
                    var cell = table.FooterCells[i];
                    int span = Math.Max(1, cell.ColSpan);
                    float width = colWidths.Skip(col).Take(span).Sum();

                    float fontSize = cell.FontSize ?? table.FooterFontSize ?? table.FontSize ?? 12f;
                    string fontKey = FontManager.NormalizeFontKey(cell.FontFamily ?? table.FooterFontFamily ?? table.FontFamily ?? "Helvetica", cell.Bold);

                    float padL = cell.PaddingLeft ?? 6;
                    float padR = cell.PaddingRight ?? 6;
                    float padT = cell.PaddingTop ?? 4;
                    float padB = cell.PaddingBottom ?? 4;

                    float maxTextWidth = width - padL - padR;
                    int lineCount = (cell.WrapText || table.WrapText)
                        ? WrapText(cell.Text ?? "", fontKey, fontSize, maxTextWidth).Count
                        : 1;

                    float height = padT + padB + (lineCount * fontSize * 1.2f);
                    footerHeight = Math.Max(footerHeight, height);

                    col += span;
                    i++;
                }

                // Second pass to draw
                curX = x;
                col = 0;
                i = 0;

                while (col < cols && i < table.FooterCells.Count)
                {
                    var cell = table.FooterCells[i];
                    int span = Math.Max(1, cell.ColSpan);
                    float width = colWidths.Skip(col).Take(span).Sum();

                    DrawTableCell(sb, cell, curX, curY, width, footerHeight, table, isHeader: false, isFooter: true);

                    curX += width;
                    col += span;
                    i++;
                }

                curY -= footerHeight;
            }
        }
        private void DrawTableCell(StringBuilder sb, TableCellElement cell, float x, float y, float width, float height, TableElement table, bool isHeader = false, bool isFooter = false)
        {
            string bg = cell.BackgroundColor
                ?? (isHeader ? table.HeaderBackgroundColor
                : isFooter ? table.FooterBackgroundColor ?? table.HeaderBackgroundColor
                : table.RowBackgroundColor);

            if (!string.IsNullOrWhiteSpace(bg))
            {
                sb.AppendLine($"{ConvertColor(bg)} rg");
                sb.AppendLine($"{PdfNumber(x)} {PdfNumber(y - height)} {PdfNumber(width)} {PdfNumber(height)} re f");
            }

            if (!string.IsNullOrWhiteSpace(table.BorderColor) && table.BorderWidth > 0)
            {
                sb.AppendLine($"{ConvertColor(table.BorderColor)} RG");
                sb.AppendLine($"{PdfNumber(table.BorderWidth ?? 1)} w");
                sb.AppendLine($"{PdfNumber(x)} {PdfNumber(y - height)} {PdfNumber(width)} {PdfNumber(height)} re S");
            }

            sb.AppendLine("BT");

            string fontKey = FontManager.NormalizeFontKey(
                cell.FontFamily
                ?? (isHeader ? table.HeaderFontFamily : isFooter ? table.FooterFontFamily : table.FontFamily)
                ?? "Helvetica", cell.Bold);

            float fontSize = cell.FontSize
                ?? (isHeader ? table.HeaderFontSize : isFooter ? table.FooterFontSize : table.FontSize)
                ?? 12f;

            sb.AppendLine($"/F{_fontManager.FontMap[fontKey]} {fontSize} Tf");
            sb.AppendLine($"{ConvertColor(cell.FontColor ?? table.TextColor ?? "#000")} rg");

            float padL = cell.PaddingLeft ?? 6;
            float padR = cell.PaddingRight ?? 6;
            float padT = cell.PaddingTop ?? 4;

            float maxTextWidth = width - padL - padR;
            var lines = (cell.WrapText || table.WrapText)
                ? WrapText(cell.Text ?? "", fontKey, fontSize, maxTextWidth)
                : new List<string> { cell.Text ?? "" };

            float lineSpacing = fontSize * 1.25f;
            float currentY = y - padT - fontSize * 0.85f;

            foreach (var line in lines)
            {
                float textWidth = PdfLayoutUtils.EstimateTextWidth(line, fontKey, fontSize);
                float minX = x + padL;
                float maxX = x + width - padR;

                float tx;
                if (cell.Alignment == TableCellAlignment.Right)
                {
                    tx = maxX - textWidth;
                    if (tx < minX) tx = minX; // prevent overflow
                }
                else if (cell.Alignment == TableCellAlignment.Center)
                {
                    tx = x + (width - textWidth) / 2f;
                    tx = Math.Clamp(tx, minX, maxX - textWidth);
                }
                else // left
                {
                    tx = minX;
                }

                sb.AppendLine($"1 0 0 1 {PdfNumber(tx)} {PdfNumber(currentY)} Tm");
                sb.AppendLine($"{Utf16HexText(line)} Tj");

                currentY -= lineSpacing;
            }

            sb.AppendLine("ET");
        }




        // Helper for special UTF-16 hex string
        private static string Utf16HexText(string? input)
        {
            if (string.IsNullOrEmpty(input)) return "()";
            string utf16Hex = BitConverter.ToString(Encoding.BigEndianUnicode.GetBytes(input)).Replace("-", "");
            return $"<{utf16Hex}>";
        }

        private List<string> WrapText(string text, string fontKey, float fontSize, float maxWidth)
        {
            var lines = new List<string>();
            var words = text.Split(new[] { ' ' }, StringSplitOptions.None);
            string currentLine = "";
            float spaceWidth = PdfLayoutUtils.EstimateTextWidth(" ", fontKey, fontSize);

            foreach (var word in words)
            {
                float wordWidth = PdfLayoutUtils.EstimateTextWidth(word, fontKey, fontSize);
                float currentWidth = PdfLayoutUtils.EstimateTextWidth(currentLine, fontKey, fontSize);

                if (wordWidth > maxWidth)
                {
                    // Force-split long word
                    var splitParts = SplitWordWithHyphen(word, fontKey, fontSize, maxWidth, 35f);

                    foreach (var part in splitParts)
                    {
                        if (!string.IsNullOrWhiteSpace(currentLine))
                        {
                            lines.Add(currentLine);
                            currentLine = "";
                        }
                        lines.Add(part);
                    }
                }
                else if (string.IsNullOrWhiteSpace(currentLine))
                {
                    currentLine = word;
                }
                else if (currentWidth + spaceWidth + wordWidth <= maxWidth - 35f)
                {
                    currentLine += " " + word;
                }
                else
                {
                    lines.Add(currentLine);
                    currentLine = word;
                }
            }

            if (!string.IsNullOrWhiteSpace(currentLine))
                lines.Add(currentLine);

            return lines;
        }


        private void DrawTextLinesInCell(StringBuilder sb, List<string> lines, float cellX, float cellY, float cellWidth, float cellHeight,
                                 string alignment, string fontKey, float fontSize, float lineHeight)
        {
            // Start from top inside the cell with a small offset
            float currentY = cellY + cellHeight - fontSize * 0.85f;

            foreach (var line in lines)
            {
                float lineWidth = PdfLayoutUtils.EstimateTextWidth(line, fontKey, fontSize);

                float minX = cellX + 1f;                       // padding left
                float maxX = cellX + cellWidth - 1f;           // padding right

                float lineX = alignment switch
                {
                    "center" => Math.Clamp(cellX + (cellWidth - lineWidth) / 2f, minX, maxX - lineWidth),
                    "right" => Math.Clamp(maxX - lineWidth, minX, maxX - lineWidth),
                    _ => minX
                };

                sb.AppendLine($"{lineX} {currentY} Td ({EscapeText(line)}) Tj");
                currentY -= lineHeight;
            }
        }
        private static string EscapeText(string text)
        {
            if (string.IsNullOrEmpty(text))
                return "";

            return text
                .Replace("\\", "\\\\")  // escape backslash
                .Replace("(", "\\(")    // escape (
                .Replace(")", "\\)");   // escape )
        }

        private List<string> SplitWordWithHyphen(string word, string fontKey, float fontSize, float maxWidth, float fudge)
        {
            var parts = new List<string>();
            int start = 0;

            while (start < word.Length)
            {
                int len = 1;
                while (start + len <= word.Length)
                {
                    string slice = word.Substring(start, len);
                    float width = PdfLayoutUtils.EstimateTextWidth(slice + "-", fontKey, fontSize);
                    if (width > maxWidth - fudge)
                        break;
                    len++;
                }

                len = Math.Max(1, len - 1);
                string piece = word.Substring(start, len);

                if (start + len < word.Length)
                    parts.Add(piece + "-");
                else
                    parts.Add(piece);

                start += len;
            }

            return parts;
        }


        private static List<string> SplitByCharacter(string word, string fontKey, float fontSize, float maxWidth)
        {
            var parts = new List<string>();
            string part = "";

            foreach (char c in word)
            {
                string next = part + c;
                float width = PdfLayoutUtils.EstimateTextWidth(next, fontKey, fontSize);

                if (width > maxWidth)
                {
                    if (!string.IsNullOrEmpty(part))
                        parts.Add(part);

                    part = c.ToString();
                }
                else
                {
                    part = next;
                }
            }

            if (!string.IsNullOrEmpty(part))
                parts.Add(part);

            return parts;
        }

        public static List<string> SplitCamelCase(string input)
        {
            if (string.IsNullOrEmpty(input))
                return new List<string>();

            var parts = new List<string>();
            int wordStart = 0;

            for (int i = 1; i < input.Length; i++)
            {
                if (char.IsUpper(input[i]) && !char.IsUpper(input[i - 1]))
                {
                    parts.Add(input.Substring(wordStart, i - wordStart));
                    wordStart = i;
                }
            }

            parts.Add(input.Substring(wordStart));
            return parts.Where(p => !string.IsNullOrWhiteSpace(p)).ToList();
        }


        // Helper for drawing ellipses/circles as clipping paths
        private void AppendEllipsePath(StringBuilder sb, float cx, float cy, float rx, float ry)
        {
            // Approximate an ellipse using 4 Bézier curves
            // See https://www.tinaja.com/glib/ellipse4.pdf
            float k = 0.552284749831f; // approximation constant
            float ox = rx * k;
            float oy = ry * k;
            float x0 = cx - rx;
            float y0 = cy;
            float x1 = cx;
            float y1 = cy - ry;
            float x2 = cx + rx;
            float y2 = cy;
            float x3 = cx;
            float y3 = cy + ry;

            sb.AppendLine($"{PdfNumber(cx - rx)} {PdfNumber(cy)} m");
            sb.AppendLine($"{PdfNumber(cx - rx)} {PdfNumber(cy - oy)} {PdfNumber(cx - ox)} {PdfNumber(cy - ry)} {PdfNumber(cx)} {PdfNumber(cy - ry)} c");
            sb.AppendLine($"{PdfNumber(cx + ox)} {PdfNumber(cy - ry)} {PdfNumber(cx + rx)} {PdfNumber(cy - oy)} {PdfNumber(cx + rx)} {PdfNumber(cy)} c");
            sb.AppendLine($"{PdfNumber(cx + rx)} {PdfNumber(cy + oy)} {PdfNumber(cx + ox)} {PdfNumber(cy + ry)} {PdfNumber(cx)} {PdfNumber(cy + ry)} c");
            sb.AppendLine($"{PdfNumber(cx - ox)} {PdfNumber(cy + ry)} {PdfNumber(cx - rx)} {PdfNumber(cy + oy)} {PdfNumber(cx - rx)} {PdfNumber(cy)} c");
            sb.AppendLine("h");
        }

        private string BuildExtGStateResourcesForPage(PdfPage page)
        {
            var usedOpacities = page.Elements
                .OfType<ImageElement>()
                .Where(e => e.Opacity < 1.0f && e.Opacity > 0f)
                .Select(e => e.Opacity)
                .Distinct()
                .ToList();
            if (!usedOpacities.Any())
                return "";

            var sb = new StringBuilder();
            sb.Append("/ExtGState << ");
            foreach (var opacity in usedOpacities)
            {
                int objId = _opacityExtGStates[opacity];
                sb.Append($"/GS_{objId} {objId} 0 R ");
            }
            sb.Append(">>");
            return sb.ToString();
        }

        // This now handles paddings, rounded corners, shadow, and border:
        private void AppendBackgroundBox(StringBuilder sb, TextElement txt, List<string> lines)
        {
            float padLeft = txt.PaddingLeft ?? 0;
            float padRight = txt.PaddingRight ?? 0;
            float padTop = txt.PaddingTop ?? 0;
            float padBottom = txt.PaddingBottom ?? 0;
            float lineHeight = txt.FontSize * txt.LineHeight;
            float textHeight = lines.Count * lineHeight;
            float fudge = 0.18f;
            float rectY = txt.Y - textHeight + lineHeight - txt.FontSize * fudge - padTop;
            float rectX = txt.X - padLeft;
            float width = lines.Any()
                    ? lines.Max(l => PdfLayoutUtils.EstimateTextWidth(l, FontManager.NormalizeFontKey(txt.FontFamily, txt.Bold, txt.Italic), txt.FontSize, txt.Monospace, txt.Bold))
                    : 0f;

            float height = textHeight + padTop + padBottom;
            if (txt.MaxWidth.HasValue)
            {
                float textWidth = lines.Any() ? lines.Max(line => PdfLayoutUtils.EstimateTextWidth(line, FontManager.NormalizeFontKey(txt.FontFamily, txt.Bold, txt.Italic), txt.FontSize, txt.Monospace)) : 0f;
                float fullWidth = textWidth + padLeft + padRight;

                if (txt.Alignment == TextAlignment.Center)
                {
                    // Move rectX to be horizontally centered in MaxWidth
                    rectX = txt.X + (txt.MaxWidth.Value - fullWidth) / 2f;
                }
                else if (txt.Alignment == TextAlignment.Right)
                {
                    // Move rectX so that the right edge of the background lines up with MaxWidth
                    rectX = txt.X + (txt.MaxWidth.Value - fullWidth);
                }
            }
            // Shadow (simple offset)
            if (!string.IsNullOrWhiteSpace(txt.BackgroundShadowColor) && (txt.BackgroundShadowOffsetX.HasValue || txt.BackgroundShadowOffsetY.HasValue))
            {
                float shadowX = rectX + (txt.BackgroundShadowOffsetX ?? 0);
                float shadowY = rectY + (txt.BackgroundShadowOffsetY ?? 0);
                sb.AppendLine($"{ConvertColor(txt.BackgroundShadowColor)} rg");
                sb.AppendLine("0.0 G");
                if (txt.BackgroundCornerRadius.HasValue && txt.BackgroundCornerRadius.Value > 0)
                {
                    AppendRoundedRect(sb, shadowX, shadowY, width, height, txt.BackgroundCornerRadius.Value);
                    sb.AppendLine("f");
                }
                else
                {
                    sb.AppendLine($"{PdfNumber(shadowX)} {PdfNumber(shadowY)} {PdfNumber(width)} {PdfNumber(height)} re f");
                }
            }

            // Background fill
            sb.AppendLine($"{ConvertColor(txt.BackgroundColor)} rg");
            sb.AppendLine("0.0 G");
            if (txt.BackgroundCornerRadius.HasValue && txt.BackgroundCornerRadius.Value > 0)
            {
                AppendRoundedRect(sb, rectX, rectY, width, height, txt.BackgroundCornerRadius.Value);
                sb.AppendLine("f");
            }
            else
            {
                sb.AppendLine($"{PdfNumber(rectX)} {PdfNumber(rectY)} {PdfNumber(width)} {PdfNumber(height)} re f");
            }

            // Border (if set)
            if (!string.IsNullOrEmpty(txt.BackgroundBorderColor) && txt.BackgroundBorderWidth > 0)
            {
                sb.AppendLine($"{ConvertColor(txt.BackgroundBorderColor)} RG");
                sb.AppendLine($"{PdfNumber(txt.BackgroundBorderWidth ?? 1)} w");
                if (txt.BackgroundCornerRadius.HasValue && txt.BackgroundCornerRadius.Value > 0)
                {
                    AppendRoundedRect(sb, rectX, rectY, width, height, txt.BackgroundCornerRadius.Value);
                    sb.AppendLine("S");
                }
                else
                {
                    sb.AppendLine($"{PdfNumber(rectX)} {PdfNumber(rectY)} {PdfNumber(width)} {PdfNumber(height)} re S");
                }
            }
        }

        private void AppendRoundedRect(StringBuilder sb, float x, float y, float width, float height, float r)
        {
            // This approximates a rounded rect using 4 Bézier curves
            float c = r * 0.5522847498f; // control point offset for a quarter circle
            float x0 = x, x1 = x + r, x2 = x + width - r, x3 = x + width;
            float y0 = y, y1 = y + r, y2 = y + height - r, y3 = y + height;

            sb.AppendLine($"{PdfNumber(x1)} {PdfNumber(y0)} m");
            sb.AppendLine($"{PdfNumber(x2)} {PdfNumber(y0)} l");
            sb.AppendLine($"{PdfNumber(x2 + c)} {PdfNumber(y0)} {PdfNumber(x3)} {PdfNumber(y1 - c)} {PdfNumber(x3)} {PdfNumber(y1)} c");
            sb.AppendLine($"{PdfNumber(x3)} {PdfNumber(y2)} l");
            sb.AppendLine($"{PdfNumber(x3)} {PdfNumber(y2 + c)} {PdfNumber(x2 + c)} {PdfNumber(y3)} {PdfNumber(x2)} {PdfNumber(y3)} c");
            sb.AppendLine($"{PdfNumber(x1)} {PdfNumber(y3)} l");
            sb.AppendLine($"{PdfNumber(x1 - c)} {PdfNumber(y3)} {PdfNumber(x0)} {PdfNumber(y2 + c)} {PdfNumber(x0)} {PdfNumber(y2)} c");
            sb.AppendLine($"{PdfNumber(x0)} {PdfNumber(y1)} l");
            sb.AppendLine($"{PdfNumber(x0)} {PdfNumber(y1 - c)} {PdfNumber(x1 - c)} {PdfNumber(y0)} {PdfNumber(x1)} {PdfNumber(y0)} c");
            sb.AppendLine("h");
        }




        private float CalculateXPosition(TextElement txt, string line)
        {
            float lineWidth = PdfLayoutUtils.EstimateTextWidth(line, FontManager.NormalizeFontKey(txt.FontFamily, txt.Bold, txt.Italic), txt.FontSize, txt.Monospace);
            float xPos = txt.X;

            if (txt.MaxWidth != null)
            {
                if (txt.Alignment == TextAlignment.Center)
                    xPos += (txt.MaxWidth.Value - lineWidth) / 2;
                else if (txt.Alignment == TextAlignment.Right)
                    xPos += (txt.MaxWidth.Value - lineWidth);
            }

            return xPos;
        }
        


        private void AppendRotation(StringBuilder sb, float x, float y, float degrees)
        {
            double radians = degrees * Math.PI / 180;
            double cos = Math.Cos(radians);
            double sin = Math.Sin(radians);
            sb.AppendLine($"{PdfNumber(cos)} {PdfNumber(sin)} {PdfNumber(-sin)} {PdfNumber(cos)} {PdfNumber(x)} {PdfNumber(y)} Tm");
        }

        private void AppendUnderline(StringBuilder sb, float x, float y, float width, TextElement txt, int fontId)
        {
            float underlineY = y - 2;
            sb.AppendLine("ET");
            sb.AppendLine($"{ConvertColor(txt.Color)} RG");
            sb.AppendLine($"{PdfNumber(x)} {PdfNumber(underlineY)} m {PdfNumber(x + width)} {PdfNumber(underlineY)} l S");
            sb.AppendLine("BT");
            sb.AppendLine($"/F{fontId} {txt.FontSize} Tf");
            sb.AppendLine($"{ConvertColor(txt.Color)} rg");
            sb.AppendLine($"{PdfNumber(x)} {PdfNumber(y)} Td");
        }

        private void AppendUnderlineElement(StringBuilder sb, UnderlineElement line)
        {
            sb.AppendLine("q");
            sb.AppendLine($"{ConvertColor(line.Color)} RG");
            sb.AppendLine($"{PdfNumber(line.Thickness)} w");

            if (line.Style == LineStyle.Dashed)
                sb.AppendLine("[3 2] 0 d");

            if (line.Rotation != 0)
            {
                double rad = line.Rotation * Math.PI / 180;
                sb.AppendLine($"{PdfNumber(Math.Cos(rad))} {PdfNumber(Math.Sin(rad))} {PdfNumber(-Math.Sin(rad))} {PdfNumber(Math.Cos(rad))} {PdfNumber(line.X)} {PdfNumber(line.Y)} cm");
                sb.AppendLine($"0 0 m {line.Width} 0 l S");
            }
            else
            {
                sb.AppendLine($"{PdfNumber(line.X)} {PdfNumber(line.Y)} m {PdfNumber(line.X + line.Width)} {PdfNumber(line.Y)} l S");
            }

            sb.AppendLine("Q");
        }

        private string GetFontKey(TextElement txt)
        {
            return FontManager.NormalizeFontKey(txt.FontFamily, txt.Bold, txt.Italic);
        }
        private string EscapePdfText(string text) =>
            text.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");

        public string ConvertColor(string color)
        {
            if (string.IsNullOrWhiteSpace(color) || color.Equals("black", StringComparison.OrdinalIgnoreCase))
                return "0 0 0";

            if (color.StartsWith("#") && color.Length == 7)
            {
                var r = int.Parse(color.Substring(1, 2), System.Globalization.NumberStyles.HexNumber) / 255f;
                var g = int.Parse(color.Substring(3, 2), System.Globalization.NumberStyles.HexNumber) / 255f;
                var b = int.Parse(color.Substring(5, 2), System.Globalization.NumberStyles.HexNumber) / 255f;
                return $"{PdfNumber(r)} {PdfNumber(g)} {PdfNumber(b)}";
            }

            return "0 0 0";
        }
        private IEnumerable<string> GetUsedFontKeys(PdfDocument doc)
        {
            var fontKeys = new HashSet<string>();

            // All TextElement instances (uses Bold/Italic)
            foreach (var textElem in doc.Pages.SelectMany(p => p.Elements.OfType<TextElement>()))
                fontKeys.Add(FontManager.NormalizeFontKey(textElem.FontFamily, textElem.Bold, textElem.Italic));

            // All TableElements
            foreach (var table in doc.Pages.SelectMany(p => p.Elements.OfType<TableElement>()))
            {
                // Headers
                foreach (var cell in table.HeaderCells)
                {
                    fontKeys.Add(FontManager.NormalizeFontKey(
                        cell.FontFamily ?? table.HeaderFontFamily ?? table.FontFamily ?? "Helvetica",
                        cell.Bold,
                        cell.Italic
                    ));
                }
                // Body rows
                foreach (var row in table.Rows)
                {
                    foreach (var cell in row)
                    {
                        fontKeys.Add(FontManager.NormalizeFontKey(
                            cell.FontFamily ?? table.FontFamily ?? "Helvetica",
                            cell.Bold,
                            cell.Italic
                        ));
                    }
                }
                // Footer
                if (table.FooterCells != null)
                {
                    foreach (var cell in table.FooterCells)
                    {
                        fontKeys.Add(FontManager.NormalizeFontKey(
                            cell.FontFamily ?? table.FooterFontFamily ?? table.FontFamily ?? "Helvetica",
                            cell.Bold,
                            cell.Italic
                        ));
                    }
                }
            }

            return fontKeys;
        }




    }
}
