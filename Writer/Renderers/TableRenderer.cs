using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Text;
using PdfBuilder.Document;
using PdfBuilder.Document.Layout;
using PdfBuilder.Elements;
using PdfBuilder.Models;
using PdfBuilder.TextShaping;
using TableModels = PdfBuilder.Elements.Table;

namespace PdfBuilder.Writer
{
    public static class TableRenderer
    {
        private static readonly IFormatProvider Inv = CultureInfo.InvariantCulture;
        private static string N(double v) => v.ToString("0.###", Inv);
        static float ClampThin(float w) => Math.Max(0.25f, w);
        static float AlignHalf(float v) => (float)Math.Round(v * 2f) / 2f;

        private sealed class Edge
        {
            public bool Exists;
            public float Width;
            public Color Color;
            public int OriginRank; // cell > row > table (lower is stronger)
            public string? Style { get; set; }  // legacy style tag for tie-breaking
            public TableModels.BorderStyle? BorderStyle;
        }

        private sealed class BorderDrawSpec
        {
            public Color Color;
            public float Width;
            public TableModels.BorderStyle? Style;
        }

        private sealed class ResolvedRun
        {
            public string Text = string.Empty;
            public TableModels.TextStyle Style = new TableModels.TextStyle();
            public string BaseFont = "Helvetica";
            public Color Color = Color.Black;
            public float FontSize;
            public bool Underline;
            public bool Strikethrough;
            public bool Overline;
            public Color? DecorationColor;
            public float? DecorationThickness;
            public TextDecorationStyle DecorationStyle = TextDecorationStyle.Solid;
            public Color? Background;
            public float HighlightPadding;
            public bool Superscript;
            public bool Subscript;
            public float RotationOverride;
            public List<string>? FallbackFonts;
            public float Width;
            public Dictionary<string, ShapedLine>? ShapeCache;
        }

        private sealed class Fragment
        {
            public ResolvedRun Run = null!;
            public string Text = string.Empty;
            public float Width;
            public bool IsWhitespace;
            public bool IsLineBreak;
            public ShapedLine? ShapedLine;
            public float Ascent;
            public float Descent;
        }

        private sealed class LineLayout
        {
            public List<Fragment> Fragments { get; } = new();
            public float Width;
            public float MaxFontSize;
            public float Height;
            public float BaselineOffset;
            public float Ascent;
            public float Descent;
        }

        // Compare a and b; return +1 if b wins, 0 if a wins, -1 if none (no competitor)
        private static int CompareEdges(Edge a, Edge b, bool preferSecondOnTie)
        {
            if (b == null || !b.Exists) return -1;
            if (!a.Exists) return +1;

            // (1) style precedence (optional - if not tracking, skip)
            // same style => (2) thicker wins
            if (Math.Abs(a.Width - b.Width) > 1e-3f)
                return b.Width > a.Width ? +1 : 0;

            // (3) origin precedence: lower OriginRank wins
            if (a.OriginRank != b.OriginRank)
                return b.OriginRank < a.OriginRank ? +1 : 0;

            // (4) final tie-break: optionally prefer the competitor when ties remain
            return preferSecondOnTie ? +1 : 0;
        }

        public static void Append(StringBuilder sb, TableElement table, PdfRenderContext context)
        {
            if (table == null || table.Rows == null || table.Rows.Count == 0) return;
            TableGridValidator.Validate(table);
            var rows = table.Rows!;

            // local helper: a side is "explicit" if caller set color and/or width on that side
            static bool IsExplicitSide(Color? c, float? w) => c.HasValue || w.HasValue && w.Value > 0f;

            sb.Append("q\n");           // isolate graphics state
            sb.Append("0 J 0 j\n");     // butt caps, miter joins
            bool collapse = table.BorderCollapse == TableModels.BorderCollapseMode.Collapse
                             || table.ResolveBorderConflicts;

            // -- Geometry: columns --
            int totalCols = rows.Max(r => r.Cells.Sum(c => Math.Max(1, c.ColSpan)));
            float tableWidth = table.TableWidth ?? 500f;

            float[] colWidths;
            if (table.ColumnWidths != null && table.ColumnWidths.Count == totalCols && table.ColumnWidths.All(w => w > 0f))
            {
                colWidths = table.ColumnWidths.ToArray();
            }
            else if (table.AutoSizeColumns == false)
            {
                colWidths = Enumerable.Repeat(tableWidth / Math.Max(1, totalCols), totalCols).ToArray();
            }
            else
            {
                colWidths = TableColumnWidthCalculator.Calculate(table, totalCols, tableWidth);
            }

            // -- Heights (row/colspan-aware) --
            var rowHeights = ComputeRowHeights(table, colWidths);

            // -- Caption --
            float y = table.Y;
            if (!string.IsNullOrWhiteSpace(table.CaptionText))
            {
                string captionText = table.CaptionText;
                string captionFont = string.IsNullOrWhiteSpace(table.DefaultFont) ? "Helvetica" : table.DefaultFont;
                float capSize = Math.Max(table.DefaultFontSize, 11f);
                float lineMult = PdfDefaults.LineHeightMultiplier;

                var capRequest = new TextShapingRequest(
                    captionText,
                    captionFont,
                    capSize,
                    lineMult,
                    maxWidth: 0f,
                    bold: true,
                    italic: false,
                    smallCaps: false,
                    monospace: false,
                    fallbackFonts: table.DefaultTextStyle?.FallbackFonts,
                    table.DefaultTextStyle?.FlowDirection ?? FlowDirection.LeftToRight);

                var capParagraph = TextShaper.Shared.ShapeParagraph(capRequest);
                var capLine = capParagraph.Lines.FirstOrDefault();
                float textWidth = capLine?.Width ?? 0f;

                float totalWidth = colWidths.Sum();
                float xCap = table.X;
                if (table.CaptionAlign == HorizontalAlign.Center)
                    xCap = table.X + Math.Max(0, (totalWidth - textWidth) / 2f);
                else if (table.CaptionAlign == HorizontalAlign.Right)
                    xCap = table.X + Math.Max(0, totalWidth - textWidth);

                if (capLine != null)
                {
                    float cursor = xCap;
                    const string fill = "0 0 0 rg";
                    foreach (var run in capLine.Runs)
                    {
                        var encoded = GlyphRunEncoder.Encode(run, context);
                        sb.Append("BT ");
                        sb.Append($"{encoded.FontResourceName} {N(run.FontSize)} Tf ");
                        sb.Append(fill);
                        sb.Append(' ');
                        sb.Append($"{N(cursor)} {N(y)} Td ");
                        sb.Append($"{encoded.TjCommand} ET\n");
                        cursor += run.Width;
                    }

                    float captionHeight = Math.Max(capLine.LineHeight, capSize * lineMult);
                    y -= captionHeight + 4f;
                }
                else
                {
                    float captionHeight = capSize * lineMult;
                    y -= captionHeight + 4f;
                }
            }

            // -- Outer frame (behind cells so cell borders stay visible) --
            if (table.DrawOuterFrame || table.OuterBorder != null)
            {
                float frameW = colWidths.Sum();
                float frameH = rowHeights.Sum();
                float frameBottom = y - frameH;

                var outerStyle = table.OuterBorder?.Clone();
                Color outerColor = outerStyle?.Color ?? table.OuterFrameColor;
                float outerWidth = outerStyle?.Width ?? table.OuterFrameWidth;

                StrokeRoundedRect(
                    sb,
                    table.X,
                    frameBottom,
                    frameW,
                    frameH,
                    table.OuterCornerRadiusTopLeft,
                    table.OuterCornerRadiusTopRight,
                    table.OuterCornerRadiusBottomRight,
                    table.OuterCornerRadiusBottomLeft,
                    outerColor,
                    outerWidth,
                    outerStyle);
            }

            // -- Alternating rows (OPT-IN) --
            bool zebraEnabled = table.AltRowBackground.HasValue && table.AltRowEvery > 0;
            int zebraEvery = zebraEnabled ? Math.Max(1, table.AltRowEvery) : int.MaxValue;
            int zebraStart = Math.Max(0, table.AltRowStartIndex);
            int bodyRowCounter = 0;

            var covered = new HashSet<(int row, int col)>();
            var drawnHorizontalSeams = new HashSet<(int seamRow, int colIndex)>();
            var drawnVerticalSeams = new HashSet<(int rowIndex, int seamCol)>();
            int rowIndex = 0;

            // -- Rows --
            while (rowIndex < table.Rows.Count)
            {
                float rowHeight = rowHeights[rowIndex];
                float x = table.X;
                int colIndex = 0;
                var row = table.Rows[rowIndex];
                int absoluteBodyIndex = row.BandIndex ?? table.RowBandOffset + bodyRowCounter;
                var rowBand = row.IsHeader || row.IsFooter ? null : ResolveRowBand(table, absoluteBodyIndex);

                while (colIndex < totalCols && covered.Contains((rowIndex, colIndex)))
                {
                    x += colWidths[colIndex];
                    colIndex++;
                }

                // Row background: explicit -> header -> zebra (only if enabled)
                bool isAlt = zebraEnabled
                             && !row.IsHeader
                             && !row.IsFooter
                             && absoluteBodyIndex >= zebraStart
                             && (absoluteBodyIndex - zebraStart) % zebraEvery == 0;

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
                    int logicalColIndex = colIndex;
                    var columnBand = ResolveColumnBand(table, logicalColIndex);
                    var eff = BuildEffectiveCell(table, row, cell, logicalColIndex, rowBand, columnBand);

                    // Background
                    float cellBottom = y - cellHeight;
                    var baseRowFill = row.BackgroundColor
                                      ?? (row.IsHeader ? table.HeaderBackground
                                                       : isAlt ? table.AltRowBackground : null);
                    var rowBandFill = rowBand?.FillColor;
                    var columnBandFill = columnBand?.FillColor;
                    var cellFill = eff.BackgroundColor;

                    if (baseRowFill.HasValue)
                        FillRoundedRect(sb, x, cellBottom, cellWidth, cellHeight,
                                        eff.CornerRadiusTopLeft, eff.CornerRadiusTopRight,
                                        eff.CornerRadiusBottomRight, eff.CornerRadiusBottomLeft,
                                        baseRowFill.Value);
                    if (rowBandFill.HasValue)
                        FillRoundedRect(sb, x, cellBottom, cellWidth, cellHeight,
                                        eff.CornerRadiusTopLeft, eff.CornerRadiusTopRight,
                                        eff.CornerRadiusBottomRight, eff.CornerRadiusBottomLeft,
                                        rowBandFill.Value);
                    if (columnBandFill.HasValue)
                        FillRoundedRect(sb, x, cellBottom, cellWidth, cellHeight,
                                        eff.CornerRadiusTopLeft, eff.CornerRadiusTopRight,
                                        eff.CornerRadiusBottomRight, eff.CornerRadiusBottomLeft,
                                        columnBandFill.Value);
                    if (cellFill.HasValue)
                        FillRoundedRect(sb, x, cellBottom, cellWidth, cellHeight,
                                        eff.CornerRadiusTopLeft, eff.CornerRadiusTopRight,
                                        eff.CornerRadiusBottomRight, eff.CornerRadiusBottomLeft,
                                        cellFill.Value);

                    // ----- Border drawing -----
                    if (collapse)
                    {
                        // TOP: per-slot winner (mixed neighbors handled)
                        float xSegTop = x;
                        for (int s = 0; s < colSpan; s++)
                        {
                            float segW = colWidths[logicalColIndex + s];

                            BuildTopVsAboveBottom(
                                table, rowIndex, logicalColIndex + s, covered,
                                eff.BorderTop, eff.DrawTop,
                                out var top, out var aboveBottom);

                            // Draw TOP only if it wins. If it loses, previous row already drew its BOTTOM.
                            var topWins = CompareEdges(top, aboveBottom, preferSecondOnTie: true) <= 0 ? top : null;
                            if (topWins != null && topWins.Exists)
                            {
                                var seamKey = (rowIndex, logicalColIndex + s);
                                if (drawnHorizontalSeams.Add(seamKey))
                                    StrokeLine(sb, xSegTop, y, xSegTop + segW, y, topWins.Color, topWins.Width, topWins.BorderStyle);
                            }

                            xSegTop += segW;
                        }

                        // VERTICALS: draw each shared boundary once at x+cellWidth (RIGHT owns)
                        if (logicalColIndex == 0 && eff.BorderLeft)
                            StrokeLine(sb, x, y, x, y - cellHeight, eff.DrawLeft.Color, eff.DrawLeft.Width, eff.DrawLeft.Style);

                        BuildRightVsNeighborLeft(
                            table, row, logicalColIndex,
                            eff.BorderRight, eff.DrawRight,
                            out var right, out var neighborLeft);

                        Edge vWinner;
                        if (!neighborLeft.Exists)
                        {
                            vWinner = right;
                        }
                        else
                        {
                            int cmp = CompareEdges(neighborLeft, right, preferSecondOnTie: true); // right wins ties
                            vWinner = cmp > 0 ? right : neighborLeft;
                        }
                        if (vWinner.Exists)
                            StrokeLine(sb, x + cellWidth, y, x + cellWidth, y - cellHeight, vWinner.Color, vWinner.Width, vWinner.BorderStyle);

                        // BOTTOM: per-slot winner and draw once here
                        float xSeg = x;
                        for (int s = 0; s < colSpan; s++)
                        {
                            float segW = colWidths[logicalColIndex + s];

                            BuildBottomVsBelowTop(
                                table, rowIndex, logicalColIndex + s, rowSpan, covered,
                                eff.BorderBottom, eff.DrawBottom,
                                out var bottom, out var belowTop);

                            // Bottom vs below's Top; ties favor this cell's bottom
                            int cmpH = CompareEdges(bottom, belowTop, preferSecondOnTie: false);
                            if (cmpH <= 0 && bottom.Exists)
                                StrokeLine(sb, xSeg, y - cellHeight, xSeg + segW, y - cellHeight, bottom.Color, bottom.Width, bottom.BorderStyle);

                            xSeg += segW;
                        }
                    }
                    else
                    {
                        // Non-conflict mode: draw what the cell asks for using effective per-side values.
                        bool topExp = IsExplicitSide(cell.BorderColorTop, cell.BorderWidthTop);
                        bool rightExp = IsExplicitSide(cell.BorderColorRight, cell.BorderWidthRight);
                        bool bottomExp = IsExplicitSide(cell.BorderColorBottom, cell.BorderWidthBottom);
                        bool leftExp = IsExplicitSide(cell.BorderColorLeft, cell.BorderWidthLeft);

                        if (cell.BorderTop && !topExp)
                            StrokeLine(sb, x, y, x + cellWidth, y, eff.DrawTop.Color, eff.DrawTop.Width, eff.DrawTop.Style);
                        if (cell.BorderRight && !rightExp)
                            StrokeLine(sb, x + cellWidth, y, x + cellWidth, y - cellHeight, eff.DrawRight.Color, eff.DrawRight.Width, eff.DrawRight.Style);
                        if (cell.BorderBottom && !bottomExp)
                            StrokeLine(sb, x, y - cellHeight, x + cellWidth, y - cellHeight, eff.DrawBottom.Color, eff.DrawBottom.Width, eff.DrawBottom.Style);
                        if (cell.BorderLeft && !leftExp)
                            StrokeLine(sb, x, y, x, y - cellHeight, eff.DrawLeft.Color, eff.DrawLeft.Width, eff.DrawLeft.Style);

                        // Overlay explicit per-side overrides last so their colors and thickness win visually.
                        if (cell.BorderTop && topExp)
                            StrokeLine(sb, x, y, x + cellWidth, y, eff.DrawTop.Color, eff.DrawTop.Width, eff.DrawTop.Style);
                        if (cell.BorderRight && rightExp)
                            StrokeLine(sb, x + cellWidth, y, x + cellWidth, y - cellHeight, eff.DrawRight.Color, eff.DrawRight.Width, eff.DrawRight.Style);
                        if (cell.BorderBottom && bottomExp)
                            StrokeLine(sb, x, y - cellHeight, x + cellWidth, y - cellHeight, eff.DrawBottom.Color, eff.DrawBottom.Width, eff.DrawBottom.Style);
                        if (cell.BorderLeft && leftExp)
                            StrokeLine(sb, x, y, x, y - cellHeight, eff.DrawLeft.Color, eff.DrawLeft.Width, eff.DrawLeft.Style);
                    }

                    // ----- Text -----
                    if (!cell.HasContainerContent && eff.Runs.Any(run => !string.IsNullOrEmpty(run.Text)))
                    {
                        var pad = GetPadding(eff, table.CellPadding);

                        // Clip to cell rect
                        sb.Append("q ");
                        sb.Append($"{N(x)} {N(y - cellHeight)} {N(cellWidth)} {N(cellHeight)} re W n\n");

                        RenderCellText(sb, table, eff, x, y, cellWidth, cellHeight, pad, context);

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

                if (!row.IsHeader && !row.IsFooter) bodyRowCounter++;
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
            public FlowDirection FlowDirection = FlowDirection.LeftToRight;

            public HorizontalAlign HorizontalAlign = HorizontalAlign.Left;
            public VerticalAlign VerticalAlign = VerticalAlign.Top;

            public Color? BackgroundColor;
            public float CornerRadius = 0f;
            public float CornerRadiusTopLeft;
            public float CornerRadiusTopRight;
            public float CornerRadiusBottomRight;
            public float CornerRadiusBottomLeft;

            public TableModels.TextStyle? ResolvedTextStyle;
            public List<ResolvedRun> Runs = new();

            // cell-wide defaults (used as baseline for sides)
            public Color BorderColor = Color.Black;
            public float BorderWidth = PdfDefaults.DefaultBorderWidth;
            public TableModels.BorderStyle? BorderStyle;

            // side enable flags (come from the cell)
            public bool BorderTop = true, BorderRight = true, BorderBottom = true, BorderLeft = true;

            // per-side actual color/width used when drawing
            public Color BorderColorTop, BorderColorRight, BorderColorBottom, BorderColorLeft;
            public float BorderWidthTop, BorderWidthRight, BorderWidthBottom, BorderWidthLeft;
            public TableModels.BorderStyle? BorderStyleTop, BorderStyleRight, BorderStyleBottom, BorderStyleLeft;
            public BorderDrawSpec DrawTop = new BorderDrawSpec();
            public BorderDrawSpec DrawRight = new BorderDrawSpec();
            public BorderDrawSpec DrawBottom = new BorderDrawSpec();
            public BorderDrawSpec DrawLeft = new BorderDrawSpec();

            // padding
            public float? Padding; public float? PaddingTop, PaddingRight, PaddingBottom, PaddingLeft;
        }


        private static Effective BuildEffectiveCell(
            TableElement table,
            TableRow row,
            TableCell cell,
            int columnIndex,
            TableModels.BandFill? rowBand,
            TableModels.BandFill? columnBand)
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
                CornerRadiusTopLeft = cell.CornerRadiusTopLeft > 0 ? cell.CornerRadiusTopLeft : cell.CornerRadius,
                CornerRadiusTopRight = cell.CornerRadiusTopRight > 0 ? cell.CornerRadiusTopRight : cell.CornerRadius,
                CornerRadiusBottomRight = cell.CornerRadiusBottomRight > 0 ? cell.CornerRadiusBottomRight : cell.CornerRadius,
                CornerRadiusBottomLeft = cell.CornerRadiusBottomLeft > 0 ? cell.CornerRadiusBottomLeft : cell.CornerRadius,

                // baseline for borders comes from TABLE, not the cell's default black
                BorderColor = table.BorderColor,
                BorderWidth = cell.BorderWidth > 0 ? cell.BorderWidth
                            : table.BorderWidth > 0 ? table.BorderWidth : PdfDefaults.DefaultBorderWidth,
                BorderStyle = cell.BorderStyle?.Clone(),

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
            e.BorderStyleTop = cell.BorderStyleTop?.Clone();
            e.BorderStyleRight = cell.BorderStyleRight?.Clone();
            e.BorderStyleBottom = cell.BorderStyleBottom?.Clone();
            e.BorderStyleLeft = cell.BorderStyleLeft?.Clone();

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

            var resolvedStyle = ResolveCellTextStyle(table, row, cell, columnIndex);
            e.ResolvedTextStyle = resolvedStyle;
            if (!string.IsNullOrWhiteSpace(resolvedStyle.FontFamily))
                e.Font = resolvedStyle.FontFamily;
            if (resolvedStyle.FontSize > 0)
                e.FontSize = resolvedStyle.FontSize;
            e.TextColor = resolvedStyle.TextColor;
            e.Bold = resolvedStyle.Bold;
            e.Italic = resolvedStyle.Italic;
            e.Underline = resolvedStyle.Underline;
            e.Strikethrough = resolvedStyle.Strikethrough;
            e.SmallCaps = resolvedStyle.SmallCaps;
            if (resolvedStyle.LineHeight.HasValue)
                e.LineHeight = resolvedStyle.LineHeight;
            e.HorizontalAlign = resolvedStyle.HorizontalAlign;
            e.VerticalAlign = resolvedStyle.VerticalAlign;
            e.FlowDirection = resolvedStyle.FlowDirection;
            if (!e.BackgroundColor.HasValue && resolvedStyle.BackgroundColor.HasValue)
                e.BackgroundColor = resolvedStyle.BackgroundColor;
            e.Runs = ResolveRuns(cell, resolvedStyle);

            var tableInner = table.InnerBorder;
            var tableDefaultStyle = table.BorderStyle;
            var rowBorderOverride = rowBand?.BorderOverride;
            var columnBorderOverride = columnBand?.BorderOverride;

            e.BorderStyleTop = PickBorderStyle(e.BorderStyleTop, e.BorderStyle, rowBorderOverride, tableInner, tableDefaultStyle);
            e.BorderStyleBottom = PickBorderStyle(e.BorderStyleBottom, e.BorderStyle, rowBorderOverride, tableInner, tableDefaultStyle);
            e.BorderStyleLeft = PickBorderStyle(e.BorderStyleLeft, e.BorderStyle, columnBorderOverride, tableInner, tableDefaultStyle);
            e.BorderStyleRight = PickBorderStyle(e.BorderStyleRight, e.BorderStyle, columnBorderOverride, tableInner, tableDefaultStyle);

            ApplyBorderStyle(e.BorderStyleTop, ref e.BorderColorTop, ref e.BorderWidthTop, cell.BorderColorTop, cell.BorderWidthTop);
            ApplyBorderStyle(e.BorderStyleBottom, ref e.BorderColorBottom, ref e.BorderWidthBottom, cell.BorderColorBottom, cell.BorderWidthBottom);
            ApplyBorderStyle(e.BorderStyleLeft, ref e.BorderColorLeft, ref e.BorderWidthLeft, cell.BorderColorLeft, cell.BorderWidthLeft);
            ApplyBorderStyle(e.BorderStyleRight, ref e.BorderColorRight, ref e.BorderWidthRight, cell.BorderColorRight, cell.BorderWidthRight);

            e.DrawTop = new BorderDrawSpec { Color = e.BorderColorTop, Width = e.BorderWidthTop, Style = e.BorderStyleTop };
            e.DrawBottom = new BorderDrawSpec { Color = e.BorderColorBottom, Width = e.BorderWidthBottom, Style = e.BorderStyleBottom };
            e.DrawLeft = new BorderDrawSpec { Color = e.BorderColorLeft, Width = e.BorderWidthLeft, Style = e.BorderStyleLeft };
            e.DrawRight = new BorderDrawSpec { Color = e.BorderColorRight, Width = e.BorderWidthRight, Style = e.BorderStyleRight };

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
                var rowBand = ResolveRowBand(table, table.RowBandOffset + rowIndex);

                while (colIndex < totalCols && covered.Contains((rowIndex, colIndex))) colIndex++;

                foreach (var cell in row.Cells)
                {
                    while (colIndex < totalCols && covered.Contains((rowIndex, colIndex))) colIndex++;

                    int colSpan = Math.Max(1, cell.ColSpan);
                    int rowSpan = Math.Max(1, cell.RowSpan);

                    float cw = 0f;
                    for (int c = 0; c < colSpan; c++) cw += colWidths[colIndex + c];

                    var columnBand = ResolveColumnBand(table, colIndex);
                    var effective = BuildEffectiveCell(table, row, cell, colIndex, rowBand, columnBand);
                    float req = cell.HasContainerContent
                        ? cell.CachedContentHeight
                        : MeasureCellContentHeight(table, effective, cw);

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

        private static float MeasureCellContentHeight(TableElement table, Effective cell, float cellWidth)
        {
            var pad = GetPadding(cell, table.CellPadding);
            float usableWidth = Math.Max(0, cellWidth - pad.left - pad.right);

            var wrapMode = cell.ResolvedTextStyle?.Wrap ?? MapWrap(table.OverflowPolicy);
            if (cell.WordBreak == CellWordBreak.BreakWord && wrapMode == TableModels.TextWrapMode.Wrap)
                wrapMode = TableModels.TextWrapMode.Hyphenate;

            float lineMult = cell.LineHeight
                             ?? cell.ResolvedTextStyle?.LineHeight
                             ?? PdfDefaults.LineHeightMultiplier;

            int? maxLines = cell.MaxLines;
            if (wrapMode == TableModels.TextWrapMode.EllipsisWhenClipped)
                maxLines = 1;
            else if (wrapMode == TableModels.TextWrapMode.NoWrap && !maxLines.HasValue)
                maxLines = 1;

            var fragments = TokenizeRuns(cell.Runs);
            if (fragments.Count == 0)
                return pad.top + pad.bottom;

            var lines = LayoutFragments(fragments, cell, wrapMode, maxLines, usableWidth, lineMult);
            if (lines.Count == 0)
                return pad.top + pad.bottom;

            if (wrapMode == TableModels.TextWrapMode.EllipsisWhenClipped && lines.Count > 0)
            {
                ApplyEllipsis(lines[0], usableWidth, cell);
                lines = new List<LineLayout> { lines[0] };
            }

            float blockHeight = lines.Sum(l => l.Height);
            return pad.top + blockHeight + pad.bottom;
        }

        private static bool HasCoverageAbove(HashSet<(int row, int col)> covered, int row, int colStart, int colSpan)
        {
            if (row == 0) return false;
            for (int c = 0; c < colSpan; c++)
                if (covered.Contains((row, colStart + c))) return true;
            return false;
        }

        // ---------- Text rendering with overflow, alignment, rotation ----------
        private static TableModels.TextWrapMode MapWrap(CellOverflowPolicy policy)
            => policy switch
            {
                CellOverflowPolicy.Ellipsis => TableModels.TextWrapMode.EllipsisWhenClipped,
                CellOverflowPolicy.Clip => TableModels.TextWrapMode.NoWrap,
                _ => TableModels.TextWrapMode.Wrap
            };

        private static Fragment CreateFragment(ResolvedRun run, string text, bool isWhitespace, bool isLineBreak = false, ShapedLine? shapedOverride = null)
        {
            if (isLineBreak)
            {
                return new Fragment
                {
                    Run = run,
                    Text = string.Empty,
                    Width = 0f,
                    IsWhitespace = false,
                    IsLineBreak = true,
                    ShapedLine = null,
                    Ascent = 0f,
                    Descent = 0f
                };
            }

            if (string.IsNullOrEmpty(text))
            {
                return new Fragment
                {
                    Run = run,
                    Text = string.Empty,
                    Width = 0f,
                    IsWhitespace = isWhitespace,
                    IsLineBreak = false,
                    ShapedLine = null,
                    Ascent = 0f,
                    Descent = 0f
                };
            }

            var shaped = shapedOverride ?? ShapeFragmentText(run, text);
            return new Fragment
            {
                Run = run,
                Text = text,
                Width = shaped.Width,
                IsWhitespace = isWhitespace,
                IsLineBreak = false,
                ShapedLine = shaped,
                Ascent = shaped.Ascent,
                Descent = shaped.Descent
            };
        }

        private static ShapedLine ShapeFragmentText(ResolvedRun run, string text)
        {
            text ??= string.Empty;

            run.ShapeCache ??= new Dictionary<string, ShapedLine>(StringComparer.Ordinal);
            if (run.ShapeCache.TryGetValue(text, out var cached))
                return cached;

            string fontFamily = !string.IsNullOrWhiteSpace(run.Style.FontFamily)
                ? run.Style.FontFamily!
                : run.BaseFont?.Split('-')[0] ?? "Helvetica";
            float fontSize = EffectiveRunFontSize(run);
            float lineHeight = run.Style.LineHeight ?? 1f;

            var request = new TextShapingRequest(
                text,
                fontFamily,
                fontSize,
                lineHeight,
                maxWidth: 0f,
                bold: run.Style.Bold,
                italic: run.Style.Italic,
                smallCaps: run.Style.SmallCaps,
                monospace: false,
                fallbackFonts: run.FallbackFonts,
                flowDirection: run.Style.Direction.HasValue
                    ? TypographyDirectionResolver.Resolve(run.Style.Direction.Value, text, run.Style.FlowDirection)
                    : run.Style.FlowDirection);

            var paragraph = TextShaper.Shared.ShapeParagraph(request);
            var shapedLine = paragraph.Lines.FirstOrDefault()
                              ?? new ShapedLine(text, Array.Empty<ShapedRun>(), 0f, fontSize * 0.8f, fontSize * 0.2f, fontSize);

            run.ShapeCache[text] = shapedLine;
            return shapedLine;
        }

        private static List<Fragment> TokenizeRuns(List<ResolvedRun> runs)
        {
            var fragments = new List<Fragment>();
            foreach (var run in runs)
            {
                string text = run.Text ?? string.Empty;
                if (run.Style.SmallCaps)
                    text = text.ToUpperInvariant();

                text = text.Replace("\r\n", "\n").Replace("\r", "\n");
                int index = 0;
                while (index < text.Length)
                {
                    char ch = text[index];
                    if (ch == '\n')
                    {
                        fragments.Add(CreateFragment(run, string.Empty, isWhitespace: false, isLineBreak: true));
                        index++;
                        continue;
                    }

                    bool isWhitespace = char.IsWhiteSpace(ch);
                    int start = index;
                    while (index < text.Length && text[index] != '\n' && char.IsWhiteSpace(text[index]) == isWhitespace)
                        index++;

                    string slice = text.Substring(start, index - start);
                    fragments.Add(CreateFragment(run, slice, isWhitespace));
                }
            }

            return fragments;
        }

        private static List<LineLayout> LayoutFragments(
            List<Fragment> fragments,
            Effective cell,
            TableModels.TextWrapMode wrapMode,
            int? maxLines,
            float availableWidth,
            float lineMult)
        {
            var lines = new List<LineLayout>();
            if (fragments.Count == 0) return lines;

            var queue = new Queue<Fragment>(fragments);
            var currentLine = new LineLayout();
            float currentWidth = 0f;

            void FinalizeCurrentLine()
            {
                TrimTrailingWhitespace(currentLine);
                if (currentLine.Fragments.Count == 0) return;
                FinalizeLineMetrics(currentLine, lineMult, cell.FontSize);
                lines.Add(currentLine);
                currentLine = new LineLayout();
                currentWidth = 0f;
            }

            while (queue.Count > 0)
            {
                if (maxLines.HasValue && lines.Count >= maxLines.Value)
                    break;

                var fragment = queue.Dequeue();

                if (fragment.IsLineBreak)
                {
                    FinalizeCurrentLine();
                    continue;
                }

                if (wrapMode == TableModels.TextWrapMode.NoWrap || wrapMode == TableModels.TextWrapMode.EllipsisWhenClipped)
                {
                    if (fragment.IsWhitespace && currentLine.Fragments.Count == 0)
                        continue;
                    currentLine.Fragments.Add(fragment);
                    currentWidth += fragment.Width;
                    continue;
                }

                if (fragment.IsWhitespace)
                {
                    if (currentLine.Fragments.Count == 0)
                        continue;
                    currentLine.Fragments.Add(fragment);
                    currentWidth += fragment.Width;
                    continue;
                }

                float limit = Math.Max(0, availableWidth);
                bool exceeds = limit > 0 && currentWidth + fragment.Width > limit + 0.1f;

                if (exceeds && currentLine.Fragments.Count > 0)
                {
                    FinalizeCurrentLine();
                    if (maxLines.HasValue && lines.Count >= maxLines.Value)
                        break;
                    exceeds = limit > 0 && fragment.Width > limit + 0.1f;
                }

                if (wrapMode == TableModels.TextWrapMode.Hyphenate &&
                    exceeds &&
                    limit > 0)
                {
                    var split = SplitFragmentForHyphenation(fragment, Math.Max(0, limit - currentWidth));
                    if (split.First != null)
                    {
                        currentLine.Fragments.Add(split.First);
                        currentWidth += split.First.Width;

                        if (split.Hyphen != null)
                        {
                            currentLine.Fragments.Add(split.Hyphen);
                            currentWidth += split.Hyphen.Width;
                        }

                        FinalizeCurrentLine();
                        if (split.Remaining != null)
                            queue = new Queue<Fragment>(new[] { split.Remaining }.Concat(queue));
                        continue;
                    }
                }

                currentLine.Fragments.Add(fragment);
                currentWidth += fragment.Width;
            }

            if (currentLine.Fragments.Count > 0 &&
                (!maxLines.HasValue || lines.Count < maxLines.Value))
            {
                FinalizeCurrentLine();
            }

            return lines;
        }

        private static void ApplyEllipsis(LineLayout line, float availableWidth, Effective cell)
        {
            TrimTrailingWhitespace(line);
            if (line.Fragments.Count == 0) return;
            if (availableWidth <= 0) return;
            if (line.Width <= availableWidth) return;

            const string ellipsis = "...";
            var last = line.Fragments.Last();
            var ellShape = ShapeFragmentText(last.Run, ellipsis);
            float ellWidth = ellShape.Width;
            float allowed = Math.Max(0, availableWidth - ellWidth);

            int idx = line.Fragments.Count - 1;
            while (idx >= 0 && line.Fragments[idx].Text.Length == 0)
                idx--;

            if (idx < 0)
            {
                line.Fragments.Clear();
                line.Fragments.Add(CreateFragment(last.Run, ellipsis, isWhitespace: false, shapedOverride: ellShape));
                FinalizeLineMetrics(line, cell.LineHeight ?? PdfDefaults.LineHeightMultiplier, cell.FontSize);
                return;
            }

            var target = line.Fragments[idx];
            string content = target.Text;

            while (content.Length > 0 && ShapeFragmentText(target.Run, content).Width > allowed)
                content = content[..^1];

            target.Text = content;
            if (target.Text.Length > 0)
            {
                var targetShape = ShapeFragmentText(target.Run, content);
                target.Width = targetShape.Width;
                target.ShapedLine = targetShape;
                target.Ascent = targetShape.Ascent;
                target.Descent = targetShape.Descent;
            }
            else
            {
                target.Width = 0f;
                target.ShapedLine = null;
                target.Ascent = 0f;
                target.Descent = 0f;
            }

            if (target.Text.Length == 0)
                line.Fragments.RemoveAt(idx);

            for (int removeIndex = line.Fragments.Count - 1; removeIndex > idx; removeIndex--)
                line.Fragments.RemoveAt(removeIndex);

            TrimTrailingWhitespace(line);

            line.Fragments.Add(CreateFragment(target.Run, ellipsis, isWhitespace: false, shapedOverride: ellShape));

            FinalizeLineMetrics(line, cell.LineHeight ?? PdfDefaults.LineHeightMultiplier, cell.FontSize);
        }

        private static float ResolveLineStartX(HorizontalAlign align, float x, float cellWidth, float padLeft, float padRight, float lineWidth)
        {
            return align switch
            {
                HorizontalAlign.Center => x + Math.Max(padLeft, (cellWidth - lineWidth) / 2f),
                HorizontalAlign.Right => x + Math.Max(padLeft, cellWidth - padRight - lineWidth),
                _ => x + padLeft
            };
        }

        private static void DrawTextDecorations(StringBuilder sb, ResolvedRun run, float fragmentWidth, float startX, float baselineY, float rotationDeg)
        {
            Color decoColor = run.DecorationColor ?? run.Color;
            float size = EffectiveRunFontSize(run);
            float thickness = Math.Max(0.25f, run.DecorationThickness ?? size * 0.07f);
            var style = BuildDecorationBorderStyle(run.DecorationStyle, thickness, decoColor);

            if (run.Underline)
            {
                float offset = baselineY - size * 0.15f;
                DrawDecorationStroke(sb, style, startX, offset, startX + fragmentWidth, offset, startX, baselineY, rotationDeg);
            }

            if (run.Strikethrough)
            {
                float offset = baselineY + size * 0.3f;
                if (run.DecorationStyle == TextDecorationStyle.Double)
                {
                    float delta = thickness * 2.0f;
                    DrawDecorationStroke(sb, style, startX, offset, startX + fragmentWidth, offset, startX, baselineY, rotationDeg);
                    DrawDecorationStroke(sb, style, startX, offset + delta, startX + fragmentWidth, offset + delta, startX, baselineY, rotationDeg);
                }
                else
                {
                    DrawDecorationStroke(sb, style, startX, offset, startX + fragmentWidth, offset, startX, baselineY, rotationDeg);
                }
            }
            if (run.Overline)
            {
                float offset = baselineY + size * 0.8f;
                DrawDecorationStroke(sb, style, startX, offset, startX + fragmentWidth, offset, startX, baselineY, rotationDeg);
            }
        }

        private static void DrawDecorationStroke(StringBuilder sb, TableModels.BorderStyle style, float x1, float y1, float x2, float y2, float pivotX, float pivotY, float rotationDeg)
        {
            if (Math.Abs(rotationDeg) > 0.01f)
            {
                var start = RotatePoint(x1, y1, pivotX, pivotY, rotationDeg);
                var end = RotatePoint(x2, y2, pivotX, pivotY, rotationDeg);
                StrokeLine(sb, start.x, start.y, end.x, end.y, style.Color, style.Width, style);
            }
            else
            {
                StrokeLine(sb, x1, y1, x2, y2, style.Color, style.Width, style);
            }
        }

        private static (float x, float y) RotatePoint(float px, float py, float originX, float originY, float angleDeg)
        {
            double rad = angleDeg * Math.PI / 180.0;
            double cos = Math.Cos(rad);
            double sin = Math.Sin(rad);
            double dx = px - originX;
            double dy = py - originY;
            double rx = originX + dx * cos - dy * sin;
            double ry = originY + dx * sin + dy * cos;
            return ((float)rx, (float)ry);
        }

        private static TableModels.BorderStyle BuildDecorationBorderStyle(TextDecorationStyle decorationStyle, float thickness, Color color)
        {
            var border = new TableModels.BorderStyle
            {
                Color = color,
                Width = thickness,
                LineCap = TableModels.BorderLineCap.Butt,
                LineJoin = TableModels.BorderLineJoin.Miter
            };

            switch (decorationStyle)
            {
                case TextDecorationStyle.Dotted:
                    border.DashPattern = new[] { thickness, thickness };
                    border.LineCap = TableModels.BorderLineCap.Round;
                    break;
                case TextDecorationStyle.Dashed:
                    border.DashPattern = new[] { thickness * 2.5f, thickness * 1.2f };
                    break;
                case TextDecorationStyle.Double:
                    border.Width = thickness * 0.6f;
                    break;
                default:
                    border.DashPattern = null;
                    break;
            }

            return border;
        }

        private static (Fragment? First, Fragment? Hyphen, Fragment? Remaining) SplitFragmentForHyphenation(Fragment fragment, float availableWidth)
        {
            var run = fragment.Run;
            string text = fragment.Text;
            if (string.IsNullOrEmpty(text)) return (null, null, null);

            var hyphenShape = ShapeFragmentText(run, "-");
            float remaining = availableWidth - hyphenShape.Width;
            if (remaining <= 0) return (null, null, null);

            int length = 0;
            float width = 0f;
            while (length < text.Length)
            {
                length++;
                width = ShapeFragmentText(run, text[..length]).Width;
                if (width > remaining)
                {
                    length--;
                    break;
                }
            }

            if (length <= 0) return (null, null, null);

            string head = text[..length];
            string tail = text[length..];

            var firstShape = ShapeFragmentText(run, head);
            var first = CreateFragment(run, head, isWhitespace: false, shapedOverride: firstShape);

            var hyphen = CreateFragment(run, "-", isWhitespace: false, shapedOverride: hyphenShape);

            Fragment? remainder = null;
            if (tail.Length > 0)
            {
                var tailShape = ShapeFragmentText(run, tail);
                remainder = CreateFragment(run, tail, isWhitespace: false, shapedOverride: tailShape);
            }

            return (first, hyphen, remainder);
        }

        private static void TrimTrailingWhitespace(LineLayout line)
        {
            int idx = line.Fragments.Count - 1;
            while (idx >= 0)
            {
                var frag = line.Fragments[idx];
                if (!frag.IsWhitespace || frag.Text.Any(ch => !char.IsWhiteSpace(ch)))
                    break;
                line.Fragments.RemoveAt(idx--);
            }
        }

        private static void FinalizeLineMetrics(LineLayout line, float lineMult, float defaultFontSize)
        {
            line.Width = line.Fragments.Sum(f => f.Width);
            float maxFont = line.Fragments.Count > 0
                ? line.Fragments.Max(f => EffectiveRunFontSize(f.Run))
                : (defaultFontSize > 0 ? defaultFontSize : 10f);
            float ascent = line.Fragments.Count > 0
                ? line.Fragments.Max(f => f.Ascent)
                : maxFont * 0.8f;
            float descent = line.Fragments.Count > 0
                ? line.Fragments.Max(f => f.Descent)
                : maxFont * 0.2f;
            float natural = ascent + descent;
            float heightTarget = maxFont * lineMult;
            float height = Math.Max(natural, heightTarget);
            line.MaxFontSize = maxFont;
            line.Ascent = ascent;
            line.Descent = descent;
            line.Height = height;
            line.BaselineOffset = height - descent;
        }

        private static float EffectiveRunFontSize(ResolvedRun run)
        {
            float size = run.FontSize > 0 ? run.FontSize : 10f;
            if (run.Superscript || run.Subscript)
                size *= 0.8f;
            return size;
        }

        private static void RenderCellText(
            StringBuilder sb,
            TableElement table,
            Effective cell,
            float x,
            float yTop,
            float cellWidth,
            float cellHeight,
            (float top, float right, float bottom, float left) pad,
            PdfRenderContext context)
        {
            if (cell.Runs.Count == 0) return;

            float usableWidth = Math.Max(0, cellWidth - pad.left - pad.right);
            var wrapMode = cell.ResolvedTextStyle?.Wrap ?? MapWrap(table.OverflowPolicy);
            if (cell.WordBreak == CellWordBreak.BreakWord && wrapMode == TableModels.TextWrapMode.Wrap)
                wrapMode = TableModels.TextWrapMode.Hyphenate;

            float lineMult = cell.LineHeight
                             ?? cell.ResolvedTextStyle?.LineHeight
                             ?? PdfDefaults.LineHeightMultiplier;

            int? maxLines = cell.MaxLines;
            if (wrapMode == TableModels.TextWrapMode.EllipsisWhenClipped)
                maxLines = 1;
            else if (wrapMode == TableModels.TextWrapMode.NoWrap && !maxLines.HasValue)
                maxLines = 1;

            var fragments = TokenizeRuns(cell.Runs);
            if (fragments.Count == 0) return;

            var lines = LayoutFragments(fragments, cell, wrapMode, maxLines, usableWidth, lineMult);
            if (lines.Count == 0) return;

            if (wrapMode == TableModels.TextWrapMode.EllipsisWhenClipped && lines.Count > 0)
            {
                ApplyEllipsis(lines[0], usableWidth, cell);
                lines = new List<LineLayout> { lines[0] };
            }

            float blockHeight = lines.Sum(l => l.Height);
            float blockTop = GetVerticalAlignedY(
                cell.VerticalAlign,
                yTop,
                cellHeight,
                blockHeight,
                pad.top,
                pad.bottom);

            float currentTop = blockTop;
            bool isRtl = cell.FlowDirection == FlowDirection.RightToLeft;
            foreach (var line in lines)
            {
                float lineStartX = ResolveLineStartX(cell.HorizontalAlign, x, cellWidth, pad.left, pad.right, line.Width);
                float cursorX = isRtl ? lineStartX + line.Width : lineStartX;
                float baselineY = currentTop - line.BaselineOffset;

                foreach (var fragment in line.Fragments)
                {
                    if (fragment.Text.Length == 0)
                    {
                        cursorX += isRtl ? -fragment.Width : fragment.Width;
                        continue;
                    }

                    var run = fragment.Run;
                    float runFontSize = run.FontSize;
                    float baselineAdjust = 0f;
                    if (run.Superscript)
                    {
                        baselineAdjust = runFontSize * 0.35f;
                        runFontSize *= 0.8f;
                    }
                    else if (run.Subscript)
                    {
                        baselineAdjust = -runFontSize * 0.20f;
                        runFontSize *= 0.8f;
                    }

                    float tx = isRtl ? cursorX - fragment.Width : cursorX;
                    float ty = baselineY + baselineAdjust;
                    float rotation = run.RotationOverride != 0 ? run.RotationOverride : cell.RotationDegrees;

                    var shapedLine = fragment.ShapedLine ?? ShapeFragmentText(run, fragment.Text);
                    if (shapedLine == null || shapedLine.Runs.Count == 0)
                    {
                        cursorX += isRtl ? -fragment.Width : fragment.Width;
                        continue;
                    }

                    string fill = ToRgbFill(run.Color) ?? "0 0 0 rg";
                    float runCursor = tx;

                    if (Math.Abs(rotation) > 0.01f)
                    {
                        double radians = rotation * Math.PI / 180.0;
                        double cos = Math.Cos(radians);
                        double sin = Math.Sin(radians);

                        foreach (var shapedRun in shapedLine.Runs)
                        {
                            if (shapedRun.Glyphs.Count == 0)
                            {
                                runCursor += shapedRun.Width;
                                continue;
                            }

                            var encoded = GlyphRunEncoder.Encode(shapedRun, context);
                            bool preserveLogicalOrder = TypographyDirectionResolver.ContainsRightToLeft(shapedRun.Text);
                            if (preserveLogicalOrder)
                                sb.Append($"/Span << /ActualText {PdfStringEncoder.Encode(shapedRun.Text)} >> BDC\n");
                            sb.Append("q ");
                            sb.Append($"{N(1)} {N(0)} {N(0)} {N(1)} {N(runCursor)} {N(ty)} cm ");
                            sb.Append($"{N(cos)} {N(sin)} {N(-sin)} {N(cos)} 0 0 cm ");
                            sb.Append("BT ");
                            sb.Append($"{encoded.FontResourceName} {N(shapedRun.FontSize)} Tf ");
                            sb.Append(fill);
                            sb.Append(' ');
                            sb.Append($"{encoded.TjCommand} ET\n");
                            sb.Append("Q\n");
                            if (preserveLogicalOrder)
                                sb.Append("EMC\n");
                            runCursor += shapedRun.Width;
                        }
                    }
                    else
                    {
                        foreach (var shapedRun in shapedLine.Runs)
                        {
                            if (shapedRun.Glyphs.Count == 0)
                            {
                                runCursor += shapedRun.Width;
                                continue;
                            }

                            var encoded = GlyphRunEncoder.Encode(shapedRun, context);
                            bool preserveLogicalOrder = TypographyDirectionResolver.ContainsRightToLeft(shapedRun.Text);
                            if (preserveLogicalOrder)
                                sb.Append($"/Span << /ActualText {PdfStringEncoder.Encode(shapedRun.Text)} >> BDC\n");
                            sb.Append("BT ");
                            sb.Append($"{encoded.FontResourceName} {N(shapedRun.FontSize)} Tf ");
                            sb.Append(fill);
                            sb.Append(' ');
                            sb.Append($"{N(runCursor)} {N(ty)} Td ");
                            sb.Append($"{encoded.TjCommand} ET\n");
                            if (preserveLogicalOrder)
                                sb.Append("EMC\n");
                            runCursor += shapedRun.Width;
                        }
                    }

                    if (run.Underline || run.Strikethrough || run.Overline)
                        DrawTextDecorations(sb, run, fragment.Width, tx, ty, rotation);

                    cursorX += isRtl ? -fragment.Width : fragment.Width;
                }

                currentTop -= line.Height;
            }
        }

        // ---------- Geometry & drawing ----------
        private static void FillRect(StringBuilder sb, float x, float y, float w, float h, Color color)
        {
            sb.Append($"{ToRgbFill(color)} {N(x)} {N(y)} {N(w)} {N(h)} re f\n");
        }

        private static void FillRoundedRect(StringBuilder sb, float x, float y, float w, float h,
                                            float rTL, float rTR, float rBR, float rBL, Color color)
        {
            NormalizeRadii(w, h, ref rTL, ref rTR, ref rBR, ref rBL);
            if (rTL <= 0 && rTR <= 0 && rBR <= 0 && rBL <= 0)
            {
                FillRect(sb, x, y, w, h, color);
                return;
            }

            sb.Append(ToRgbFill(color) + " ");
            AppendRoundedRectPath(sb, x, y, w, h, rTL, rTR, rBR, rBL);
            sb.Append("f\n");
        }

        private static void StrokeRoundedRect(StringBuilder sb, float x, float y, float w, float h,
                                              float rTL, float rTR, float rBR, float rBL,
                                              Color color, float width, TableModels.BorderStyle? style)
        {
            NormalizeRadii(w, h, ref rTL, ref rTR, ref rBR, ref rBL);
            width = width <= 0f ? (PdfDefaults.DefaultBorderWidth > 0f ? PdfDefaults.DefaultBorderWidth : 0.5f) : width;
            width = ClampThin(width);

            if (style != null)
            {
                sb.Append("q ");
                AppendStrokeStyle(sb, style);
                sb.Append($"{ToRgbStroke(color)} {N(width)} w ");
                AppendRoundedRectPath(sb, x, y, w, h, rTL, rTR, rBR, rBL);
                sb.Append("S Q\n");
            }
            else
            {
                sb.Append($"{ToRgbStroke(color)} {N(width)} w ");
                AppendRoundedRectPath(sb, x, y, w, h, rTL, rTR, rBR, rBL);
                sb.Append("S\n");
            }
        }

        private static void AppendRoundedRectPath(StringBuilder sb, float x, float y, float w, float h,
                                                  float rTL, float rTR, float rBR, float rBL)
        {
            float x0 = x, y0 = y;
            float x1 = x + w, y1 = y + h;

            const float K = 0.552284749831f; // cubic bezier constant for quarter circle
            float oxTL = rTL * K, oyTL = rTL * K;
            float oxTR = rTR * K, oyTR = rTR * K;
            float oxBR = rBR * K, oyBR = rBR * K;
            float oxBL = rBL * K, oyBL = rBL * K;

            sb.Append($"{N(x0 + rBL)} {N(y0)} m ");
            sb.Append($"{N(x1 - rBR)} {N(y0)} l ");
            sb.Append($"{N(x1 - rBR + oxBR)} {N(y0)} {N(x1)} {N(y0 + rBR - oyBR)} {N(x1)} {N(y0 + rBR)} c ");
            sb.Append($"{N(x1)} {N(y1 - rTR)} l ");
            sb.Append($"{N(x1)} {N(y1 - rTR + oyTR)} {N(x1 - rTR + oxTR)} {N(y1)} {N(x1 - rTR)} {N(y1)} c ");
            sb.Append($"{N(x0 + rTL)} {N(y1)} l ");
            sb.Append($"{N(x0 + rTL - oxTL)} {N(y1)} {N(x0)} {N(y1 - rTL + oyTL)} {N(x0)} {N(y1 - rTL)} c ");
            sb.Append($"{N(x0)} {N(y0 + rBL)} l ");
            sb.Append($"{N(x0)} {N(y0 + rBL - oyBL)} {N(x0 + rBL - oxBL)} {N(y0)} {N(x0 + rBL)} {N(y0)} c ");
        }

        private static void NormalizeRadii(float w, float h,
                                           ref float rTL, ref float rTR, ref float rBR, ref float rBL)
        {
            rTL = Math.Max(0, Math.Min(rTL, Math.Min(w, h) / 2f));
            rTR = Math.Max(0, Math.Min(rTR, Math.Min(w, h) / 2f));
            rBR = Math.Max(0, Math.Min(rBR, Math.Min(w, h) / 2f));
            rBL = Math.Max(0, Math.Min(rBL, Math.Min(w, h) / 2f));

            float scale = 1f;
            float sumTop = rTL + rTR;
            if (sumTop > w && sumTop > 0) scale = Math.Min(scale, w / sumTop);
            float sumBottom = rBL + rBR;
            if (sumBottom > w && sumBottom > 0) scale = Math.Min(scale, w / sumBottom);
            float sumLeft = rTL + rBL;
            if (sumLeft > h && sumLeft > 0) scale = Math.Min(scale, h / sumLeft);
            float sumRight = rTR + rBR;
            if (sumRight > h && sumRight > 0) scale = Math.Min(scale, h / sumRight);

            if (scale < 1f)
            {
                rTL *= scale;
                rTR *= scale;
                rBR *= scale;
                rBL *= scale;
            }
        }

        private static void StrokeLine(StringBuilder sb, float x1, float y1, float x2, float y2, Color color, float width, TableModels.BorderStyle? style)
        {
            width = width <= 0f ? (PdfDefaults.DefaultBorderWidth > 0f ? PdfDefaults.DefaultBorderWidth : 0.5f) : width;
            width = ClampThin(width);

            if (width <= 1f)
            {
                x1 = AlignHalf(x1); x2 = AlignHalf(x2);
                y1 = AlignHalf(y1); y2 = AlignHalf(y2);
            }

            if (style != null)
            {
                sb.Append("q ");
                AppendStrokeStyle(sb, style);
                sb.Append($"{ToRgbStroke(color)} {N(width)} w {N(x1)} {N(y1)} m {N(x2)} {N(y2)} l S Q\n");
            }
            else
            {
                sb.Append($"{ToRgbStroke(color)} {N(width)} w {N(x1)} {N(y1)} m {N(x2)} {N(y2)} l S\n");
            }
        }




        private static void StrokeRect(StringBuilder sb, float x, float topY, float w, float h, Color color, float width, TableModels.BorderStyle? style)
        {
            if (h <= 0) return;
            float bottomY = topY - h;
            width = width <= 0f ? (PdfDefaults.DefaultBorderWidth > 0f ? PdfDefaults.DefaultBorderWidth : 0.5f) : width;
            width = ClampThin(width);

            if (style != null)
            {
                sb.Append("q ");
                AppendStrokeStyle(sb, style);
                sb.Append($"{ToRgbStroke(color)} {N(width)} w ");
                sb.Append($"{N(x)} {N(topY)} m {N(x + w)} {N(topY)} l {N(x + w)} {N(bottomY)} l {N(x)} {N(bottomY)} l h S Q\n");
            }
            else
            {
                sb.Append($"{ToRgbStroke(color)} {N(width)} w ");
                sb.Append($"{N(x)} {N(topY)} m {N(x + w)} {N(topY)} l {N(x + w)} {N(bottomY)} l {N(x)} {N(bottomY)} l h S\n");
            }
        }

        private static void AppendStrokeStyle(StringBuilder sb, TableModels.BorderStyle style)
        {
            if (style.DashPattern != null && style.DashPattern.Count > 0)
            {
                var dash = string.Join(" ", style.DashPattern.Select(v => N(v)));
                sb.Append($"[{dash}] {N(style.DashPhase)} d ");
            }
            else
            {
                sb.Append("[] 0 d ");
            }

            int lineCap = style.LineCap switch
            {
                TableModels.BorderLineCap.Round => 1,
                TableModels.BorderLineCap.Square => 2,
                _ => 0
            };
            int lineJoin = style.LineJoin switch
            {
                TableModels.BorderLineJoin.Round => 1,
                TableModels.BorderLineJoin.Bevel => 2,
                _ => 0
            };

            sb.Append($"{lineCap} J {lineJoin} j ");

            if (style.MiterLimit.HasValue && style.MiterLimit.Value > 0)
                sb.Append($"{N(style.MiterLimit.Value)} M ");
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
        private static (int cellIdx, TableCell? cell) CellCoveringColumn(TableRow row, int colIndex)
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
            TableElement table,
            int rowIndex,
            int colIndex,
            HashSet<(int row, int col)> covered,
            bool cellTopOn,
            BorderDrawSpec? topSpec,
            out Edge top,
            out Edge aboveBottom)
        {
            float topWidth = topSpec?.Width ?? 0f;
            Color topColor = topSpec?.Color ?? table.BorderColor;
            bool topExplicit = cellTopOn &&
                (Math.Abs(topWidth - table.BorderWidth) > 1e-3f ||
                 topColor.ToArgb() != table.BorderColor.ToArgb());

            top = new Edge
            {
                Exists = cellTopOn,
                Width = topWidth,
                Color = topColor,
                OriginRank = topExplicit ? -2 : 0,
                BorderStyle = topSpec?.Style
            };

            var currRow = table.Rows[rowIndex];
            if (currRow.ThickTopBorder)
            {
                top.Exists = true;
                if (currRow.ThickBorderWidth > top.Width) top.Width = currRow.ThickBorderWidth;
                top.Color = currRow.ThickBorderColor ?? table.BorderColor;
                top.OriginRank = Math.Min(top.OriginRank, 1);
                top.BorderStyle ??= table.BorderStyle?.Clone();
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
                    OriginRank = 1,
                    BorderStyle = table.BorderStyle?.Clone()
                };
            }

            var (_, aboveCell) = CellCoveringColumn(aboveRow, colIndex);
            if (aboveCell != null && aboveCell.BorderBottom)
            {
                SideSpec(aboveCell.BorderColorBottom, aboveCell.BorderWidthBottom,
                         aboveCell.BorderColor, aboveCell.BorderWidth,
                         table.BorderColor, table.BorderWidth,
                         out var col, out var w, out var isExp);

                var style = aboveCell.BorderStyleBottom
                            ?? aboveCell.BorderStyle
                            ?? table.BorderStyle;

                aboveBottom = new Edge
                {
                    Exists = true,
                    Width = Math.Max(aboveBottom.Width, w),
                    Color = col,
                    OriginRank = isExp ? -2 : aboveBottom.OriginRank,
                    BorderStyle = style?.Clone()
                };
            }
        }







        // BOTTOM (this cell) vs TOP (row below)
        private static void BuildBottomVsBelowTop(
            TableElement table, int rowIndex, int colIndex, int rowSpan, HashSet<(int row, int col)> covered,
            bool cellBottomOn, BorderDrawSpec? bottomSpec,
            out Edge bottom, out Edge belowTop)
        {
            float bottomWidth = bottomSpec?.Width ?? 0f;
            Color bottomColor = bottomSpec?.Color ?? table.BorderColor;
            bool bottomExplicit = cellBottomOn &&
                (Math.Abs(bottomWidth - table.BorderWidth) > 1e-3f ||
                 bottomColor.ToArgb() != table.BorderColor.ToArgb());

            bottom = new Edge
            {
                Exists = cellBottomOn,
                Width = bottomWidth,
                Color = bottomColor,
                OriginRank = bottomExplicit ? -2 : 0,
                BorderStyle = bottomSpec?.Style
            };

            var currRow = table.Rows[rowIndex];
            if (currRow.ThickBottomBorder)
            {
                bottom.Exists = true;
                if (currRow.ThickBorderWidth > bottom.Width) bottom.Width = currRow.ThickBorderWidth;
                bottom.Color = currRow.ThickBorderColor ?? table.BorderColor;
                bottom.OriginRank = Math.Min(bottom.OriginRank, 1);
                bottom.BorderStyle ??= table.BorderStyle?.Clone();
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
                    OriginRank = 1,
                    BorderStyle = table.BorderStyle?.Clone()
                };
            }

            var (_, belowCell) = CellCoveringColumn(belowRow, colIndex);
            if (belowCell != null && belowCell.BorderTop)
            {
                SideSpec(belowCell.BorderColorTop, belowCell.BorderWidthTop,
                         belowCell.BorderColor, belowCell.BorderWidth,
                         table.BorderColor, table.BorderWidth,
                         out var col, out var w, out var isExp);

                var style = belowCell.BorderStyleTop
                            ?? belowCell.BorderStyle
                            ?? table.BorderStyle;

                belowTop = new Edge
                {
                    Exists = true,
                    Width = Math.Max(belowTop.Width, w),
                    Color = col,
                    OriginRank = isExp ? -2 : belowTop.OriginRank,
                    BorderStyle = style?.Clone()
                };
            }
        }



        private static void BuildRightVsNeighborLeft(
            TableElement table,
            TableRow row,
            int colIndex,
            bool cellRightOn,
            BorderDrawSpec? rightSpec,
            out Edge right,
            out Edge neighborLeft)
        {
            float rightWidth = rightSpec?.Width ?? 0f;
            Color rightColor = rightSpec?.Color ?? table.BorderColor;
            bool rightExplicit = cellRightOn &&
                (Math.Abs(rightWidth - table.BorderWidth) > 1e-3f ||
                 rightColor.ToArgb() != table.BorderColor.ToArgb());

            right = new Edge
            {
                Exists = cellRightOn,
                Width = rightWidth,
                Color = rightColor,
                OriginRank = rightExplicit ? -2 : 0,
                BorderStyle = rightSpec?.Style
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

                        var style = nCell.BorderStyleLeft
                                    ?? nCell.BorderStyle
                                    ?? table.BorderStyle;

                        neighborLeft = new Edge
                        {
                            Exists = true,
                            Width = w,
                            Color = col,
                            OriginRank = isExp ? -2 : 0,
                            BorderStyle = style?.Clone()
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
            bool cellLeftOn, BorderDrawSpec? leftSpec,
            out Edge left, out Edge neighborRight)
        {
            float leftWidth = leftSpec?.Width ?? 0f;
            Color leftColor = leftSpec?.Color ?? Color.Black;
            left = new Edge
            {
                Exists = cellLeftOn,
                Width = leftWidth,
                Color = leftColor,
                OriginRank = 0,
                BorderStyle = leftSpec?.Style
            };
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
                    var style = nc.BorderStyleRight ?? nc.BorderStyle;
                    neighborRight = new Edge
                    {
                        Exists = true,
                        Width = w,
                        Color = c2,
                        OriginRank = 0,
                        BorderStyle = style?.Clone()
                    };
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
        private static TableModels.TextStyle ResolveCellTextStyle(
            TableElement table,
            TableRow row,
            TableCell cell,
            int columnIndex)
        {
            var style = table.DefaultTextStyle?.Clone() ?? new TableModels.TextStyle();
            if (string.IsNullOrWhiteSpace(style.FontFamily))
                style.FontFamily = table.DefaultFont;
            if (style.FontSize <= 0)
                style.FontSize = table.DefaultFontSize;

            var columnStyle = table.ColumnStyles.FirstOrDefault(s => s.Index == columnIndex);
            if (columnStyle != null)
            {
                if (!string.IsNullOrWhiteSpace(columnStyle.Font))
                    style.FontFamily = columnStyle.Font;
                if (columnStyle.FontSize.HasValue && columnStyle.FontSize.Value > 0)
                    style.FontSize = columnStyle.FontSize.Value;
                if (columnStyle.TextColor.HasValue)
                    style.TextColor = columnStyle.TextColor.Value;
                if (columnStyle.Background.HasValue && !style.BackgroundColor.HasValue)
                    style.BackgroundColor = columnStyle.Background.Value;
                if (columnStyle.HAlign.HasValue)
                    style.HorizontalAlign = columnStyle.HAlign.Value;
                if (columnStyle.VAlign.HasValue)
                    style.VerticalAlign = columnStyle.VAlign.Value;
            }

            ApplyCellLegacyFormatting(table, style, cell);

            if (cell.TextStyle != null)
                MergeTextStyles(style, cell.TextStyle);

            if (style.Direction.HasValue)
                style.FlowDirection = TypographyDirectionResolver.Resolve(style.Direction.Value, cell.Text, style.FlowDirection);

            return style;
        }

        private static void ApplyCellLegacyFormatting(
            TableElement table,
            TableModels.TextStyle target,
            TableCell cell)
        {
            if (!string.IsNullOrWhiteSpace(cell.Font))
                target.FontFamily = cell.Font;
            if (cell.FontSize > 0)
                target.FontSize = cell.FontSize;
            target.TextColor = cell.TextColor;
            if (cell.BackgroundColor.HasValue)
                target.BackgroundColor = cell.BackgroundColor;

            target.Bold = cell.Bold;
            target.Italic = cell.Italic;
            target.Underline = cell.Underline;
            target.Strikethrough = cell.Strikethrough;
            target.SmallCaps = cell.SmallCaps;
            if (cell.LineHeight.HasValue)
                target.LineHeight = cell.LineHeight;
            target.HorizontalAlign = cell.HorizontalAlign;
            target.VerticalAlign = cell.VerticalAlign;
            target.RotationDegrees = cell.RotationDegrees;

            target.Wrap = table.OverflowPolicy switch
            {
                CellOverflowPolicy.Wrap => TableModels.TextWrapMode.Wrap,
                CellOverflowPolicy.Ellipsis => TableModels.TextWrapMode.EllipsisWhenClipped,
                _ => TableModels.TextWrapMode.NoWrap
            };
        }

        private static void MergeTextStyles(TableModels.TextStyle target, TableModels.TextStyle source)
        {
            if (!string.IsNullOrWhiteSpace(source.FontFamily))
                target.FontFamily = source.FontFamily;
            if (source.FontSize > 0)
                target.FontSize = source.FontSize;
            target.Bold = source.Bold;
            target.Italic = source.Italic;
            target.SmallCaps = source.SmallCaps;
            target.Underline = source.Underline;
            target.Strikethrough = source.Strikethrough;
            target.Overline = source.Overline;
            target.TextColor = source.TextColor;
            if (source.BackgroundColor.HasValue)
                target.BackgroundColor = source.BackgroundColor;
            if (source.HighlightPadding.HasValue)
                target.HighlightPadding = source.HighlightPadding;
            if (source.LineHeight.HasValue)
                target.LineHeight = source.LineHeight;
            if (source.LetterSpacing.HasValue)
                target.LetterSpacing = source.LetterSpacing;
            if (source.WordSpacing.HasValue)
                target.WordSpacing = source.WordSpacing;
            if (source.ParagraphSpacingBefore.HasValue)
                target.ParagraphSpacingBefore = source.ParagraphSpacingBefore;
            if (source.ParagraphSpacingAfter.HasValue)
                target.ParagraphSpacingAfter = source.ParagraphSpacingAfter;
            if (source.DecorationColor.HasValue)
                target.DecorationColor = source.DecorationColor;
            if (source.DecorationThickness.HasValue)
                target.DecorationThickness = source.DecorationThickness;
            target.DecorationStyle = source.DecorationStyle;
            target.Superscript = source.Superscript;
            target.Subscript = source.Subscript;
            target.RotationDegrees = source.RotationDegrees;
            target.Wrap = source.Wrap;
            if (!string.IsNullOrEmpty(source.Hyperlink))
                target.Hyperlink = source.Hyperlink;
            if (!string.IsNullOrEmpty(source.ToolTip))
                target.ToolTip = source.ToolTip;
            target.HorizontalAlign = source.HorizontalAlign;
            target.VerticalAlign = source.VerticalAlign;
            target.Direction = source.Direction;
            target.FlowDirection = source.FlowDirection;
        }

        private static List<ResolvedRun> ResolveRuns(TableCell cell, TableModels.TextStyle baseStyle)
        {
            var runs = new List<ResolvedRun>();
            if (cell.TextRuns.Count == 0)
            {
                runs.Add(CreateResolvedRun(cell.Text ?? string.Empty, baseStyle, null));
                return runs;
            }

            foreach (var inline in cell.TextRuns)
            {
                if (inline == null) continue;
                var style = baseStyle.Clone();
                if (inline.Style != null)
                    MergeTextStyles(style, inline.Style);
                runs.Add(CreateResolvedRun(inline.Text ?? string.Empty, style, inline.FallbackFonts));
            }

            return runs;
        }

        private static ResolvedRun CreateResolvedRun(string text, TableModels.TextStyle style, List<string>? fallbackFonts)
        {
            var combinedFallback = fallbackFonts ?? style.FallbackFonts;
            var resolved = new ResolvedRun
            {
                Text = text,
                Style = style.Clone(),
                Color = style.TextColor,
                FontSize = style.FontSize > 0 ? style.FontSize : 10f,
                Underline = style.Underline,
                Strikethrough = style.Strikethrough,
                Overline = style.Overline,
                DecorationColor = style.DecorationColor,
                DecorationThickness = style.DecorationThickness,
                DecorationStyle = style.DecorationStyle,
                Background = style.BackgroundColor,
                HighlightPadding = style.HighlightPadding ?? 0f,
                Superscript = style.Superscript,
                Subscript = style.Subscript,
                RotationOverride = style.RotationDegrees,
                FallbackFonts = combinedFallback != null ? new List<string>(combinedFallback) : null
            };

            string fontFamily = string.IsNullOrWhiteSpace(style.FontFamily) ? "Helvetica" : style.FontFamily;
            resolved.BaseFont = MapFontVariant(fontFamily, style.Bold, style.Italic);
            resolved.Width = PdfLayoutUtils.EstimateTextWidth(text ?? string.Empty, fontFamily, resolved.FontSize);
            return resolved;
        }

        private static float RotatedBBoxHeight(float textWidth, float unrotatedHeight, float angleDeg)
        {
            double r = Math.Abs(angleDeg) * Math.PI / 180.0;
            return (float)(Math.Abs(textWidth * Math.Sin(r)) + Math.Abs(unrotatedHeight * Math.Cos(r)));
        }

        private static TableModels.BandFill? ResolveRowBand(TableElement table, int absoluteRowIndex)
            => ResolveBandFill(table.RowBanding?.Fills, table.RowBanding?.Step ?? 0, absoluteRowIndex);

        private static TableModels.BandFill? ResolveColumnBand(TableElement table, int columnIndex)
            => ResolveBandFill(table.ColumnBanding?.Fills, table.ColumnBanding?.Step ?? 0, columnIndex);

        private static TableModels.BandFill? ResolveBandFill(
            IReadOnlyList<TableModels.BandFill>? fills,
            int step,
            int index)
        {
            if (fills == null || fills.Count == 0 || step <= 0 || index < 0) return null;
            int band = index / step;
            return fills[band % fills.Count];
        }

        private static TableModels.BorderStyle? PickBorderStyle(params TableModels.BorderStyle?[] styles)
        {
            foreach (var style in styles)
            {
                if (style == null) continue;
                return style.Clone();
            }
            return null;
        }

        private static void ApplyBorderStyle(
            TableModels.BorderStyle? style,
            ref Color color,
            ref float width,
            Color? explicitColor,
            float? explicitWidth)
        {
            if (style == null) return;
            if (!explicitColor.HasValue)
                color = style.Color;
            if (!explicitWidth.HasValue && style.Width > 0)
                width = style.Width;
        }


        private static bool IsExplicitSide(Color? c, float? w)
            => c.HasValue || (w.HasValue && w.Value > 0f);
    }
}





















