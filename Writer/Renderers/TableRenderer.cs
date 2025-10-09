using PdfBuilder.Document;
using PdfBuilder.Elements;
using PdfBuilder.Encoder;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Text;

namespace PdfBuilder.Writer
{
    public static class TableRenderer
    {
        private static readonly IFormatProvider Inv = CultureInfo.InvariantCulture;
        private static string N(double v) => v.ToString("0.###", Inv);
        private enum Axis { Horizontal, Vertical }
        static float ClampThin(float w) => Math.Max(0.25f, w);
        static float AlignHalf(float v) => (float)Math.Round(v * 2f) / 2f;

        private static bool PreferSecondOnTie(Axis axis)
        {
            // CSS collapsed borders: bottom beats top, right beats left
            return axis == Axis.Horizontal /*top vs bottom*/ || axis == Axis.Vertical /*left vs right*/;
        }

        private sealed class Edge
        {
            public bool Exists;
            public float Width;
            public Color Color;
            public int OriginRank; // cell > row > table (lower is stronger)
            public string Style;   // "solid","dashed", etc. (optional; treat as equal if not used)
        }

        // Compare a and b; return +1 if b wins, 0 if a wins, -1 if none (no competitor)
        private static int CompareEdges(Edge a, Edge b, Axis axis)
        {
            if (b == null || !b.Exists) return -1;
            if (!a.Exists) return +1;

            // (1) style precedence (optional — if not tracking, skip)
            // same style => (2) thicker wins
            if (Math.Abs(a.Width - b.Width) > 1e-3f)
                return b.Width > a.Width ? +1 : 0;

            // (3) origin precedence: lower OriginRank wins
            if (a.OriginRank != b.OriginRank)
                return b.OriginRank < a.OriginRank ? +1 : 0;

            // (4) final tie-break: prefer bottom over top; right over left
            return PreferSecondOnTie(axis) ? +1 : 0;
        }

        public static void Append(StringBuilder sb, TableElement table, Dictionary<string, int> fontObjId)
        {
            if (table == null || table.Rows == null || table.Rows.Count == 0) return;

            // local helper: a side is "explicit" if caller set color and/or width on that side
            static bool IsExplicitSide(Color? c, float? w) => c.HasValue || w.HasValue && w.Value > 0f;

            sb.Append("q\n");           // isolate graphics state
            sb.Append("0 J 0 j\n");     // butt caps, miter joins

            // —— Geometry: columns ——
            int totalCols = table.Rows.Max(r => r.Cells.Sum(c => Math.Max(1, c.ColSpan)));
            float tableWidth = table.TableWidth ?? 500f;

            float[] colWidths;
            bool useFixed =
                table.ColumnWidths != null &&
                table.ColumnWidths.Count == totalCols &&
                table.ColumnWidths.All(w => w > 0f);

            if (useFixed || table.AutoSizeColumns == false)
            {
                colWidths = useFixed
                    ? table.ColumnWidths.ToArray()
                    : Enumerable.Repeat(tableWidth / Math.Max(1, totalCols), totalCols).ToArray();
            }
            else
            {
                colWidths = AutoSizeColumnWidths(table, totalCols, tableWidth);
            }

            // —— Heights (row/colspan-aware) ——
            var rowHeights = ComputeRowHeights(table, colWidths);

            // —— Caption ——
            float y = table.Y;
            if (!string.IsNullOrWhiteSpace(table.CaptionText))
            {
                string capFont = table.DefaultFont;
                float capSize = Math.Max(table.DefaultFontSize, 11f);
                float lineH = capSize * PdfDefaults.LineHeightMultiplier;

                float textWidth = PdfLayoutUtils.EstimateTextWidth(table.CaptionText, capFont, capSize);
                float totalWidth = colWidths.Sum();
                float xCap = table.X;
                if (table.CaptionAlign == HorizontalAlign.Center)
                    xCap = table.X + Math.Max(0, (totalWidth - textWidth) / 2f);
                else if (table.CaptionAlign == HorizontalAlign.Right)
                    xCap = table.X + Math.Max(0, totalWidth - textWidth);

                int fId = ResolveFontId(fontObjId, MapFontVariant(capFont, bold: true, italic: false));
                sb.Append("BT ");
                sb.Append($"/F{fId} {N(capSize)} Tf ");
                sb.Append($"{ToRgbFill(Color.Black)} ");
                sb.Append($"{N(xCap)} {N(y)} Td ");
                sb.Append($"{PdfEnc.WinAnsiHex(table.CaptionText)} Tj ET\n");

                y -= lineH + 4; // spacing under caption
            }

            // —— Outer frame (behind cells so cell borders stay visible) ——
            if (table.DrawOuterFrame)
            {
                float frameW = colWidths.Sum();
                float frameH = rowHeights.Sum();
                StrokeRect(sb, table.X, y, frameW, frameH, table.OuterFrameColor, table.OuterFrameWidth);
            }

            // —— Alternating rows (OPT-IN) ——
            bool zebraEnabled = table.AltRowBackground.HasValue && table.AltRowEvery > 0;
            int zebraEvery = zebraEnabled ? Math.Max(1, table.AltRowEvery) : int.MaxValue;
            int zebraStart = Math.Max(0, table.AltRowStartIndex);
            int bodyRowCounter = 0;

            var covered = new HashSet<(int row, int col)>();
            int rowIndex = 0;

            // —— Rows ——
            while (rowIndex < table.Rows.Count)
            {
                float rowHeight = rowHeights[rowIndex];
                float x = table.X;
                int colIndex = 0;

                while (colIndex < totalCols && covered.Contains((rowIndex, colIndex)))
                {
                    x += colWidths[colIndex];
                    colIndex++;
                }

                var row = table.Rows[rowIndex];

                // Row background: explicit -> header -> zebra (only if enabled)
                bool isAlt = zebraEnabled
                             && !row.IsHeader
                             && bodyRowCounter >= zebraStart
                             && (bodyRowCounter - zebraStart) % zebraEvery == 0;

                Color? rowBg = row.BackgroundColor
                               ?? (row.IsHeader ? table.HeaderBackground
                                                : isAlt ? table.AltRowBackground : null);

                // Cells
                for (int i = 0; i < row.Cells.Count; i++)
                {
                    var cell = row.Cells[i];

                    while (colIndex < totalCols && covered.Contains((rowIndex, colIndex)))
                    {
                        x += colWidths[colIndex];
                        colIndex++;
                    }

                    int colSpan = Math.Max(1, cell.ColSpan);
                    int rowSpan = Math.Max(1, cell.RowSpan);

                    float cellWidth = 0f;
                    for (int s = 0; s < colSpan; s++) cellWidth += colWidths[colIndex + s];

                    float cellHeight = 0f;
                    for (int r = 0; r < rowSpan && rowIndex + r < rowHeights.Length; r++)
                        cellHeight += rowHeights[rowIndex + r];

                    // Effective style (column defaults -> cell overrides)
                    var eff = BuildEffectiveCell(table, cell, colIndex);

                    // Background
                    var bg = eff.BackgroundColor ?? rowBg;
                    if (bg.HasValue)
                        FillRect(sb, x, y - cellHeight, cellWidth, cellHeight, bg.Value);

                    // ----- Border drawing -----
                    if (table.ResolveBorderConflicts)
                    {
                        int logicalColIndex = colIndex;

                        // TOP: per-slot winner (mixed neighbors handled)
                        float xSegTop = x;
                        for (int s = 0; s < colSpan; s++)
                        {
                            float segW = colWidths[logicalColIndex + s];

                            BuildTopVsAboveBottom(
                                table, rowIndex, logicalColIndex + s, covered,
                                eff.BorderTop, eff.BorderColorTop, eff.BorderWidthTop,
                                out var top, out var aboveBottom);

                            // Draw TOP only if it wins. If it loses, previous row already drew its BOTTOM.
                            var topWins = CompareEdges(top, aboveBottom, Axis.Horizontal) <= 0 ? top : null;
                            if (topWins != null && topWins.Exists)
                                StrokeLine(sb, xSegTop, y, xSegTop + segW, y, topWins.Color, topWins.Width);

                            xSegTop += segW;
                        }

                        // VERTICALS: draw each shared boundary once at x+cellWidth (RIGHT owns)
                        if (logicalColIndex == 0 && eff.BorderLeft)
                            StrokeLine(sb, x, y, x, y - cellHeight, eff.BorderColorLeft, eff.BorderWidthLeft);

                        BuildRightVsNeighborLeft(
                            table, row, logicalColIndex,
                            eff.BorderRight, eff.BorderColorRight, eff.BorderWidthRight,
                            out var right, out var neighborLeft);

                        Edge vWinner;
                        if (!neighborLeft.Exists)
                        {
                            vWinner = right;
                        }
                        else
                        {
                            int cmp = CompareEdges(neighborLeft, right, Axis.Vertical); // right wins ties
                            vWinner = cmp > 0 ? right : neighborLeft;
                        }
                        if (vWinner.Exists)
                            StrokeLine(sb, x + cellWidth, y, x + cellWidth, y - cellHeight, vWinner.Color, vWinner.Width);

                        // BOTTOM: per-slot winner and draw once here
                        float xSeg = x;
                        for (int s = 0; s < colSpan; s++)
                        {
                            float segW = colWidths[logicalColIndex + s];

                            BuildBottomVsBelowTop(
                                table, rowIndex, logicalColIndex + s, rowSpan, covered,
                                eff.BorderBottom, eff.BorderColorBottom, eff.BorderWidthBottom,
                                out var bottom, out var belowTop);

                            // Bottom vs below's Top; ties → Bottom
                            int cmpH = CompareEdges(bottom, belowTop, Axis.Horizontal);
                            if (cmpH <= 0 && bottom.Exists)
                                StrokeLine(sb, xSeg, y - cellHeight, xSeg + segW, y - cellHeight, bottom.Color, bottom.Width);

                            xSeg += segW;
                        }
                    }
                    else
                    {
                        // Non-conflict mode: draw what the cell asks for using effective per-side values,
                        // BUT skip any explicit sides here — we'll overlay those right after this branch.
                        bool topExp = IsExplicitSide(cell.BorderColorTop, cell.BorderWidthTop);
                        bool rightExp = IsExplicitSide(cell.BorderColorRight, cell.BorderWidthRight);
                        bool bottomExp = IsExplicitSide(cell.BorderColorBottom, cell.BorderWidthBottom);
                        bool leftExp = IsExplicitSide(cell.BorderColorLeft, cell.BorderWidthLeft);

                        if (cell.BorderTop && !topExp)
                            StrokeLine(sb, x, y, x + cellWidth, y, eff.BorderColorTop, eff.BorderWidthTop);
                        if (cell.BorderRight && !rightExp)
                            StrokeLine(sb, x + cellWidth, y, x + cellWidth, y - cellHeight, eff.BorderColorRight, eff.BorderWidthRight);
                        if (cell.BorderBottom && !bottomExp)
                            StrokeLine(sb, x, y - cellHeight, x + cellWidth, y - cellHeight, eff.BorderColorBottom, eff.BorderWidthBottom);
                        if (cell.BorderLeft && !leftExp)
                            StrokeLine(sb, x, y, x, y - cellHeight, eff.BorderColorLeft, eff.BorderWidthLeft);
                    }

                    // ===== Overlay: draw *explicit* per-side overrides LAST so colors always show =====
                    bool expTop = IsExplicitSide(cell.BorderColorTop, cell.BorderWidthTop);
                    bool expRight = IsExplicitSide(cell.BorderColorRight, cell.BorderWidthRight);
                    bool expBottom = IsExplicitSide(cell.BorderColorBottom, cell.BorderWidthBottom);
                    bool expLeft = IsExplicitSide(cell.BorderColorLeft, cell.BorderWidthLeft);

                    if (cell.BorderTop && expTop)
                        StrokeLine(sb, x, y, x + cellWidth, y, eff.BorderColorTop, eff.BorderWidthTop);          // e.g. #22bb77 → 0.133 0.733 0.467 RG
                    if (cell.BorderRight && expRight)
                        StrokeLine(sb, x + cellWidth, y, x + cellWidth, y - cellHeight, eff.BorderColorRight, eff.BorderWidthRight); // e.g. #dd3333 → 0.867 0.2 0.2 RG
                    if (cell.BorderBottom && expBottom)
                        StrokeLine(sb, x, y - cellHeight, x + cellWidth, y - cellHeight, eff.BorderColorBottom, eff.BorderWidthBottom);
                    if (cell.BorderLeft && expLeft)
                        StrokeLine(sb, x, y, x, y - cellHeight, eff.BorderColorLeft, eff.BorderWidthLeft);

                    // ----- Text -----
                    if (!string.IsNullOrEmpty(eff.Text))
                    {
                        var pad = GetPadding(eff, table.CellPadding);
                        string baseFontName = MapFontVariant(eff.Font, eff.Bold, eff.Italic);
                        int fontId = ResolveFontId(fontObjId, baseFontName);
                        var rgbFill = ToRgbFill(eff.TextColor);

                        // Clip to cell rect
                        sb.Append("q ");
                        sb.Append($"{N(x)} {N(y - cellHeight)} {N(cellWidth)} {N(cellHeight)} re W n\n");

                        RenderCellText(sb, table, eff, x, y, cellWidth, cellHeight, pad,
                                       eff.Font, eff.FontSize, eff.LineHeight ?? PdfDefaults.LineHeightMultiplier,
                                       eff.MaxLines, eff.WordBreak, eff.RotationDegrees,
                                       eff.HorizontalAlign, eff.VerticalAlign, fontId, rgbFill);

                        sb.Append("Q\n");
                    }

                    // Span coverage
                    if (rowSpan > 1 || colSpan > 1)
                        for (int rr = 0; rr < rowSpan; rr++)
                            for (int cc = 0; cc < colSpan; cc++)
                                if (!(rr == 0 && cc == 0))
                                    covered.Add((rowIndex + rr, colIndex + cc));

                    x += cellWidth;
                    colIndex += colSpan;
                }

                y -= rowHeight;

                if (!row.IsHeader) bodyRowCounter++;
                rowIndex++;
            }

            sb.Append("Q\n");
        }





        // ---------- Effective cell (column defaults + overrides) ----------
        private sealed class Effective
        {
            public string Text = "";
            public string Font = "Helvetica";
            public float FontSize = 10f;
            public Color TextColor = Color.Black;

            public bool Bold, Italic, Underline, Strikethrough, Overline, SmallCaps;
            public float? LineHeight;
            public int? MaxLines;
            public CellWordBreak WordBreak = CellWordBreak.Normal;
            public float RotationDegrees = 0f;

            public HorizontalAlign HorizontalAlign = HorizontalAlign.Left;
            public VerticalAlign VerticalAlign = VerticalAlign.Top;

            public Color? BackgroundColor;
            public float CornerRadius = 0f;

            // cell-wide defaults (used as baseline for sides)
            public Color BorderColor = Color.Black;
            public float BorderWidth = PdfDefaults.DefaultBorderWidth;

            // side enable flags (come from the cell)
            public bool BorderTop = true, BorderRight = true, BorderBottom = true, BorderLeft = true;

            // per-side actual color/width used when drawing
            public Color BorderColorTop, BorderColorRight, BorderColorBottom, BorderColorLeft;
            public float BorderWidthTop, BorderWidthRight, BorderWidthBottom, BorderWidthLeft;

            // padding
            public float? Padding; public float? PaddingTop, PaddingRight, PaddingBottom, PaddingLeft;
        }


        private static Effective BuildEffectiveCell(TableElement table, TableCell cell, int columnIndex)
        {
            var e = new Effective
            {
                Text = cell.Text ?? "",
                Font = string.IsNullOrWhiteSpace(cell.Font) ? table.DefaultFont : cell.Font,
                FontSize = cell.FontSize > 0 ? cell.FontSize : table.DefaultFontSize,
                TextColor = cell.TextColor,

                Bold = cell.Bold,
                Italic = cell.Italic,
                Underline = cell.Underline,
                Strikethrough = cell.Strikethrough,
                Overline = cell.Overline,
                SmallCaps = cell.SmallCaps,
                LineHeight = cell.LineHeight,
                MaxLines = cell.MaxLines,
                WordBreak = cell.WordBreak,
                RotationDegrees = cell.RotationDegrees,

                HorizontalAlign = cell.HorizontalAlign,
                VerticalAlign = cell.VerticalAlign,

                BackgroundColor = cell.BackgroundColor,
                CornerRadius = cell.CornerRadius,

                // baseline for borders comes from TABLE, not the cell's default black
                BorderColor = table.BorderColor,
                BorderWidth = cell.BorderWidth > 0 ? cell.BorderWidth
                            : table.BorderWidth > 0 ? table.BorderWidth : PdfDefaults.DefaultBorderWidth,

                BorderTop = cell.BorderTop,
                BorderRight = cell.BorderRight,
                BorderBottom = cell.BorderBottom,
                BorderLeft = cell.BorderLeft,

                Padding = cell.Padding,
                PaddingTop = cell.PaddingTop,
                PaddingRight = cell.PaddingRight,
                PaddingBottom = cell.PaddingBottom,
                PaddingLeft = cell.PaddingLeft,
            };

            // ---- per-side colors/widths (fallback to table baseline unless explicitly set) ----
            e.BorderColorTop = cell.BorderColorTop ?? e.BorderColor;
            e.BorderColorRight = cell.BorderColorRight ?? e.BorderColor;
            e.BorderColorBottom = cell.BorderColorBottom ?? e.BorderColor;
            e.BorderColorLeft = cell.BorderColorLeft ?? e.BorderColor;

            var baseBW = e.BorderWidth; // already resolved above
            e.BorderWidthTop = NormWidth(cell.BorderWidthTop, baseBW);
            e.BorderWidthRight = NormWidth(cell.BorderWidthRight, baseBW);
            e.BorderWidthBottom = NormWidth(cell.BorderWidthBottom, baseBW);
            e.BorderWidthLeft = NormWidth(cell.BorderWidthLeft, baseBW);

            // ---- apply per-column defaults (only when the cell itself didn't set them) ----
            var col = table.ColumnStyles.FirstOrDefault(s => s.Index == columnIndex);
            if (col != null)
            {
                if (col.Font != null) e.Font = col.Font;
                if (col.FontSize.HasValue) e.FontSize = col.FontSize.Value;
                if (col.TextColor.HasValue) e.TextColor = col.TextColor.Value;
                if (col.Background.HasValue && e.BackgroundColor == null) e.BackgroundColor = col.Background.Value;

                if (!cell.PaddingTop.HasValue && col.PaddingTop.HasValue) e.PaddingTop = col.PaddingTop;
                if (!cell.PaddingRight.HasValue && col.PaddingRight.HasValue) e.PaddingRight = col.PaddingRight;
                if (!cell.PaddingBottom.HasValue && col.PaddingBottom.HasValue) e.PaddingBottom = col.PaddingBottom;
                if (!cell.PaddingLeft.HasValue && col.PaddingLeft.HasValue) e.PaddingLeft = col.PaddingLeft;

                if (col.HAlign.HasValue && cell.HorizontalAlign == HorizontalAlign.Left)
                    e.HorizontalAlign = col.HAlign.Value;
                if (col.VAlign.HasValue && cell.VerticalAlign == VerticalAlign.Top)
                    e.VerticalAlign = col.VAlign.Value;
            }

            return e;
        }



        private static (float top, float right, float bottom, float left) GetPadding(Effective c, float tableDefault)
        {
            if (c.Padding.HasValue)
                return (c.Padding.Value, c.Padding.Value, c.Padding.Value, c.Padding.Value);

            float top = c.PaddingTop ?? tableDefault;
            float right = c.PaddingRight ?? tableDefault;
            float bottom = c.PaddingBottom ?? tableDefault;
            float left = c.PaddingLeft ?? tableDefault;
            return (top, right, bottom, left);
        }

        // ---------- Height planning ----------
        private static float[] ComputeRowHeights(TableElement table, float[] colWidths)
        {
            int totalCols = colWidths.Length;
            int rowCount = table.Rows.Count;
            var heights = new float[rowCount];

            for (int r = 0; r < rowCount; r++)
            {
                heights[r] = table.Rows[r].RowHeight
                    ?? table.DefaultFontSize * PdfDefaults.LineHeightMultiplier + table.CellPadding * 2;
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

                    float req = MeasureCellContentHeight(table, cell, cw);

                    if (rowSpan == 1)
                    {
                        if (req > heights[rowIndex]) heights[rowIndex] = req;
                    }
                    else
                    {
                        int lastRow = Math.Min(rowCount - 1, rowIndex + rowSpan - 1);
                        float sum = 0f;
                        for (int r = rowIndex; r <= lastRow; r++) sum += heights[r];

                        if (req > sum)
                        {
                            float deficit = req - sum;
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
                    outLines.Add(line);
                    continue;
                }

                var sbLine = new StringBuilder();
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

        private static bool HasCoverageAbove(HashSet<(int row, int col)> covered, int row, int colStart, int colSpan)
        {
            if (row == 0) return false;
            for (int c = 0; c < colSpan; c++)
                if (covered.Contains((row, colStart + c))) return true;
            return false;
        }

        // ---------- Text rendering with overflow, alignment, rotation ----------
        private static void RenderCellText(
            StringBuilder sb, TableElement table, Effective cell,
            float x, float yTop, float cellWidth, float cellHeight,
            (float top, float right, float bottom, float left) pad,
            string fontFamily, float fontSize, float lineMult,
            int? maxLines, CellWordBreak wordBreak, float rotationDeg,
            HorizontalAlign hAlign, VerticalAlign vAlign,
            int fontId, string? rgbFill)
        {
            const float ASCENT_RATIO = 0.70f; // approx for core 14 fonts
            float usableWidth = Math.Max(0, cellWidth - pad.left - pad.right);
            float lineH = fontSize * lineMult;
            float ascent = ASCENT_RATIO * fontSize;
            bool rotated = Math.Abs(rotationDeg) > 0.01f;

            // Helper: distance (downwards) from TOP of the (possibly rotated) text block to the BASELINE
            // For θ=0 this reduces to (lineH - ascent), which matches existing unrotated behavior.
            static float BaselineOffsetFromTop(float textWidth, float lineH, float ascent, float angleDeg)
            {
                if (Math.Abs(angleDeg) <= 0.01f) return lineH - ascent;
                double r = Math.Abs(angleDeg) * Math.PI / 180.0;
                double cos = Math.Cos(r);
                double sin = Math.Sin(r);
                double extraTop = angleDeg > 0 ? textWidth * sin : 0.0; // +θ lifts the right edge → more area above
                return (float)((lineH - ascent) * cos + extraTop);
            }

            if (table.OverflowPolicy == CellOverflowPolicy.Wrap)
            {
                var lines = PdfLayoutUtils.WrapText(cell.Text ?? string.Empty, fontFamily, fontSize, usableWidth);
                if (wordBreak == CellWordBreak.BreakWord)
                    lines = ForceBreakLongLines(lines, fontFamily, fontSize, usableWidth);
                if (maxLines.HasValue && maxLines.Value > 0 && lines.Count > maxLines.Value)
                    lines = lines.Take(maxLines.Value).ToList();

                float stackH = Math.Max(lineH, lines.Count * lineH);

                float blockH;
                float widthForBlock = 0f;
                if (rotated)
                {
                    foreach (var ln in lines)
                        widthForBlock = Math.Max(widthForBlock, PdfLayoutUtils.EstimateTextWidth(ln ?? string.Empty, fontFamily, fontSize));
                    blockH = RotatedBBoxHeight(widthForBlock, stackH, rotationDeg);
                }
                else
                {
                    blockH = stackH;
                }

                float topOfBlock = GetVerticalAlignedY(vAlign, yTop, cellHeight, blockH, pad.top, pad.bottom);

                // Lay out each line. For rotated blocks we anchor using the same top reference.
                float lineTop = topOfBlock;
                foreach (var line in lines)
                {
                    float wNow = PdfLayoutUtils.EstimateTextWidth(line ?? string.Empty, fontFamily, fontSize);
                    float baselineDown = rotated
                        ? BaselineOffsetFromTop(widthForBlock, lineH, ascent, rotationDeg) // keep consistent with block top
                        : lineH - ascent;

                    float baselineY = lineTop - baselineDown;
                    float textX = GetHorizontalAlignedX(hAlign, x, cellWidth, line, fontFamily, fontSize, pad.left, pad.right);

                    DrawTextRun(sb, line, fontId, fontSize, rgbFill, textX, baselineY, rotationDeg);
                    lineTop -= lineH;
                }
            }
            else
            {
                string line = cell.Text ?? string.Empty;

                if (table.OverflowPolicy == CellOverflowPolicy.Ellipsis)
                {
                    float width = PdfLayoutUtils.EstimateTextWidth(line, fontFamily, fontSize);
                    if (width > usableWidth && usableWidth > 0)
                    {
                        const string ell = "…";
                        float ellW = PdfLayoutUtils.EstimateTextWidth(ell, fontFamily, fontSize);
                        if (ellW < usableWidth)
                        {
                            while (line.Length > 0 &&
                                   PdfLayoutUtils.EstimateTextWidth(line, fontFamily, fontSize) + ellW > usableWidth)
                                line = line[..^1];
                            line += ell;
                        }
                        else line = string.Empty;
                    }
                }

                float widthNow = PdfLayoutUtils.EstimateTextWidth(line, fontFamily, fontSize);
                float blockH = rotated ? RotatedBBoxHeight(widthNow, lineH, rotationDeg) : lineH;

                float topOfBlock = GetVerticalAlignedY(vAlign, yTop, cellHeight, blockH, pad.top, pad.bottom);

                // Correct baseline position for rotation
                float baselineDown = rotated
                    ? BaselineOffsetFromTop(widthNow, lineH, ascent, rotationDeg)
                    : lineH - ascent;

                float baselineY = topOfBlock - baselineDown;
                float textX = GetHorizontalAlignedX(hAlign, x, cellWidth, line, fontFamily, fontSize, pad.left, pad.right);

                DrawTextRun(sb, line, fontId, fontSize, rgbFill, textX, baselineY, rotationDeg);
            }
        }



        private static void DrawTextRun(StringBuilder sb, string text, int fontId, float size, string? rgbFill,
                                        float tx, float ty, float rotateDeg)
        {
            if (Math.Abs(rotateDeg) > 0.01f)
            {
                double a = Math.Cos(rotateDeg * Math.PI / 180.0);
                double b = Math.Sin(rotateDeg * Math.PI / 180.0);
                double c = -b;
                double d = a;

                sb.Append("q ");                                  // save
                sb.Append($"{N(1)} {N(0)} {N(0)} {N(1)} {N(tx)} {N(ty)} cm "); // translate
                sb.Append($"{N(a)} {N(b)} {N(c)} {N(d)} 0 0 cm ");             // rotate

                sb.Append("BT ");
                sb.Append($"/F{fontId} {N(size)} Tf ");
                if (rgbFill != null) sb.Append(rgbFill + " ");
                sb.Append($"{N(0)} {N(0)} Td ");
                sb.Append($"{PdfEnc.WinAnsiHex(text)} Tj ET\n");

                sb.Append("Q\n");
            }
            else
            {
                sb.Append("BT ");
                sb.Append($"/F{fontId} {N(size)} Tf ");
                if (rgbFill != null) sb.Append(rgbFill + " ");
                sb.Append($"{N(tx)} {N(ty)} Td ");
                sb.Append($"{PdfEnc.WinAnsiHex(text)} Tj ET\n");
            }
        }
        // Distance from top of the rotated bbox down to the text baseline.
        // Assumes we rotate around the baseline origin (0,0) at the left edge.
        private static float TopToBaselineDistance(float lineWidth, float lineHeight, float ascent, float angleDeg)
        {
            double r = angleDeg * Math.PI / 180.0;
            double s = Math.Sin(r);
            double c = Math.Cos(r);

            // For small |angle| (< 90°), topmost point is:
            //  - positive θ: top-right corner -> adds w*sin above the ascent*cos
            //  - negative θ: top-left corner  -> no w*sin term
            double extraTop = s > 0 ? lineWidth * s : 0.0;

            return (float)(ascent * c + extraTop);
        }

        // ---------- Geometry & drawing ----------
        private static void FillRect(StringBuilder sb, float x, float y, float w, float h, Color color)
        {
            sb.Append($"{ToRgbFill(color)} {N(x)} {N(y)} {N(w)} {N(h)} re f\n");
        }

        private static void FillRoundedRect(StringBuilder sb, float x, float y, float w, float h, float r, Color color)
        {
            r = Math.Max(0, Math.Min(r, Math.Min(w, h) / 2f));
            if (r <= 0f) { FillRect(sb, x, y, w, h, color); return; }

            float k = 0.552284749831f; // cubic bezier approximation of a quarter circle
            float ox = r * k, oy = r * k;
            float x0 = x, y0 = y;
            float x1 = x + w, y1 = y + h;

            sb.Append(ToRgbFill(color) + " ");
            sb.Append($"{N(x0 + r)} {N(y0)} m ");
            sb.Append($"{N(x1 - r)} {N(y0)} l ");
            sb.Append($"{N(x1 - r + ox)} {N(y0)} {N(x1)} {N(y0 + r - oy)} {N(x1)} {N(y0 + r)} c ");
            sb.Append($"{N(x1)} {N(y1 - r)} l ");
            sb.Append($"{N(x1)} {N(y1 - r + oy)} {N(x1 - r + ox)} {N(y1)} {N(x1 - r)} {N(y1)} c ");
            sb.Append($"{N(x0 + r)} {N(y1)} l ");
            sb.Append($"{N(x0 + r - ox)} {N(y1)} {N(x0)} {N(y1 - r + oy)} {N(x0)} {N(y1 - r)} c ");
            sb.Append($"{N(x0)} {N(y0 + r)} l ");
            sb.Append($"{N(x0)} {N(y0 + r - oy)} {N(x0 + r - ox)} {N(y0)} {N(x0 + r)} {N(y0)} c f\n");
        }

        private static void StrokeLine(StringBuilder sb, float x1, float y1, float x2, float y2, Color color, float width)
        {
            width = width <= 0f ? PdfDefaults.DefaultBorderWidth > 0f ? PdfDefaults.DefaultBorderWidth : 0.5f : width;
            width = ClampThin(width);

            if (width <= 1f)
            {
                x1 = AlignHalf(x1); x2 = AlignHalf(x2);
                y1 = AlignHalf(y1); y2 = AlignHalf(y2);
            }

            sb.Append($"{ToRgbStroke(color)} {N(width)} w {N(x1)} {N(y1)} m {N(x2)} {N(y2)} l S\n");
        }




        private static void StrokeRect(StringBuilder sb, float x, float topY, float w, float h, Color color, float width)
        {
            if (h <= 0) return;
            float bottomY = topY - h;
            sb.Append($"{ToRgbStroke(color)} {N(width)} w ");
            sb.Append($"{N(x)} {N(topY)} m {N(x + w)} {N(topY)} l {N(x + w)} {N(bottomY)} l {N(x)} {N(bottomY)} l h S\n");
        }

        // ---------- Alignment helpers ----------
        private static float GetVerticalAlignedY(VerticalAlign align, float rowTopY, float targetHeight,
                                                 float blockHeight, float padTop, float padBottom)
        {
            float contentTop = rowTopY - padTop;
            float contentHeight = Math.Max(0f, targetHeight - (padTop + padBottom));
            return align switch
            {
                VerticalAlign.Middle => contentTop - (contentHeight - blockHeight) / 2f,
                VerticalAlign.Bottom => contentTop - (contentHeight - blockHeight),
                _ => contentTop // Top
            };
        }

        private static float GetHorizontalAlignedX(HorizontalAlign align, float startX, float cellWidth, string line,
                                                   string fontFamily, float fontSize, float padLeft, float padRight)
        {
            float textWidth = PdfLayoutUtils.EstimateTextWidth(line ?? "", fontFamily, fontSize);
            return align switch
            {
                HorizontalAlign.Center => startX + Math.Max(padLeft, (cellWidth - textWidth) / 2f),
                HorizontalAlign.Right => startX + Math.Max(padLeft, cellWidth - textWidth - padRight),
                _ => startX + padLeft
            };
        }

        // ---------- Text helpers ----------
        private static string PdfText(string s)
        {
            s = SanitizeForPdfText(s) ?? string.Empty;

            // Map to Windows-1252 (WinAnsi). Unmappable chars -> '?'
            var enc = Encoding.GetEncoding(1252,
                EncoderFallback.ReplacementFallback,
                DecoderFallback.ReplacementFallback);
            byte[] bytes = enc.GetBytes(s);

            var sb = new StringBuilder(bytes.Length + 16);
            sb.Append('(');
            foreach (byte b in bytes)
            {
                // Escape parens and backslash
                if (b == (byte)'(' || b == (byte)')' || b == (byte)'\\')
                {
                    sb.Append('\\').Append((char)b);
                }
                // Control/non-ASCII -> octal \ddd (PDF spec 7.3.4.2)
                else if (b < 32 || b >= 127)
                {
                    sb.Append('\\');
                    sb.Append(b >> 6 & 0x07);
                    sb.Append(b >> 3 & 0x07);
                    sb.Append(b & 0x07);
                }
                else
                {
                    sb.Append((char)b);
                }
            }
            sb.Append(')');
            return sb.ToString();
        }

        private static bool IsAscii(string s)
        {
            foreach (var ch in s) if (ch > 0x7F) return false;
            return true;
        }

        private static string EscapeParens(string s) => s.Replace(@"\", @"\\").Replace("(", @"\(").Replace(")", @"\)");

        private static string Utf16Hex(string s)
        {
            var raw = Encoding.BigEndianUnicode.GetBytes(s);
            var withBom = new byte[raw.Length + 2];
            withBom[0] = 0xFE; withBom[1] = 0xFF;
            Buffer.BlockCopy(raw, 0, withBom, 2, raw.Length);
            return $"<{BitConverter.ToString(withBom).Replace("-", "")}>";
        }

        // ---------- color & fonts ----------
        private static string? ToRgbFill(Color? c)
        {
            if (c == null) return null;
            var col = c.Value;
            return $"{(col.R / 255.0).ToString("0.###", Inv)} {(col.G / 255.0).ToString("0.###", Inv)} {(col.B / 255.0).ToString("0.###", Inv)} rg";
        }
        private static string ToRgbStroke(Color c)
            => $"{(c.R / 255.0).ToString("0.###", Inv)} {(c.G / 255.0).ToString("0.###", Inv)} {(c.B / 255.0).ToString("0.###", Inv)} RG";

        private static string MapFontVariant(string family, bool bold, bool italic)
        {
            var f = (family ?? "Helvetica").Trim();
            bool helv = f.Equals("Helvetica", StringComparison.OrdinalIgnoreCase);
            bool cour = f.Equals("Courier", StringComparison.OrdinalIgnoreCase);

            if (helv)
            {
                if (bold && italic) return "Helvetica-BoldOblique";
                if (bold) return "Helvetica-Bold";
                if (italic) return "Helvetica-Oblique";
                return "Helvetica";
            }
            if (cour)
            {
                if (bold && italic) return "Courier-BoldOblique";
                if (bold) return "Courier-Bold";
                if (italic) return "Courier-Oblique";
                return "Courier";
            }
            // fallback to Helvetica variants
            return MapFontVariant("Helvetica", bold, italic);
        }

        private static int ResolveFontId(Dictionary<string, int> fontObjId, string name)
        {
            if (fontObjId.TryGetValue(name, out var id)) return id;
            // soft fallback to Helvetica if requested variant not embedded
            var fallback = fontObjId.ContainsKey("Helvetica") ? "Helvetica" : fontObjId.Keys.First();
            return fontObjId[fallback];
        }

        // Does the row above actually draw a bottom on the span [colStart..colStart+colSpan-1]?
        private static bool PreviousRowDrawsBottom(TableElement table, int rowIndex, int colStart, int colSpan)
        {
            if (rowIndex <= 0) return false;

            var prev = table.Rows[rowIndex - 1];
            int totalCols = table.Rows.Max(r => r.Cells.Sum(c => Math.Max(1, c.ColSpan)));

            int ci = 0;
            for (int i = 0; i < prev.Cells.Count; i++)
            {
                var c = prev.Cells[i];
                int span = Math.Max(1, c.ColSpan);

                int from = ci;
                int to = ci + span - 1;
                int reqFrom = colStart;
                int reqTo = colStart + colSpan - 1;

                bool overlaps = !(to < reqFrom || from > reqTo);
                if (overlaps)
                {
                    // If the cell above row-spans into this row, treat that boundary as "owned" by above.
                    if (c.RowSpan > 1) return true;
                    if (c.BorderBottom) return true;
                }

                ci += span;
            }
            return false;
        }
        private static float[] AutoSizeColumnWidths(TableElement table, int totalCols, float tableWidth)
        {
            // Allow partial fixed widths: any >0 in ColumnWidths are respected; others auto.
            var fixedW = new float[totalCols];
            var isFixed = new bool[totalCols];
            if (table.ColumnWidths != null)
            {
                int n = Math.Min(totalCols, table.ColumnWidths.Count);
                for (int i = 0; i < n; i++)
                {
                    if (table.ColumnWidths[i] > 0f)
                    {
                        isFixed[i] = true;
                        fixedW[i] = table.ColumnWidths[i];
                    }
                }
            }

            float fixedSum = fixedW.Sum();
            float avail = Math.Max(1f, tableWidth - fixedSum);

            // Minimum and "desired" widths for auto columns
            var minW = Enumerable.Repeat(40f, totalCols).ToArray();  // floor so borders/text don’t collapse
            var wantW = Enumerable.Repeat(40f, totalCols).ToArray();

            float padDefault = Math.Max(0f, table.CellPadding);

            void Consider(int col, TableCell c, string text)
            {
                if (isFixed[col]) return;

                float padL = c.PaddingLeft ?? c.Padding ?? padDefault;
                float padR = c.PaddingRight ?? c.Padding ?? padDefault;
                string font = string.IsNullOrWhiteSpace(c.Font) ? table.DefaultFont : c.Font;
                float size = c.FontSize > 0 ? c.FontSize : table.DefaultFontSize;

                float head = PdfLayoutUtils.EstimateTextWidth(text ?? "", font, size) + padL + padR;
                if (head > minW[col]) minW[col] = head;

                // Numeric-ish columns should be compact; text columns can flex.
                bool numericish = c.HorizontalAlign == HorizontalAlign.Right || IsNumericLike(c.Text);
                float want;
                if (numericish)
                {
                    // keep reasonable for 3–6 digits, parentheses/%, etc.
                    want = Math.Clamp(head, 40f, 90f);
                }
                else
                {
                    // Encourage wrapping: base "want" on the longer of header or longest word,
                    // but cap so one verbose cell (Description) doesn’t steal the table.
                    string longestWord = LongestWord(c.Text);
                    float lw = PdfLayoutUtils.EstimateTextWidth(longestWord, font, size) + padL + padR;
                    float cap = tableWidth * 0.55f;
                    want = Math.Min(Math.Max(minW[col], Math.Max(head, lw)), cap);
                }
                if (want > wantW[col]) wantW[col] = want;
            }

            // Consider headers (consecutive IsHeader rows from the top)
            int headerCount = CountLeadingHeaders(table);
            for (int r = 0; r < Math.Min(headerCount, table.Rows.Count); r++)
            {
                int cpos = 0;
                foreach (var c in table.Rows[r].Cells)
                {
                    int span = Math.Max(1, c.ColSpan);
                    if (span == 1) Consider(cpos, c, c.Text ?? "");
                    cpos += span;
                }
            }

            // Consider body cells (only single-span cells contribute; multi-span are ignored for sizing)
            for (int r = headerCount; r < table.Rows.Count; r++)
            {
                int cpos = 0;
                foreach (var c in table.Rows[r].Cells)
                {
                    int span = Math.Max(1, c.ColSpan);
                    if (span == 1) Consider(cpos, c, c.Text ?? "");
                    cpos += span;
                }
            }

            // Allocate widths for auto columns between min and desired, inside the available space.
            float sumMin = 0f, sumWant = 0f;
            var auto = new List<int>();
            for (int i = 0; i < totalCols; i++)
            {
                if (isFixed[i]) continue;
                minW[i] = Math.Min(minW[i], avail);
                wantW[i] = Math.Max(minW[i], wantW[i]);
                sumMin += minW[i];
                sumWant += wantW[i];
                auto.Add(i);
            }

            var result = fixedW.ToArray(); // start with fixed

            if (auto.Count == 0)
                return result;

            if (sumMin > avail)
            {
                // Not enough room: scale the minima proportionally
                float scale = avail / sumMin;
                foreach (int i in auto) result[i] = Math.Max(24f, minW[i] * scale);
            }
            else if (sumWant <= avail)
            {
                foreach (int i in auto) result[i] = wantW[i];
            }
            else
            {
                // Distribute the extra beyond minima toward the desired widths
                float extra = avail - sumMin;
                float denom = Math.Max(1e-6f, sumWant - sumMin);
                foreach (int i in auto)
                    result[i] = minW[i] + extra * ((wantW[i] - minW[i]) / denom);
            }

            // Nudge last auto column so total matches tableWidth exactly (avoids 1px gaps from rounding)
            float diff = tableWidth - result.Sum();
            if (Math.Abs(diff) > 0.01f)
            {
                int idx = auto.Last();
                result[idx] += diff;
            }

            return result;
        }

        private static bool IsNumericLike(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return false;
            foreach (char ch in s)
                if (!(char.IsDigit(ch) || ch == ' ' || ch == ',' || ch == '.' || ch == '-' || ch == '(' || ch == ')' || ch == '%'))
                    return false;
            return true;
        }

        private static string LongestWord(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return "";
            string[] parts = s.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            string longest = "";
            foreach (var p in parts) if (p.Length > longest.Length) longest = p;
            return longest;
        }
        // Counts consecutive header rows from the top of the table.
        // Header rows are those with row.IsHeader == true.
        private static int CountLeadingHeaders(TableElement table)
        {
            if (table?.Rows == null) return 0;

            int count = 0;
            foreach (var row in table.Rows)
            {
                if (row != null && row.IsHeader) count++;
                else break;
            }
            return count;
        }

        // Map a logical column index to the cell that covers it (accounts for ColSpan)
        // Map a logical column index to the cell that covers it (accounts for ColSpan)
        private static (int cellIdx, TableCell cell) CellCoveringColumn(TableRow row, int colIndex)
        {
            int cursor = 0;
            for (int i = 0; i < row.Cells.Count; i++)
            {
                var c = row.Cells[i];
                int span = Math.Max(1, c.ColSpan);
                if (colIndex >= cursor && colIndex < cursor + span)
                    return (i, c);
                cursor += span;
            }
            return (-1, null);
        }

        // Build an Edge for TOP of (row,col), and the competing BOTTOM of the row above
        private static void BuildTopVsAboveBottom(
     TableElement table, int rowIndex, int colIndex, HashSet<(int row, int col)> covered,
     bool cellTopOn, Color topColor, float topWidth,
     out Edge top, out Edge aboveBottom)
        {
            bool topExplicit = cellTopOn &&
                (Math.Abs(topWidth - table.BorderWidth) > 1e-3f ||
                 topColor.ToArgb() != table.BorderColor.ToArgb());

            top = new Edge
            {
                Exists = cellTopOn,
                Width = topWidth,
                Color = topColor,
                OriginRank = topExplicit ? -2 : 0
            };

            var currRow = table.Rows[rowIndex];
            if (currRow.ThickTopBorder)
            {
                top.Exists = true;
                if (currRow.ThickBorderWidth > top.Width) top.Width = currRow.ThickBorderWidth;
                top.Color = currRow.ThickBorderColor ?? table.BorderColor;
                top.OriginRank = Math.Min(top.OriginRank, 1);
            }

            aboveBottom = new Edge { Exists = false };
            if (rowIndex <= 0) return;
            if (covered.Contains((rowIndex, colIndex))) return;

            var aboveRow = table.Rows[rowIndex - 1];
            if (aboveRow.ThickBottomBorder)
            {
                aboveBottom = new Edge
                {
                    Exists = true,
                    Width = aboveRow.ThickBorderWidth,
                    Color = aboveRow.ThickBorderColor ?? table.BorderColor,
                    OriginRank = 1
                };
            }

            var (_, aboveCell) = CellCoveringColumn(aboveRow, colIndex);
            if (aboveCell != null && aboveCell.BorderBottom)
            {
                SideSpec(aboveCell.BorderColorBottom, aboveCell.BorderWidthBottom,
                         aboveCell.BorderColor, aboveCell.BorderWidth,
                         table.BorderColor, table.BorderWidth,
                         out var col, out var w, out var isExp);

                aboveBottom = new Edge
                {
                    Exists = true,
                    Width = Math.Max(aboveBottom.Width, w),
                    Color = col,
                    OriginRank = isExp ? -2 : aboveBottom.OriginRank
                };
            }
        }







        // BOTTOM (this cell) vs TOP (row below)
        private static void BuildBottomVsBelowTop(
            TableElement table, int rowIndex, int colIndex, int rowSpan, HashSet<(int row, int col)> covered,
            bool cellBottomOn, Color bottomColor, float bottomWidth,
            out Edge bottom, out Edge belowTop)
        {
            bool bottomExplicit = cellBottomOn &&
                (Math.Abs(bottomWidth - table.BorderWidth) > 1e-3f ||
                 bottomColor.ToArgb() != table.BorderColor.ToArgb());

            bottom = new Edge
            {
                Exists = cellBottomOn,
                Width = bottomWidth,
                Color = bottomColor,
                OriginRank = bottomExplicit ? -2 : 0
            };

            var currRow = table.Rows[rowIndex];
            if (currRow.ThickBottomBorder)
            {
                bottom.Exists = true;
                if (currRow.ThickBorderWidth > bottom.Width) bottom.Width = currRow.ThickBorderWidth;
                bottom.Color = currRow.ThickBorderColor ?? table.BorderColor;
                bottom.OriginRank = Math.Min(bottom.OriginRank, 1);
            }

            belowTop = new Edge { Exists = false };

            int belowRowIndex = rowIndex + Math.Max(1, rowSpan);
            if (belowRowIndex >= table.Rows.Count) return;
            if (covered.Contains((belowRowIndex, colIndex))) return;

            var belowRow = table.Rows[belowRowIndex];
            if (belowRow.ThickTopBorder)
            {
                belowTop = new Edge
                {
                    Exists = true,
                    Width = belowRow.ThickBorderWidth,
                    Color = belowRow.ThickBorderColor ?? table.BorderColor,
                    OriginRank = 1
                };
            }

            var (_, belowCell) = CellCoveringColumn(belowRow, colIndex);
            if (belowCell != null && belowCell.BorderTop)
            {
                SideSpec(belowCell.BorderColorTop, belowCell.BorderWidthTop,
                         belowCell.BorderColor, belowCell.BorderWidth,
                         table.BorderColor, table.BorderWidth,
                         out var col, out var w, out var isExp);

                belowTop = new Edge
                {
                    Exists = true,
                    Width = Math.Max(belowTop.Width, w),
                    Color = col,
                    OriginRank = isExp ? -2 : belowTop.OriginRank
                };
            }
        }



        private static void BuildRightVsNeighborLeft(
     TableElement table, TableRow row, int colIndex,
     bool cellRightOn, Color rightColor, float rightWidth,
     out Edge right, out Edge neighborLeft)
        {
            bool rightExplicit = cellRightOn &&
                (Math.Abs(rightWidth - table.BorderWidth) > 1e-3f ||
                 rightColor.ToArgb() != table.BorderColor.ToArgb());

            right = new Edge
            {
                Exists = cellRightOn,
                Width = rightWidth,
                Color = rightColor,
                OriginRank = rightExplicit ? -2 : 0
            };

            neighborLeft = new Edge { Exists = false };

            int cursor = 0;
            for (int i = 0; i < row.Cells.Count; i++)
            {
                var c = row.Cells[i];
                int span = Math.Max(1, c.ColSpan);
                if (colIndex < cursor + span)
                {
                    int nextCol = cursor + span;
                    var (_, nCell) = CellCoveringColumn(row, nextCol);
                    if (nCell != null && nCell.BorderLeft)
                    {
                        SideSpec(nCell.BorderColorLeft, nCell.BorderWidthLeft,
                                 nCell.BorderColor, nCell.BorderWidth,
                                 table.BorderColor, table.BorderWidth,
                                 out var col, out var w, out var isExp);

                        neighborLeft = new Edge
                        {
                            Exists = true,
                            Width = w,
                            Color = col,
                            OriginRank = isExp ? -2 : 0
                        };
                    }
                    break;
                }
                cursor += span;
            }
        }



        private static float NormWidth(float? v, float fallback)
     => v.HasValue && v.Value > 0f ? v.Value : fallback;

        // Compare THIS cell's LEFT edge vs the LEFT neighbor's RIGHT edge.
        // Used when the shared vertical seam is "owned" by the RIGHT cell (this one) drawing its LEFT.
        private static void BuildLeftVsLeftNeighborRight(
            TableRow row, int colIndex,
            bool cellLeftOn, Color leftColor, float leftWidth,
            out Edge left, out Edge neighborRight)
        {
            left = new Edge { Exists = cellLeftOn, Width = leftWidth, Color = leftColor, OriginRank = 0 };
            neighborRight = new Edge { Exists = false };

            int cursor = 0, neighborIdx = -1;
            for (int i = 0; i < row.Cells.Count; i++)
            {
                var c = row.Cells[i];
                int span = Math.Max(1, c.ColSpan);

                // If this logical column starts a cell, the neighbor is the previous cell.
                if (colIndex == cursor) { neighborIdx = i - 1; break; }

                cursor += span;

                // If we're inside a multi-span cell, there's no left boundary to compare.
                if (colIndex < cursor) { neighborIdx = i; break; }
            }

            if (neighborIdx >= 0 && neighborIdx < row.Cells.Count)
            {
                var nc = row.Cells[neighborIdx];
                if (nc.BorderRight)
                {
                    float w = nc.BorderWidthRight ?? nc.BorderWidth;
                    var c2 = nc.BorderColorRight ?? nc.BorderColor;
                    neighborRight = new Edge { Exists = true, Width = w, Color = c2, OriginRank = 0 };
                }
            }
        }


        // helper: get a side's effective width/color + explicitness against table defaults
        // Baseline from TABLE; only upgrade when caller explicitly set a side.
        // This avoids "default Black" from the cell leaking into comparisons.
        static void SideSpec(
      Color? sideColor, float? sideWidth,
      Color cellBaseColor, float cellBaseWidth,
      Color tableColor, float tableWidth,
      out Color color, out float width, out bool isExplicit)
        {
            color = tableColor;
            width = tableWidth > 0 ? tableWidth : PdfDefaults.DefaultBorderWidth;

            if (cellBaseWidth > 0 && Math.Abs(cellBaseWidth - width) > 1e-3f)
                width = cellBaseWidth;

            if (sideWidth.HasValue && sideWidth.Value > 0) width = sideWidth.Value;
            if (sideColor.HasValue) color = sideColor.Value;

            isExplicit = sideColor.HasValue || sideWidth.HasValue && sideWidth.Value > 0;
        }
        private static string SanitizeForPdfText(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            Span<char> zw = stackalloc char[] { '\uFEFF', '\u200B', '\u200C', '\u200D', '\u2060' };
            var sb = new StringBuilder(s.Length);
            foreach (var ch in s)
            {
                bool skip = false;
                for (int i = 0; i < zw.Length; i++) if (ch == zw[i]) { skip = true; break; }
                if (!skip) sb.Append(ch);
            }
            return sb.ToString();
        }
        private static float RotatedBBoxHeight(float textWidth, float unrotatedHeight, float angleDeg)
        {
            double r = Math.Abs(angleDeg) * Math.PI / 180.0;
            return (float)(Math.Abs(textWidth * Math.Sin(r)) + Math.Abs(unrotatedHeight * Math.Cos(r)));
        }


        private static bool IsExplicitSide(Color? c, float? w)
    => c.HasValue || w.HasValue && w.Value > 0f;



    }
}
