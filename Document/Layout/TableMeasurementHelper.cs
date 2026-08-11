using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using PdfBuilder.Document;
using PdfBuilder.Document.TextShaping;
using PdfBuilder.Elements;
using PdfBuilder.Models;
using PdfBuilder.TextShaping;
using PdfBuilder.Writer;
using TableModels = PdfBuilder.Elements.Table;

namespace PdfBuilder.Document.Layout
{
    internal static class TableMeasurementHelper
    {
        internal readonly struct TableMetrics
        {
            public TableMetrics(int totalColumns, float[] columnWidths, float[] rowHeights)
            {
                TotalColumns = totalColumns;
                ColumnWidths = columnWidths;
                RowHeights = rowHeights;
            }

            public int TotalColumns { get; }
            public float[] ColumnWidths { get; }
            public float[] RowHeights { get; }
        }
        public static float EstimateTableHeight(TableElement table, float availableWidth)
        {
            var metrics = Measure(table, availableWidth);
            if (metrics.RowHeights.Length == 0)
                return 0f;
            float total = 0f;
            foreach (var height in metrics.RowHeights)
                total += height;
            return total;
        }

        public static TableMetrics Measure(TableElement table, float availableWidth)
            => Measure(table, availableWidth, null, null);

        internal static TableMetrics Measure(TableElement table, float availableWidth, PdfPage? page, LayoutOptions? options)
        {
            if (table == null) throw new ArgumentNullException(nameof(table));

            if (table.Rows == null || table.Rows.Count == 0)
                return new TableMetrics(0, Array.Empty<float>(), Array.Empty<float>());

            int totalCols = table.Rows.Max(r => r.Cells.Sum(c => Math.Max(1, c.ColSpan)));
            if (totalCols <= 0)
                return new TableMetrics(0, Array.Empty<float>(), Array.Empty<float>());

            float tableWidth = table.TableWidth.HasValue && table.TableWidth.Value > 0f
                ? table.TableWidth.Value
                : availableWidth;

            tableWidth = Math.Max(0f, tableWidth);

            var colWidths = ResolveColumnWidths(table, totalCols, tableWidth);
            var rowHeights = ComputeRowHeights(table, colWidths, page, options);
            return new TableMetrics(totalCols, colWidths, rowHeights);
        }

        internal static float[] ResolveColumnWidths(TableElement table, int totalCols, float tableWidth)
        {
            return TableColumnWidthCalculator.Calculate(table, totalCols, tableWidth);
        }

        internal static float[] ComputeRowHeights(TableElement table, float[] colWidths, PdfPage? page = null, LayoutOptions? options = null)
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

                    if (colIndex + colSpan > totalCols)
                        colSpan = Math.Max(1, totalCols - colIndex);

                    float cellWidth = 0f;
                    for (int c = 0; c < colSpan; c++)
                        cellWidth += colWidths[colIndex + c];

                    float required = MeasureCellContentHeight(table, cell, cellWidth, page, options);

                    if (rowSpan == 1)
                    {
                        if (required > heights[rowIndex])
                            heights[rowIndex] = required;
                    }
                    else
                    {
                        int lastRow = Math.Min(rowCount - 1, rowIndex + rowSpan - 1);
                        float sum = 0f;
                        for (int r = rowIndex; r <= lastRow; r++)
                            sum += heights[r];

                        if (required > sum)
                        {
                            float deficit = required - sum;
                            float per = deficit / (lastRow - rowIndex + 1);
                            for (int r = rowIndex; r <= lastRow; r++)
                                heights[r] += per;
                        }
                    }

                    for (int r = 0; r < rowSpan; r++)
                    {
                        for (int c = 0; c < colSpan; c++)
                        {
                            if (!(r == 0 && c == 0))
                                covered.Add((rowIndex + r, colIndex + c));
                        }
                    }

                    colIndex += colSpan;
                }
            }

            for (int r = 0; r < rowCount; r++)
            {
                var explicitHeight = table.Rows[r].RowHeight;
                if (explicitHeight.HasValue && explicitHeight.Value > heights[r])
                    heights[r] = explicitHeight.Value;
            }

            return heights;
        }

        private static float MeasureCellContentHeight(TableElement table, TableCell cell, float cellWidth, PdfPage? page, LayoutOptions? options)
        {
            float uniform = cell.Padding ?? table.CellPadding;
            float padLeft = cell.PaddingLeft ?? uniform;
            float padRight = cell.PaddingRight ?? uniform;
            float padTop = cell.PaddingTop ?? uniform;
            float padBottom = cell.PaddingBottom ?? uniform;

            float usable = Math.Max(0f, cellWidth - padLeft - padRight);
            if (usable <= 0f)
            {
                cell.CachedLayout = null;
                cell.CachedLayoutWidth = usable;
                cell.CachedContentHeight = padTop + padBottom;
                return cell.CachedContentHeight;
            }

            if (cell.ContentFactory != null)
            {
                PdfPage measurePage = page ?? new PdfPage(Math.Max(1f, cellWidth), 1_000_000f);
                LayoutOptions measureOptions = options ?? measurePage.LayoutOptions;
                var column = new FlowColumn(0, 0f, usable, 0f, -1_000_000f);
                var context = new LayoutMeasureContext(measurePage, column, measureOptions);
                var component = cell.ContentFactory();
                LayoutMeasurement measurement = component.Measure(context);
                if (measurement.IsWrap || measurement.Remainder != null)
                {
                    throw new InvalidOperationException(
                        "Table cell container content could not be measured atomically. Keep rows atomic or use content that completes within one row.");
                }

                cell.MeasuredContent = component;
                cell.MeasuredContentLayout = measurement;
                cell.CachedLayout = null;
                cell.CachedLayoutWidth = usable;
                cell.CachedContentHeight = measurement.ReservedHeight + padTop + padBottom;
                return cell.CachedContentHeight;
            }

            var layout = EnsureCellLayout(table, cell, usable);

            float textHeight = layout.TotalHeight;
            if (cell.MaxLines.HasValue && cell.MaxLines.Value > 0)
            {
                int maxLines = Math.Max(1, cell.MaxLines.Value);
                float limitedHeight = 0f;
                for (int i = 0; i < Math.Min(maxLines, layout.Lines.Count); i++)
                    limitedHeight += layout.Lines[i].LineHeight;
                textHeight = limitedHeight;
            }
            else if (table.OverflowPolicy != CellOverflowPolicy.Wrap)
            {
                if (layout.Lines.Count > 0)
                    textHeight = layout.Lines[0].LineHeight;
            }

            cell.CachedContentHeight = textHeight + padTop + padBottom;
            return cell.CachedContentHeight;
        }

        private static RichTextLayoutResult EnsureCellLayout(TableElement table, TableCell cell, float usableWidth)
        {
            if (cell.CachedLayout != null && Math.Abs(cell.CachedLayoutWidth - usableWidth) < 0.1f)
                return cell.CachedLayout;

            var layout = LayoutCellContent(table, cell, usableWidth);
            cell.CachedLayout = layout;
            cell.CachedLayoutWidth = usableWidth;
            return layout;
        }

        private static RichTextLayoutResult LayoutCellContent(TableElement table, TableCell cell, float usableWidth)
        {
            var rich = new RichTextElement(0, 0)
            {
                FontFamily = ResolveFontFamily(table, cell),
                FontSize = ResolveFontSize(table, cell),
                LineHeight = ResolveLineHeight(table, cell),
                Alignment = MapAlignment(cell.HorizontalAlign),
                MaxWidth = usableWidth
            };

            if (cell.TextRuns != null && cell.TextRuns.Count > 0)
            {
                foreach (var inline in cell.TextRuns)
                    rich.Runs.Add(ConvertInlineRun(table, cell, inline));
            }
            else
            {
                rich.Runs.Add(CreateRunFromCell(table, cell));
            }

            return RichTextLayouter.Layout(rich, usableWidth);
        }

        private static RichRun CreateRunFromCell(TableElement table, TableCell cell)
        {
            var text = cell.Text ?? string.Empty;
            string fontFamily = ResolveFontFamily(table, cell);
            float fontSize = ResolveFontSize(table, cell);
            return new RichRun
            {
                Text = text,
                FontFamily = fontFamily,
                FontSize = fontSize,
                Bold = cell.Bold,
                Italic = cell.Italic,
                SmallCaps = cell.SmallCaps,
                Underline = cell.Underline,
                Strikethrough = cell.Strikethrough,
                Color = ToHex(cell.TextColor),
                FallbackFonts = cell.TextStyle?.FallbackFonts == null
                    ? null
                    : new List<string>(cell.TextStyle.FallbackFonts)
            };
        }

        private static RichRun ConvertInlineRun(TableElement table, TableCell cell, TableModels.InlineRun inline)
        {
            var style = inline.Style ?? new TableModels.TextStyle();
            string baseFontFamily = ResolveFontFamily(table, cell);
            float baseFontSize = ResolveFontSize(table, cell);

            return new RichRun
            {
                Text = inline.Text ?? string.Empty,
                FontFamily = !string.IsNullOrWhiteSpace(style.FontFamily) ? style.FontFamily : baseFontFamily,
                FontSize = style.FontSize > 0 ? style.FontSize : baseFontSize,
                Bold = style.Bold || cell.Bold,
                Italic = style.Italic || cell.Italic,
                SmallCaps = style.SmallCaps || cell.SmallCaps,
                Underline = style.Underline || cell.Underline,
                Strikethrough = style.Strikethrough || cell.Strikethrough,
                Color = ToHex(style.TextColor),
                FallbackFonts = inline.FallbackFonts ?? style.FallbackFonts ?? cell.TextStyle?.FallbackFonts
            };
        }

        private static string ResolveFontFamily(TableElement table, TableCell cell)
        {
            if (cell.TextStyle != null && !string.IsNullOrWhiteSpace(cell.TextStyle.FontFamily))
                return cell.TextStyle.FontFamily;
            if (!string.IsNullOrWhiteSpace(cell.Font))
                return cell.Font;
            if (!string.IsNullOrWhiteSpace(table.DefaultTextStyle.FontFamily))
                return table.DefaultTextStyle.FontFamily;
            return table.DefaultFont;
        }

        private static float ResolveFontSize(TableElement table, TableCell cell)
        {
            if (cell.TextStyle != null && cell.TextStyle.FontSize > 0)
                return cell.TextStyle.FontSize;
            if (cell.FontSize > 0)
                return cell.FontSize;
            if (table.DefaultTextStyle.FontSize > 0)
                return table.DefaultTextStyle.FontSize;
            return table.DefaultFontSize;
        }

        private static float ResolveLineHeight(TableElement table, TableCell cell)
        {
            if (cell.TextStyle != null && cell.TextStyle.LineHeight.HasValue)
                return cell.TextStyle.LineHeight.Value;
            if (cell.LineHeight.HasValue)
                return cell.LineHeight.Value;
            if (table.DefaultTextStyle.LineHeight.HasValue)
                return table.DefaultTextStyle.LineHeight.Value;
            return PdfDefaults.LineHeightMultiplier;
        }

        private static PdfBuilder.Models.TextAlignment MapAlignment(HorizontalAlign align) =>
            align switch
            {
                HorizontalAlign.Center => PdfBuilder.Models.TextAlignment.Center,
                HorizontalAlign.Right => PdfBuilder.Models.TextAlignment.Right,
                _ => PdfBuilder.Models.TextAlignment.Left
            };

        private static string ToHex(Color color) =>
            $"#{color.R:X2}{color.G:X2}{color.B:X2}";
    }
}










