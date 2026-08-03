using System;
using System.Collections.Generic;
using System.Linq;
using PdfBuilder.Document;
using PdfBuilder.Elements;
using PdfBuilder.Models;
using TableModels = PdfBuilder.Elements.Table;

namespace PdfBuilder.Writer
{
    internal static class TablePaginator
    {
        public static PdfDocument Paginate(PdfDocument doc)
        {
            var outDoc = new PdfDocument
            {
                Title = doc.Title,
                HeaderFooter = CloneHeaderFooter(doc.HeaderFooter) ?? new HeaderFooterSpec(),
                Master = CloneMaster(doc.Master) ?? new MasterPageSpec()
            };
            outDoc.OutputOptions.CopyFrom(doc.OutputOptions);
            outDoc.Metadata.CopyFrom(doc.Metadata);
            outDoc.TextDefaults.CopyFrom(doc.TextDefaults);
            foreach (var page in doc.Pages)
            {
                var splitPages = PaginatePage(page);
                outDoc.Pages.AddRange(splitPages);
            }
            return outDoc;
        }

        private static List<PdfPage> PaginatePage(PdfPage src)
        {
            var result = new List<PdfPage>();
            var current = NewPageLike(src);

            foreach (var el in src.Elements)
            {
                if (el is TableElement t &&
                    t.EnablePageBreaks &&
                    t.PageTopY.HasValue && t.PageBottomY.HasValue)
                {
                    float pageTop = t.PageTopY.Value;

                    foreach (var (isFirstSeg, segTable) in SplitTableIntoSegments(t))
                    {
                        // If this segment wants to start at page top and current page already has content,
                        // start a new page.
                        if (Math.Abs(segTable.Y - pageTop) <= 0.5f && current.Elements.Count > 0)
                        {
                            result.Add(current);
                            current = NewPageLike(src);
                        }

                        current.AddElement(segTable);
                    }
                }
                else
                {
                    current.AddElement(el);
                }
            }

            if (current.Elements.Count > 0) result.Add(current);
            return result;
        }

        private static IEnumerable<(bool, TableElement)> SplitTableIntoSegments(TableElement table)
        {
            int totalCols = table.Rows.Max(r => r.Cells.Sum(c => Math.Max(1, c.ColSpan)));
            float tableWidth = table.TableWidth ?? 500f;
            float[] colWidths =
                (table.ColumnWidths != null && table.ColumnWidths.Count == totalCols)
                ? table.ColumnWidths.ToArray()
                : Enumerable.Repeat(tableWidth / totalCols, totalCols).ToArray();

            // Column width overrides from ColumnStyles
            foreach (var cs in table.ColumnStyles)
            {
                if (cs.Index >= 0 && cs.Index < colWidths.Length && cs.OverrideWidth.HasValue)
                    colWidths[cs.Index] = cs.OverrideWidth.Value;
            }

            var rowHeights = ComputeRowHeights(table, colWidths);

            int headerCount = table.HeaderRowCount ?? CountLeadingHeaders(table);
            float headerHeight = SumRows(rowHeights, 0, headerCount);

            float pageTop = table.PageTopY!.Value;
            float pageBottom = table.PageBottomY!.Value;
            float pageCapacity = pageTop - pageBottom;

            // Caption consumes vertical space on the first segment only
            float captionHeight = 0f;
            if (!string.IsNullOrWhiteSpace(table.CaptionText))
            {
                float capSize = Math.Max(table.DefaultFontSize, 11f);
                captionHeight = capSize * PdfDefaults.LineHeightMultiplier + 4f;
            }

            // First segment capacity: from current Y to bottom
            float firstCapacity = table.Y - pageBottom;
            bool startOnNextPage = firstCapacity <= 0f;
            if (startOnNextPage) firstCapacity = pageCapacity;

            int start = 0;
            bool firstSegment = true;

            while (start < table.Rows.Count)
            {
                bool repeatHeader = (!firstSegment && table.RepeatHeaders && headerCount > 0);

                float cap = firstSegment ? firstCapacity : pageCapacity;
                float budget = Math.Max(0f, cap);

                // Reduce by caption if first segment (caption drawn above table)
                if (firstSegment && captionHeight > 0f)
                {
                    if (captionHeight > budget) captionHeight = Math.Min(captionHeight, budget);
                    budget -= captionHeight;
                }

                if (repeatHeader)
                {
                    if (headerHeight > budget)
                    {
                        // Emit header-only page to avoid infinite loops
                        var segHeaderOnly = CloneTableForRows(table, colWidths, 0, headerCount);
                        segHeaderOnly.Y = pageTop;
                        yield return (firstSegment && !startOnNextPage, segHeaderOnly);

                        firstSegment = false;
                        continue;
                    }
                    budget -= headerHeight;
                }

                int end = FindFittingBreakIndex(table, rowHeights, start, budget, headerCount);
                if (end < start) end = start;

                if (repeatHeader)
                {
                    var segHeader = CloneTableForRows(table, colWidths, 0, headerCount);
                    segHeader.Y = (firstSegment && !startOnNextPage) ? table.Y : pageTop;

                    // preserve caption only for very first segment
                    if (!(firstSegment && !startOnNextPage))
                        segHeader.CaptionText = null;

                    yield return (firstSegment && !startOnNextPage, segHeader);

                    var segSlice = CloneTableForRows(table, colWidths, start, end - start + 1);
                    segSlice.Y = segHeader.Y - headerHeight;

                    // first segment slice starts below caption
                    if (firstSegment && !startOnNextPage && !string.IsNullOrWhiteSpace(table.CaptionText))
                        segSlice.Y -= captionHeight;

                    yield return (false, segSlice);
                }
                else
                {
                    var seg = CloneTableForRows(table, colWidths, start, end - start + 1);
                    seg.Y = (firstSegment && !startOnNextPage) ? table.Y : pageTop;

                    if (!(firstSegment && !startOnNextPage))
                        seg.CaptionText = null; // caption only on the very first drawn piece

                    // shift for caption on very first piece
                    if (firstSegment && !startOnNextPage && !string.IsNullOrWhiteSpace(table.CaptionText))
                        seg.Y -= captionHeight;

                    yield return (firstSegment && !startOnNextPage, seg);
                }

                start = end + 1;
                firstSegment = false;
            }
        }

        private static int FindFittingBreakIndex(TableElement table, float[] rowHeights, int start, float budget, int headerCount)
        {
            int rowCount = table.Rows.Count;

            var blockedBreak = new bool[rowCount];
            var covered = new HashSet<(int row, int col)>();
            int totalCols = table.Rows.Max(r => r.Cells.Sum(c => Math.Max(1, c.ColSpan)));

            for (int r = 0; r < rowCount; r++)
            {
                int cIndex = 0;
                while (cIndex < totalCols && covered.Contains((r, cIndex))) cIndex++;
                foreach (var cell in table.Rows[r].Cells)
                {
                    while (cIndex < totalCols && covered.Contains((r, cIndex))) cIndex++;
                    int rs = Math.Max(1, cell.RowSpan);
                    int cs = Math.Max(1, cell.ColSpan);

                    for (int k = r; k < Math.Min(rowCount - 1, r + rs - 1); k++)
                        blockedBreak[k] = true;

                    for (int rr = 0; rr < rs; rr++)
                        for (int cc = 0; cc < cs; cc++)
                            if (!(rr == 0 && cc == 0))
                                covered.Add((r + rr, cIndex + cc));

                    cIndex += cs;
                }
            }

            float acc = 0f;
            int end = start - 1;

            for (int r = start; r < rowCount; r++)
            {
                acc += rowHeights[r];
                if (acc - 1e-3f <= budget)
                {
                    if (!blockedBreak[r] || r == rowCount - 1)
                        end = r;
                }
                else break;
            }

            return end;
        }

        private static float[] ComputeRowHeights(TableElement table, float[] colWidths)
        {
            int totalCols = colWidths.Length;
            int rowCount = table.Rows.Count;
            var heights = new float[rowCount];

            for (int r = 0; r < rowCount; r++)
            {
                heights[r] = table.Rows[r].RowHeight
                    ?? (table.DefaultFontSize * PdfDefaults.LineHeightMultiplier + table.CellPadding * 2);
            }

            var covered = new HashSet<(int row, int col)>();

            for (int rowIndex = 0; rowIndex < rowCount; rowIndex++)
            {
                var row = table.Rows[rowIndex];
                int colIndex = 0;

                while (colIndex < totalCols && covered.Contains((rowIndex, colIndex))) colIndex++;

                foreach (var cell in row.Cells)
                {
                    while (colIndex < totalCols && covered.Contains((rowIndex, colIndex))) colIndex++;

                    int colSpan = Math.Max(1, cell.ColSpan);
                    int rowSpan = Math.Max(1, cell.RowSpan);

                    float cw = 0f;
                    for (int c = 0; c < colSpan; c++) cw += colWidths[colIndex + c];

                    float required = MeasureCellContentHeight(table, cell, cw);

                    if (rowSpan == 1)
                    {
                        if (required > heights[rowIndex]) heights[rowIndex] = required;
                    }
                    else
                    {
                        int lastRow = Math.Min(rowCount - 1, rowIndex + rowSpan - 1);
                        float sum = 0f;
                        for (int r = rowIndex; r <= lastRow; r++) sum += heights[r];

                        if (required > sum)
                        {
                            float deficit = required - sum;
                            float per = deficit / (lastRow - rowIndex + 1);
                            for (int r = rowIndex; r <= lastRow; r++) heights[r] += per;
                        }
                    }

                    for (int r = 0; r < rowSpan; r++)
                        for (int c = 0; c < colSpan; c++)
                            if (!(r == 0 && c == 0))
                                covered.Add((rowIndex + r, colIndex + c));

                    colIndex += colSpan;
                }
            }

            return heights;
        }

        private static float MeasureCellContentHeight(TableElement table, TableCell cell, float cellWidth)
        {
            float tablePad = table.CellPadding;
            float padTop = cell.PaddingTop ?? cell.Padding ?? tablePad;
            float padBottom = cell.PaddingBottom ?? cell.Padding ?? tablePad;
            float padLeft = cell.PaddingLeft ?? cell.Padding ?? tablePad;
            float padRight = cell.PaddingRight ?? cell.Padding ?? tablePad;

            string font = string.IsNullOrWhiteSpace(cell.Font) ? table.DefaultFont : cell.Font;
            float size = cell.FontSize > 0 ? cell.FontSize : table.DefaultFontSize;
            float lineMult = cell.LineHeight ?? PdfDefaults.LineHeightMultiplier;
            float usable = Math.Max(0, cellWidth - (padLeft + padRight));
            float lineH = size * lineMult;

            bool rotated = Math.Abs(cell.RotationDegrees) > 0.01f;

            if (table.OverflowPolicy == CellOverflowPolicy.Wrap)
            {
                var lines = PdfLayoutUtils.WrapText(cell.Text ?? string.Empty, font, size, usable);
                if (cell.WordBreak == CellWordBreak.BreakWord)
                    lines = ForceBreakLongLines(lines, font, size, usable);
                if (cell.MaxLines.HasValue && cell.MaxLines.Value > 0 && lines.Count > cell.MaxLines.Value)
                    lines = lines.Take(cell.MaxLines.Value).ToList();

                float stackH = Math.Max(lineH, lines.Count * lineH);

                if (rotated)
                {
                    float maxW = 0f;
                    foreach (var ln in lines)
                        maxW = Math.Max(maxW, PdfLayoutUtils.EstimateTextWidth(ln ?? string.Empty, font, size));
                    float req = RotatedBBoxHeight(maxW, stackH, cell.RotationDegrees);
                    return req + padTop + padBottom;
                }
                return stackH + padTop + padBottom;
            }
            else
            {
                string line = cell.Text ?? string.Empty;
                float w = PdfLayoutUtils.EstimateTextWidth(line, font, size);
                float h = rotated ? RotatedBBoxHeight(w, lineH, cell.RotationDegrees) : lineH;
                return h + padTop + padBottom;
            }
        }



        private static List<string> ForceBreakLongLines(IReadOnlyList<string> lines, string font, float size, float maxWidth)
        {
            var outLines = new List<string>(lines.Count);
            foreach (var line in lines)
            {
                if (PdfLayoutUtils.EstimateTextWidth(line ?? "", font, size) <= maxWidth)
                {
                    outLines.Add(line ?? string.Empty);
                    continue;
                }

                var sbLine = new System.Text.StringBuilder();
                foreach (var ch in line ?? string.Empty)
                {
                    sbLine.Append(ch);
                    if (PdfLayoutUtils.EstimateTextWidth(sbLine.ToString(), font, size) > maxWidth)
                    {
                        if (sbLine.Length > 1)
                        {
                            var flush = sbLine.ToString(0, sbLine.Length - 1);
                            if (flush.Length > 0) outLines.Add(flush);
                            sbLine.Clear();
                            sbLine.Append(ch);
                        }
                        else
                        {
                            outLines.Add(sbLine.ToString());
                            sbLine.Clear();
                        }
                    }
                }
                if (sbLine.Length > 0) outLines.Add(sbLine.ToString());
            }
            return outLines;
        }

        private static int CountLeadingHeaders(TableElement table)
        {
            int count = 0;
            foreach (var row in table.Rows)
            {
                if (row.IsHeader) count++; else break;
            }
            return count;
        }

        private static float SumRows(float[] heights, int start, int endExcl)
        {
            float s = 0f;
            for (int i = start; i < endExcl; i++) s += heights[i];
            return s;
        }

        private static PdfPage NewPageLike(PdfPage src)
        {
            var p = new PdfPage(src.Width, src.Height)
            {
                BackgroundColor = src.BackgroundColor,
                MarginTop = src.MarginTop,
                MarginBottom = src.MarginBottom,
                MarginLeft = src.MarginLeft,
                MarginRight = src.MarginRight,
                HeaderFooterOverride = CloneHeaderFooter(src.HeaderFooterOverride),
                MasterOverride = CloneMaster(src.MasterOverride),
                Columns = src.Columns == null ? null : new ColumnLayoutSpec
                {
                    Columns = src.Columns.Columns,
                    Gutter = src.Columns.Gutter,
                    Widths = src.Columns.Widths?.ToArray()
                },
                TextDefaults = src.TextDefaults.Clone()
            };
            return p;
        }

        private static HeaderFooterSpec? CloneHeaderFooter(HeaderFooterSpec? source)
        {
            if (source == null) return null;
            return new HeaderFooterSpec
            {
                HeaderTemplate = source.HeaderTemplate,
                FooterTemplate = source.FooterTemplate,
                HeaderHeight = source.HeaderHeight,
                FooterHeight = source.FooterHeight,
                FontFamily = source.FontFamily,
                FontSize = source.FontSize,
                Color = source.Color,
                HeaderAlign = source.HeaderAlign,
                FooterAlign = source.FooterAlign,
                FirstPageDifferent = source.FirstPageDifferent,
                FirstPageHeaderTemplate = source.FirstPageHeaderTemplate,
                FirstPageFooterTemplate = source.FirstPageFooterTemplate,
                HideOnLastPage = source.HideOnLastPage,
                HeaderLayout = source.HeaderLayout?.Clone(),
                FooterLayout = source.FooterLayout?.Clone()
            };
        }

        private static MasterPageSpec? CloneMaster(MasterPageSpec? source)
        {
            if (source == null) return null;
            return new MasterPageSpec
            {
                BackgroundColor = source.BackgroundColor,
                BackgroundImage = source.BackgroundImage,
                BackgroundImageMime = source.BackgroundImageMime,
                BackgroundImageX = source.BackgroundImageX,
                BackgroundImageY = source.BackgroundImageY,
                BackgroundImageWidth = source.BackgroundImageWidth,
                BackgroundImageHeight = source.BackgroundImageHeight,
                Watermark = source.Watermark == null ? null : new WatermarkSpec
                {
                    Text = source.Watermark.Text,
                    ImageData = source.Watermark.ImageData,
                    ImageMime = source.Watermark.ImageMime,
                    CenterOnPage = source.Watermark.CenterOnPage,
                    FontFamily = source.Watermark.FontFamily,
                    FontSize = source.Watermark.FontSize,
                    Color = source.Watermark.Color,
                    Opacity = source.Watermark.Opacity,
                    RotationDegrees = source.Watermark.RotationDegrees,
                    ImageWidth = source.Watermark.ImageWidth,
                    ImageHeight = source.Watermark.ImageHeight,
                    X = source.Watermark.X,
                    Y = source.Watermark.Y,
                    Layer = source.Watermark.Layer,
                    ExtGStateResourceName = source.Watermark.ExtGStateResourceName
                }
            };
        }

        private static TableElement CloneTableForRows(TableElement src, float[] colWidths, int startRow, int count)
        {
            var clone = new TableElement(src.X, src.Y)
            {
                TableWidth = src.TableWidth,
                ColumnWidths = src.ColumnWidths?.ToList() ?? new List<float>(),
                DefaultFont = src.DefaultFont,
                DefaultFontSize = src.DefaultFontSize,
                CellPadding = src.CellPadding,
                BorderCollapse = src.BorderCollapse,
                BorderStyle = src.BorderStyle?.Clone(),
                OuterBorder = src.OuterBorder?.Clone(),
                InnerBorder = src.InnerBorder?.Clone(),
                RowBanding = src.RowBanding?.Clone(),
                ColumnBanding = src.ColumnBanding?.Clone(),
                DefaultTextStyle = src.DefaultTextStyle?.Clone() ?? new TableModels.TextStyle(),
                OuterCornerRadiusTopLeft = src.OuterCornerRadiusTopLeft,
                OuterCornerRadiusTopRight = src.OuterCornerRadiusTopRight,
                OuterCornerRadiusBottomRight = src.OuterCornerRadiusBottomRight,
                OuterCornerRadiusBottomLeft = src.OuterCornerRadiusBottomLeft,
                RowBandOffset = src.RowBandOffset + startRow,

                BorderColor = src.BorderColor,
                BorderWidth = src.BorderWidth,
                HeaderBackground = src.HeaderBackground,
                AltRowBackground = src.AltRowBackground,
                AltRowEvery = src.AltRowEvery,
                AltRowStartIndex = src.AltRowStartIndex,

                CaptionText = src.CaptionText,
                CaptionAlign = src.CaptionAlign,

                // pagination flags disabled for split segments
                EnablePageBreaks = false,
                RepeatHeaders = src.RepeatHeaders,
                MinRowsAtPageStart = src.MinRowsAtPageStart,
                MinRowsAtPageEnd = src.MinRowsAtPageEnd,
                PageTopY = src.PageTopY,
                PageBottomY = src.PageBottomY,
                OnPageBreak = null,
                HeaderRowCount = src.HeaderRowCount,
                ResolveBorderConflicts = src.ResolveBorderConflicts,
                DrawOuterFrame = src.DrawOuterFrame,
                OuterFrameColor = src.OuterFrameColor,
                OuterFrameWidth = src.OuterFrameWidth,
                OverflowPolicy = src.OverflowPolicy,

                AutoSizeColumns = src.AutoSizeColumns, // <- carry through
                ColumnStyles = src.ColumnStyles.Select(cs => new TableColumnStyle
                {
                    Index = cs.Index,
                    HAlign = cs.HAlign,
                    VAlign = cs.VAlign,
                    Font = cs.Font,
                    FontSize = cs.FontSize,
                    TextColor = cs.TextColor,
                    Background = cs.Background,
                    PaddingTop = cs.PaddingTop,
                    PaddingRight = cs.PaddingRight,
                    PaddingBottom = cs.PaddingBottom,
                    PaddingLeft = cs.PaddingLeft,
                    OverrideWidth = cs.OverrideWidth
                }).ToList()
            };

            for (int i = 0; i < count; i++)
                clone.Rows.Add(DeepCloneRow(src.Rows[startRow + i]));

            return clone;
        }

        private static TableRow DeepCloneRow(TableRow r)
        {
            var nr = new TableRow
            {
                IsHeader = r.IsHeader,
                BackgroundColor = r.BackgroundColor,
                RowHeight = r.RowHeight,
                KeepWithNext = r.KeepWithNext,
                ThickTopBorder = r.ThickTopBorder,
                ThickBottomBorder = r.ThickBottomBorder,
                ThickBorderWidth = r.ThickBorderWidth,
                ThickBorderColor = r.ThickBorderColor
            };
            foreach (var c in r.Cells) nr.Cells.Add(DeepCloneCell(c));
            return nr;
        }

        private static TableCell DeepCloneCell(TableCell c) => new TableCell
        {
            Text = c.Text,
            TextRuns = c.TextRuns.Select(run => run.Clone()).ToList(),
            TextStyle = c.TextStyle?.Clone(),

            // Typography
            Font = c.Font,
            FontSize = c.FontSize,
            TextColor = c.TextColor,
            Bold = c.Bold,
            Italic = c.Italic,
            Underline = c.Underline,
            Strikethrough = c.Strikethrough,
            Overline = c.Overline,
            SmallCaps = c.SmallCaps,
            LineHeight = c.LineHeight,
            MaxLines = c.MaxLines,
            WordBreak = c.WordBreak,
            RotationDegrees = c.RotationDegrees,

            // Alignment
            HorizontalAlign = c.HorizontalAlign,
            VerticalAlign = c.VerticalAlign,

            // Background & corners
            BackgroundColor = c.BackgroundColor,
            CornerRadius = c.CornerRadius,
            CornerRadiusTopLeft = c.CornerRadiusTopLeft,
            CornerRadiusTopRight = c.CornerRadiusTopRight,
            CornerRadiusBottomRight = c.CornerRadiusBottomRight,
            CornerRadiusBottomLeft = c.CornerRadiusBottomLeft,

            // Base border (table defaults)
            BorderColor = c.BorderColor,
            BorderWidth = c.BorderWidth,
            BorderStyle = c.BorderStyle?.Clone(),
            BorderTop = c.BorderTop,
            BorderRight = c.BorderRight,
            BorderBottom = c.BorderBottom,
            BorderLeft = c.BorderLeft,

            // *** IMPORTANT: per-side border overrides ***
            BorderColorTop = c.BorderColorTop,
            BorderColorRight = c.BorderColorRight,
            BorderColorBottom = c.BorderColorBottom,
            BorderColorLeft = c.BorderColorLeft,
            BorderWidthTop = c.BorderWidthTop,
            BorderWidthRight = c.BorderWidthRight,
            BorderWidthBottom = c.BorderWidthBottom,
            BorderWidthLeft = c.BorderWidthLeft,
            BorderStyleTop = c.BorderStyleTop?.Clone(),
            BorderStyleRight = c.BorderStyleRight?.Clone(),
            BorderStyleBottom = c.BorderStyleBottom?.Clone(),
            BorderStyleLeft = c.BorderStyleLeft?.Clone(),

            // Padding
            Padding = c.Padding,
            PaddingTop = c.PaddingTop,
            PaddingRight = c.PaddingRight,
            PaddingBottom = c.PaddingBottom,
            PaddingLeft = c.PaddingLeft,

            // Spans
            ColSpan = c.ColSpan,
            RowSpan = c.RowSpan
        };

        private static float RotatedBBoxHeight(float textWidth, float unrotatedHeight, float angleDeg)
        {
            double r = Math.Abs(angleDeg) * Math.PI / 180.0;
            return (float)(Math.Abs(textWidth * Math.Sin(r)) + Math.Abs(unrotatedHeight * Math.Cos(r)));
        }

    }
}





