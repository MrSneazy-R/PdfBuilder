using PdfBuilder.Elements;
using PdfBuilder.Models;
using PdfBuilder.Writer;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PdfBuilder.Document
{
    public class ColumnBuilder
    {
        // Current page and geometry (mutable so we can page/column-break)
        private PdfPage _page;
        private readonly float _defaultSpacing;
        private readonly float _margin;

        // Header/Footer reserved heights
        private readonly Func<PdfPage, HeaderFooterSpec?> _hfForPage; // optional
        private float _headerH;
        private float _footerH;

        // Flow columns
        private int _colIndex = 0;
        private FlowColumn[] _columns = Array.Empty<FlowColumn>();
        private FlowColumn CurrentColumn => _columns.Length == 0 ? throw new InvalidOperationException("Flow columns not initialized.") : _columns[_colIndex];

        // Optional factory for new pages
        private readonly Func<PdfPage> _newPage;

        public ColumnBuilder(
            PdfPage page,
            float margin,
            float defaultSpacing = 8f,
            Func<PdfPage> newPage = null,
            Func<PdfPage, HeaderFooterSpec?> hfForPage = null)
        {
            _page = page ?? throw new ArgumentNullException(nameof(page));
            _margin = margin;
            _defaultSpacing = defaultSpacing;
            _newPage = newPage;
            _hfForPage = hfForPage;

            ResolveHeaderFooterBands(_page, out _headerH, out _footerH);
            InitColumns(_page);
        }

        private void ResolveHeaderFooterBands(PdfPage page, out float headerH, out float footerH)
        {
            headerH = 0f; footerH = 0f;
            if (_hfForPage != null)
            {
                var hf = _hfForPage(page);
                if (hf != null)
                {
                    headerH = Math.Max(0f, hf.HeaderHeight);
                    footerH = Math.Max(0f, hf.FooterHeight);
                }
            }
        }

        private void InitColumns(PdfPage page)
        {
            var layout = page.Columns ?? new ColumnLayoutSpec { Columns = 1, Gutter = 14f };
            int columnCount = Math.Max(1, layout.Widths?.Length ?? layout.Columns);
            float gutter = layout.Gutter;
            float[]? widths = null;
            if (layout.Widths != null && layout.Widths.Length == columnCount)
                widths = (float[])layout.Widths.Clone();

            _columns = FlowGrid.Create(page, _margin, columnCount, gutter, _headerH, _footerH, widths);
            if (_columns.Length == 0)
            {
                float width = Math.Max(0, page.Width - (_margin > 0 ? _margin * 2 : page.MarginLeft + page.MarginRight));
                float top = page.Height - (_margin > 0 ? _margin : page.MarginTop) - _headerH;
                float bottom = (_margin > 0 ? _margin : page.MarginBottom) + _footerH;
                _columns = new[] { new FlowColumn(0, _margin > 0 ? _margin : page.MarginLeft, width, top, bottom) };
            }
            _colIndex = Math.Min(Math.Max(0, _colIndex), _columns.Length - 1);
            _columns[_colIndex].Reset();
        }

        // -- Navigation -------------------------------------------------------

        public float GetCurrentY() => CurrentColumn.Y;

        // Force a column break (NEW)
        public FlowColumn ActivateColumn(int index, bool reset = false)
        {
            if (index < 0 || index >= _columns.Length)
                throw new ArgumentOutOfRangeException(nameof(index));

            _colIndex = index;
            if (reset)
                _columns[_colIndex].Reset();

            return CurrentColumn;
        }

        public ColumnBuilder ColumnBreak()
        {
            if (_columns.Length == 0)
                return this;

            if (_colIndex < _columns.Length - 1)
            {
                _colIndex++;
                _columns[_colIndex].Reset();
            }
            else
            {
                PageBreak();
            }
            return this;
        }

        // Force a page break (resets to first column)
        public ColumnBuilder PageBreak()
        {
            if (_newPage == null) return this;
            _page = _newPage();
            ResolveHeaderFooterBands(_page, out _headerH, out _footerH);
            InitColumns(_page);

            _colIndex = 0;
            if (_columns.Length > 0)
                _columns[_colIndex].Reset();
            return this;
        }

        // Try next column before new page (NEW)
        private void NextColumnOrPage()
        {
            if (_columns.Length == 0)
                return;

            if (_colIndex < _columns.Length - 1)
            {
                _colIndex++;
                _columns[_colIndex].Reset();
            }
            else
            {
                PageBreak();
            }
        }

        private void EnsureSpace(float contentHeight, float marginTop, float marginBottom, bool avoidBreakInside = false)
        {
            float need = marginTop + Math.Max(0f, contentHeight) + marginBottom;
            int guard = 0;

       
            while (true)
            {
                var current = CurrentColumn;
                float topLimit = current.TopY;
                float bottomLimit = current.BottomY;
                float maxInColumn = current.Capacity;

                if (need > maxInColumn + 0.1f)
                {
                    if (_newPage != null && Math.Abs(current.Y - topLimit) > 0.5f)
                    {
                        NextColumnOrPage();
                        if (++guard > 16) break;
                        continue;
                    }
                    return;
                }

                if ((current.Y - need) >= bottomLimit)
                    return;

                if (_newPage == null)
                    return;

                if (!avoidBreakInside && Math.Abs(current.Y - topLimit) <= 0.5f)
                    return;

                NextColumnOrPage();
                if (++guard > 16)
                    break;
            }
        }


        public FlowColumn GetFlow() => CurrentColumn;
        public FlowColumn[] GetFlowColumns() => _columns;

        // -- Drawing helpers (kept) ------------------------------------------

        public ColumnBuilder Underline(float x, float? y, float width)
        {
            float useY = y ?? CurrentColumn.Y;
            _page.Elements.Add(new UnderlineElement(x, useY)
            {
                Width = width,
                Thickness = 1,
                Color = "#000000"
            });
            return this;
        }

        // Entry points (kept)
        public TextBuilder Text(string content) =>
            new TextBuilder(this, content, CurrentColumn.X, CurrentColumn.Y, CurrentColumn.Width);

        public ImageBuilder Image(byte[] data, float x, float y, float width, float height) =>
            new ImageBuilder(this, data, x, y, width, height);

        public TableBuilder Table(float x, float y, float width, float /*ignored*/ height) =>
            new TableBuilder(this, x, y, width);

        public ChartBuilder Chart(float x, float y, float width, float height) =>
            new ChartBuilder(this, x, y, width, height);

        // Row/Grid container (NEW)
        public RowBuilder Row(float gap = 12f) => new RowBuilder(this, gap, CurrentColumn.X, CurrentColumn.Y, CurrentColumn.Width);

        // -- Adders invoked by builders (kept + tiny changes) ----------------

        internal float AddText(TextElement text)
        {
            float marginTop = text.MarginTop ?? _defaultSpacing;
            float marginBottom = text.MarginBottom ?? 0f;
            float marginLeft = text.MarginLeft ?? 0f;
            float marginRight = text.MarginRight ?? 0f;

            float paddingTop = text.PaddingTop ?? 0f;
            float paddingBottom = text.PaddingBottom ?? 0f;
            float paddingLeft = text.PaddingLeft ?? 0f;
            float paddingRight = text.PaddingRight ?? 0f;

            var textContent = text.Text ?? string.Empty;

            float lineHeight = text.FontSize * text.LineHeight;

            while (true)
            {
                var column = CurrentColumn;
                int columnIndex = column.Index;

                float measureWidth = Math.Max(0f, column.Width - marginLeft - marginRight);
                float textMaxWidth = (text.MaxWidth ?? measureWidth) - paddingLeft - paddingRight;
                if (textMaxWidth < 0f) textMaxWidth = 0f;

                var lines = PdfLayoutUtils.WrapText(textContent, text.FontFamily, text.FontSize, textMaxWidth);
                int lineCount = Math.Max(1, lines.Count);

                float innerHeight = lineCount * lineHeight;
                float fullHeight = innerHeight + paddingTop + paddingBottom;

                float fullWidth = (lines.Any()
                    ? lines.Max(line => PdfLayoutUtils.EstimateTextWidth(line, text.FontFamily, text.FontSize, text.Monospace, text.Bold))
                    : 0f) + paddingLeft + paddingRight;

                float verticalSpan = text.Rotation != 0f
                    ? (float)(Math.Abs(fullHeight * Math.Cos(text.Rotation * Math.PI / 180.0)) + Math.Abs(fullWidth * Math.Sin(text.Rotation * Math.PI / 180.0)))
                    : fullHeight;

                float totalHeight = marginTop + verticalSpan + marginBottom;

                EnsureSpace(verticalSpan, marginTop, marginBottom, text.AvoidBreakInside);

                if (CurrentColumn.Index != columnIndex)
                    continue;

                column = CurrentColumn;
                float finalWidth = Math.Max(0f, column.Width - marginLeft - marginRight);
                float finalMaxWidth = (text.MaxWidth ?? finalWidth) - paddingLeft - paddingRight;
                if (finalMaxWidth < 0f) finalMaxWidth = 0f;

                if (Math.Abs(finalMaxWidth - textMaxWidth) > 0.01f)
                {
                    textMaxWidth = finalMaxWidth;
                    lines = PdfLayoutUtils.WrapText(textContent, text.FontFamily, text.FontSize, textMaxWidth);
                    lineCount = Math.Max(1, lines.Count);
                    innerHeight = lineCount * lineHeight;
                    fullHeight = innerHeight + paddingTop + paddingBottom;

                    fullWidth = (lines.Any()
                        ? lines.Max(line => PdfLayoutUtils.EstimateTextWidth(line, text.FontFamily, text.FontSize, text.Monospace, text.Bold))
                        : 0f) + paddingLeft + paddingRight;

                    verticalSpan = text.Rotation != 0f
                        ? (float)(Math.Abs(fullHeight * Math.Cos(text.Rotation * Math.PI / 180.0)) + Math.Abs(fullWidth * Math.Sin(text.Rotation * Math.PI / 180.0)))
                        : fullHeight;

                    totalHeight = marginTop + verticalSpan + marginBottom;

                    EnsureSpace(verticalSpan, marginTop, marginBottom, text.AvoidBreakInside);
                    if (CurrentColumn.Index != columnIndex)
                        continue;

                    column = CurrentColumn;
                }

                float contentTop = column.Y - marginTop;

                text.X = column.X + marginLeft + paddingLeft;
                text.Y = contentTop;
                text.MaxWidth = textMaxWidth;
                text.PaddingTop = paddingTop;
                text.PaddingBottom = paddingBottom;
                text.PaddingLeft = paddingLeft;
                text.PaddingRight = paddingRight;

                _page.AddElement(text);

                if (text.KeepWithNext)
                    EnsureSpace(lineHeight, 0, 0);

                return totalHeight;
            }
        }

        internal float AddImage(ImageElement image)
        {
            float marginTop = image.MarginTop ?? _defaultSpacing;
            float marginBottom = image.MarginBottom ?? 0f;
            float marginLeft = image.MarginLeft ?? 0f;
            float marginRight = image.MarginRight ?? 0f;

            float paddingTop = image.PaddingTop ?? 0f;
            float paddingBottom = image.PaddingBottom ?? 0f;
            float paddingLeft = image.PaddingLeft ?? 0f;
            float paddingRight = image.PaddingRight ?? 0f;

            float imageWidth = image.Width;
            float imageHeight = image.Height;

            if (image.MaxWidth.HasValue && imageWidth > image.MaxWidth.Value)
            {
                float scale = image.MaxWidth.Value / imageWidth;
                imageWidth = image.MaxWidth.Value;
                imageHeight *= scale;
            }
            if (image.MaxHeight.HasValue && imageHeight > image.MaxHeight.Value)
            {
                float scale = image.MaxHeight.Value / imageHeight;
                imageHeight = image.MaxHeight.Value;
                imageWidth *= scale;
            }

            float blockWidth = imageWidth + paddingLeft + paddingRight;
            float blockHeight = imageHeight + paddingTop + paddingBottom;

            double theta = image.Rotation * Math.PI / 180.0;
            float s = (float)Math.Sin(theta);
            float c = (float)Math.Cos(theta);

            float rotatedHeight = (image.Rotation != 0f)
                ? Math.Abs(blockHeight * c) + Math.Abs(blockWidth * s)
                : blockHeight;

            float extraShadowY = 0f;
            float shadowUp = 0f;
            if (!string.IsNullOrWhiteSpace(image.ShadowColor) &&
                (((image.ShadowOffsetX ?? 0) != 0) || ((image.ShadowOffsetY ?? 0) != 0)))
            {
                float sox = image.ShadowOffsetX ?? 0f;
                float soy = image.ShadowOffsetY ?? 0f;
                float yPrime = s * sox - c * soy;
                extraShadowY = Math.Abs(yPrime);
                shadowUp = Math.Max(0f, yPrime);
            }

            float halfW = blockWidth * 0.5f;
            float halfH = blockHeight * 0.5f;
            float rotatedHalfH = Math.Abs(halfH * c) + Math.Abs(halfW * s);
            float overhangTop = Math.Max(0f, rotatedHalfH - halfH);

            float verticalSpan = rotatedHeight + extraShadowY;

            float contentHeight = overhangTop + shadowUp + verticalSpan;
            float totalHeight = marginTop + contentHeight + marginBottom;

            EnsureSpace(contentHeight, marginTop, marginBottom, image.AvoidBreakInside);

            var column = CurrentColumn;
            float contentTop = column.Y - marginTop;

            image.X = column.X + marginLeft + paddingLeft;
            image.Y = contentTop - overhangTop - shadowUp;

            image.Width = imageWidth;
            image.Height = imageHeight;
            image.PaddingTop = paddingTop;
            image.PaddingBottom = paddingBottom;
            image.PaddingLeft = paddingLeft;
            image.PaddingRight = paddingRight;

            _page.AddElement(image);

            if (image.KeepWithNext)
            {
                float reserve = Math.Max(8f, image.Height * 0.2f);
                EnsureSpace(reserve, 0, 0);
            }

            return totalHeight;
        }

        internal float AddTable(TableElement table)
        {
            const float marginTop = 8f;
            const float marginBottom = 0f;

            float originalX = table.X;
            float originalY = table.Y;
            float? originalWidth = table.TableWidth;
            bool widthProvided = originalWidth.HasValue && originalWidth.Value > 0f;

            // If avoid-break-inside, treat table atomically; otherwise let TablePaginator split it later
            while (true)
            {
                var column = CurrentColumn;
                int columnIndex = column.Index;

                if (!widthProvided)
                    table.TableWidth = column.Width;

                float blockHeight = EstimateTableHeight(table);
                float totalHeight = marginTop + blockHeight + marginBottom;

                EnsureSpace(blockHeight, marginTop, marginBottom, table.AvoidBreakInside);

                if (CurrentColumn.Index != columnIndex)
                {
                    table.X = originalX;
                    table.Y = originalY;
                    if (!widthProvided)
                        table.TableWidth = originalWidth;
                    continue;
                }

                column = CurrentColumn;
                if (!widthProvided)
                    table.TableWidth = column.Width;

                if (table.X == 0f) table.X = column.X;
                if (table.Y == 0f) table.Y = column.Y - marginTop;

                _page.AddElement(table);

                if (table.KeepWithNext)
                    EnsureSpace(12f, 0, 0);

                return totalHeight;
            }
        }

        internal float AddChart(ChartElement chart)
        {
            const float marginTop = 8f;
            const float marginBottom = 0f;

            float titleSpace = !string.IsNullOrWhiteSpace(chart.Title) ? chart.TitleSize * 1.2f : 0f;
            float bodyHeight = chart.Height > 0 ? chart.Height : 220f;
            float legendSpace = (chart.ShowLegend && chart.LegendPosition == ChartElement.LegendPos.Below)
                ? (14f * Math.Max(1, chart.Series.Count) + 6f)
                : 0f;

            float blockHeight = titleSpace + bodyHeight + legendSpace;
            float totalHeight = marginTop + blockHeight + marginBottom;

            EnsureSpace(blockHeight, marginTop, marginBottom, avoidBreakInside: true);

            var column = CurrentColumn;
            float contentTop = column.Y - marginTop;

            if (chart.X == 0f) chart.X = column.X;
            if (chart.Y == 0f) chart.Y = contentTop;

            if (chart.Width <= 0f) chart.Width = column.Width;
            if (chart.Height <= 0f) chart.Height = 220f;

            _page.AddElement(chart);

            return totalHeight;
        }

        // ===== Row/Grid (NEW, simple row with %/fr/px cols and gap) =====
        public sealed class RowBuilder
        {
            private readonly ColumnBuilder _col;
            private readonly float _gap;
            private readonly float _baseX, _baseY, _maxW;

            private readonly List<(Unit u, float v, Action<float /*x*/, float /*y*/, float /*w*/> draw)> _cells = new();

            public enum Unit { Px, Percent, Fr }

            internal RowBuilder(ColumnBuilder col, float gap, float x, float y, float w)
            { _col = col; _gap = gap; _baseX = x; _baseY = y; _maxW = w; }

            public RowBuilder ColPx(float px, Action<float, float, float> draw) { _cells.Add((Unit.Px, px, draw)); return this; }
            public RowBuilder ColPercent(float pct, Action<float, float, float> draw) { _cells.Add((Unit.Percent, pct, draw)); return this; }
            public RowBuilder ColFr(float fr, Action<float, float, float> draw) { _cells.Add((Unit.Fr, fr, draw)); return this; }

            public ColumnBuilder Add(float estimatedRowHeight = 24f)
            {
                // Ensure space in current column
                _col.EnsureSpace(estimatedRowHeight, _col._defaultSpacing, 0f);
                var column = _col.CurrentColumn;
                column.Advance(_col._defaultSpacing);

                // Compute column widths
                float fixedSum = 0f, frSum = 0f, pctSum = 0f;
                foreach (var c in _cells)
                {
                    if (c.u == Unit.Px) fixedSum += c.v;
                    else if (c.u == Unit.Percent) pctSum += c.v;
                    else frSum += c.v;
                }

                float gaps = Math.Max(0, _cells.Count - 1) * _gap;
                float remain = Math.Max(0, _maxW - gaps - fixedSum - (_maxW * (pctSum / 100f)));
                float frUnit = frSum > 0 ? remain / frSum : 0f;

                float x = _baseX;
                foreach (var c in _cells)
                {
                    float w = c.u switch
                    {
                        Unit.Px => c.v,
                        Unit.Percent => _maxW * (c.v / 100f),
                        Unit.Fr => frUnit * c.v,
                        _ => 0f
                    };

                    c.draw(x, column.Y, w);
                    x += w + _gap;
                }

                column.Advance(estimatedRowHeight);
                return _col;
            }
        }

        // === existing helpers for table height etc. (kept) ===
        private float EstimateTableHeight(TableElement table)
        { /* � same as your version � */
            if (table.Rows == null || table.Rows.Count == 0) return 0f;
            int totalCols = table.Rows.Max(r => r.Cells.Sum(c => Math.Max(1, c.ColSpan)));
            float tableWidth = table.TableWidth.GetValueOrDefault(CurrentColumn.Width);
            var colWidths = new float[totalCols];
            if (table.ColumnWidths != null && table.ColumnWidths.Count == totalCols)
                for (int i = 0; i < totalCols; i++) colWidths[i] = table.ColumnWidths[i];
            else
            {
                float equal = tableWidth / Math.Max(1, totalCols);
                for (int i = 0; i < totalCols; i++) colWidths[i] = equal;
            }
            var rowHeights = ComputeRowHeights(table, colWidths);
            float total = 0f; for (int r = 0; r < rowHeights.Length; r++) total += rowHeights[r];
            return total;
        }

        private float[] ComputeRowHeights(TableElement table, float[] colWidths)
        { /* � same as your version � */
            int totalCols = colWidths.Length;
            int rowCount = table.Rows.Count;
            var heights = new float[rowCount];
            for (int r = 0; r < rowCount; r++)
                heights[r] = table.Rows[r].RowHeight
                    ?? (table.DefaultFontSize * PdfDefaults.LineHeightMultiplier + table.CellPadding * 2);
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
                    if (colIndex + colSpan > totalCols) colSpan = Math.Max(1, totalCols - colIndex);
                    float cw = 0f; for (int c = 0; c < colSpan; c++) cw += colWidths[colIndex + c];
                    float required = MeasureCellContentHeight(table, cell, cw);
                    if (rowSpan == 1) { if (required > heights[rowIndex]) heights[rowIndex] = required; }
                    else
                    {
                        int lastRow = Math.Min(rowCount - 1, rowIndex + rowSpan - 1);
                        float sum = 0f; for (int r = rowIndex; r <= lastRow; r++) sum += heights[r];
                        if (required > sum)
                        {
                            float deficit = required - sum;
                            float per = deficit / (lastRow - rowIndex + 1);
                            for (int r = rowIndex; r <= lastRow; r++) heights[r] += per;
                        }
                    }
                    for (int r = 0; r < rowSpan; r++)
                        for (int c = 0; c < colSpan; c++)
                            if (!(r == 0 && c == 0)) covered.Add((rowIndex + r, colIndex + c));
                    colIndex += colSpan;
                }
            }
            for (int r = 0; r < rowCount; r++)
            {
                var explicitH = table.Rows[r].RowHeight;
                if (explicitH.HasValue && explicitH.Value > heights[r]) heights[r] = explicitH.Value;
            }
            return heights;
        }
        // -- Anchors / Lists / RichText integration --------------------------

        // Optional convenience: start an anchor at the current flow position
        public AnchorBuilder Anchor(string id) => new AnchorBuilder(this, id, CurrentColumn.X, CurrentColumn.Y);

        // Called by AnchorBuilder.Add()
        internal void AddAnchor(AnchorElement a)
        {
            // If caller didn�t set coordinates, place at current flow position
            if (a.X == 0f) a.X = CurrentColumn.X;
            if (a.Y == 0f) a.Y = CurrentColumn.Y;

            _page.AddElement(a);
            // Anchors have no visual height � don�t move the flow cursor.
        }

        // Called by ListBuilder.Add()
        internal float AddList(ListElement list)
        {
            // very light Flow integration; list renderer will handle exact height.
            const float marginTop = 8f;
            const float marginBottom = 0f;
            const float conservativeRow = 20f; // small preflight to reduce overlaps

            float totalHeight = marginTop + conservativeRow + marginBottom;
            EnsureSpace(conservativeRow, marginTop, marginBottom, avoidBreakInside: true);

            var column = CurrentColumn;
            float contentTop = column.Y - marginTop;

            if (list.X == 0f) list.X = column.X;
            if (list.Y == 0f) list.Y = contentTop;
            if (list.MaxWidth <= 0f) list.MaxWidth = column.Width;   // if your ListElement supports Width

            _page.AddElement(list);

            // Advance a small amount; exact stacking is refined by renderer/paginator
            return totalHeight;
        }

        // Called by RichTextBuilder.Add()
        internal float AddRichText(RichTextElement rt)
        {
            const float marginTop = 8f;
            const float marginBottom = 0f;
            const float conservativeBlock = 22f;

            float totalHeight = marginTop + conservativeBlock + marginBottom;
            EnsureSpace(conservativeBlock, marginTop, marginBottom, avoidBreakInside: true);

            var column = CurrentColumn;
            float contentTop = column.Y - marginTop;

            if (rt.X == 0f) rt.X = column.X;
            if (rt.Y == 0f) rt.Y = contentTop;
            if (rt.MaxWidth <= 0f) rt.MaxWidth = column.Width; // if your type exposes MaxWidth

            _page.AddElement(rt);

            return totalHeight;
        }

        private float MeasureCellContentHeight(TableElement table, TableCell cell, float cellWidth)
        {
            float pad = cell.Padding ?? table.CellPadding;
            string font = string.IsNullOrWhiteSpace(cell.Font) ? table.DefaultFont : cell.Font;
            float size = cell.FontSize > 0 ? cell.FontSize : table.DefaultFontSize;
            float usable = Math.Max(0, cellWidth - pad * 2);
            if (table.OverflowPolicy == CellOverflowPolicy.Wrap)
            {
                var lines = PdfLayoutUtils.WrapText(cell.Text ?? string.Empty, font, size, usable);
                float lineH = size * PdfDefaults.LineHeightMultiplier;
                return Math.Max(lineH, lines.Count * lineH) + pad * 2;
            }
            else
            {
                float lineH = size * PdfDefaults.LineHeightMultiplier;
                return lineH + pad * 2;
            }
        }
    }
}



