using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using PdfBuilder.Document;
using PdfBuilder.Elements;
using PdfBuilder.Encoder;
using PdfBuilder.Models;

namespace PdfBuilder.Writer
{
    public class PdfContentRenderer
    {
        private readonly PdfStreamWriter _writer;

        public PdfContentRenderer(PdfStreamWriter writer)
        {
            _writer = writer;
        }

        /// <summary>
        /// Render a text element with wrapping.
        /// </summary>
        public void RenderText(TextElement element, float startX, float startY)
        {
            float maxWidth = element.MaxWidth ?? 99999f;
            var wrapped = PdfLayoutUtils.WrapText(element.Text ?? string.Empty,
                                                  element.FontFamily,
                                                  element.FontSize,
                                                  maxWidth);

            float lineHeight = element.FontSize * (element.LineHeight <= 0 ? 1.2f : element.LineHeight);
            float y = startY;

            foreach (var line in wrapped)
            {
                // NOTE: you likely want to map FontFamily -> /F1 in resources; this is a simplified placeholder
                _writer.WriteLine($"BT /F1 {element.FontSize} Tf {startX} {y} Td ({PdfEnc.WinAnsiHex(line)}) Tj ET");
                y -= lineHeight;
            }
        }
        public void DrawText(string text, float x, float y, string fontFamily, float fontSize, Color color)
        {
            string rgb = $"{color.R / 255f:F3} {color.G / 255f:F3} {color.B / 255f:F3}";
            _writer.WriteLine($"{rgb} rg");
            // NOTE: mapping fontFamily -> /F1 is stubbed; ensure resources map your fonts.
            _writer.WriteLine($"BT /F1 {fontSize} Tf {x} {y} Td {PdfEnc.WinAnsiHex(text)} Tj ET");
        }
        /// <summary>
        /// Render an image element (simplified; assumes /Im{n} already in resources and element.Width/Height set).
        /// </summary>
        public void RenderImage(ImageElement element, float x, float y)
        {
            // You will need to ensure the correct XObject name is used here; this is a placeholder.
            _writer.WriteLine($"q {element.Width} 0 0 {element.Height} {x} {y} cm /Im1 Do Q");
        }

        /// <summary>
        /// Render a table with auto/fixed column widths, alt row backgrounds, borders and alignment.
        /// </summary>
        public void RenderTable(TableElement table, float startX, float startY)
        {
            if (table.Rows.Count == 0) return;

            // 1) Columns
            int totalCols = table.Rows.Max(r => r.Cells.Sum(c => Math.Max(1, c.ColSpan)));
            float tableWidth = table.TableWidth ?? 500f;
            var colWidths = new float[totalCols];

            if (table.ColumnWidths != null && table.ColumnWidths.Count == totalCols)
            {
                colWidths = table.ColumnWidths.ToArray();
            }
            else
            {
                float equal = tableWidth / totalCols;
                for (int i = 0; i < totalCols; i++) colWidths[i] = equal;
            }

            // 2) Rows
            float currentY = startY;
            bool isAltRow = false;

            foreach (var row in table.Rows)
            {
                float rowHeight = row.RowHeight ?? GetAutoRowHeight(row, colWidths, table);
                float currentX = startX;
                int colIndex = 0;

                foreach (var cell in row.Cells)
                {
                    int span = Math.Max(1, cell.ColSpan);
                    float cellWidth = 0f;
                    for (int s = 0; s < span; s++) cellWidth += colWidths[colIndex + s];

                    // Background color order: cell > row > header > alt
                    Color? bg = cell.BackgroundColor
                                ?? row.BackgroundColor
                                ?? (row.IsHeader ? table.HeaderBackground
                                                 : isAltRow ? table.AltRowBackground : null);

                    if (bg.HasValue)
                        FillRect(currentX, currentY - rowHeight, cellWidth, rowHeight, bg.Value);

                    // Borders
                    if (cell.BorderTop) StrokeLine(currentX, currentY, currentX + cellWidth, currentY, cell.BorderColor, cell.BorderWidth);
                    if (cell.BorderBottom) StrokeLine(currentX, currentY - rowHeight, currentX + cellWidth, currentY - rowHeight, cell.BorderColor, cell.BorderWidth);
                    if (cell.BorderLeft) StrokeLine(currentX, currentY, currentX, currentY - rowHeight, cell.BorderColor, cell.BorderWidth);
                    if (cell.BorderRight) StrokeLine(currentX + cellWidth, currentY, currentX + cellWidth, currentY - rowHeight, cell.BorderColor, cell.BorderWidth);

                    // Text
                    if (!string.IsNullOrEmpty(cell.Text))
                    {
                        float padding = cell.Padding ?? table.CellPadding;

                        // wrap inside cell
                        var lines = PdfLayoutUtils.WrapText(cell.Text,
                                                            string.IsNullOrWhiteSpace(cell.Font) ? table.DefaultFont : cell.Font,
                                                            cell.FontSize <= 0 ? table.DefaultFontSize : cell.FontSize,
                                                            Math.Max(0, cellWidth - padding * 2));

                        float lineH = cell.FontSize * PdfDefaults.LineHeightMultiplier;
                        float blockH = lines.Count * lineH;

                        float textStartY = PdfLayoutUtils.GetVerticalAlignedY(cell.VerticalAlign,
                                                                              topY: currentY,
                                                                              cellHeight: rowHeight,
                                                                              textHeight: blockH,
                                                                              padding: padding);

                        float textX = PdfLayoutUtils.GetHorizontalAlignedX(
                                                                            cell.HorizontalAlign,
                                                                            startX: currentX,
                                                                            cellWidth: cellWidth,
                                                                            lines: lines,
                                                                            fontFamily: string.IsNullOrWhiteSpace(cell.Font) ? table.DefaultFont : cell.Font,
                                                                            fontSize: cell.FontSize <= 0 ? table.DefaultFontSize : cell.FontSize,
                                                                            padding: padding
                                                                            );


                        float y = textStartY;
                        foreach (var line in lines)
                        {
                            _writer.WriteLine($"BT /F1 {cell.FontSize} Tf {textX} {y - cell.FontSize} Td ({PdfEnc.WinAnsiHex(line)}) Tj ET");
                            y -= lineH;
                        }
                    }

                    currentX += cellWidth;
                    colIndex += span;
                }

                currentY -= rowHeight;
                isAltRow = !isAltRow;
            }
        }

        // ---- Helpers ----

        private void FillRect(float x, float y, float w, float h, Color color)
        {
            string rgb = $"{color.R / 255f:F3} {color.G / 255f:F3} {color.B / 255f:F3} rg";
            _writer.WriteLine($"{rgb} {x:F2} {y:F2} {w:F2} {h:F2} re f");
        }

        private void StrokeLine(float x1, float y1, float x2, float y2, Color color, float width)
        {
            string rgb = $"{color.R / 255f:F3} {color.G / 255f:F3} {color.B / 255f:F3} RG";
            _writer.WriteLine($"{rgb} {width:F2} w {x1:F2} {y1:F2} m {x2:F2} {y2:F2} l S");
        }

        private float GetAutoRowHeight(TableRow row, float[] colWidths, TableElement table)
        {
            float max = 0f;
            int colIndex = 0;

            foreach (var cell in row.Cells)
            {
                int span = Math.Max(1, cell.ColSpan);
                float cw = 0f;
                for (int s = 0; s < span; s++) cw += colWidths[colIndex + s];

                float padding = cell.Padding ?? table.CellPadding;
                var font = string.IsNullOrWhiteSpace(cell.Font) ? table.DefaultFont : cell.Font;
                float size = cell.FontSize <= 0 ? table.DefaultFontSize : cell.FontSize;

                var lines = PdfLayoutUtils.WrapText(cell.Text ?? "", font, size, Math.Max(0, cw - padding * 2));
                float lineH = size * PdfDefaults.LineHeightMultiplier;
                float total = lines.Count * lineH + padding * 2;

                if (total > max) max = total;
                colIndex += span;
            }
            return max <= 0 ? (table.DefaultFontSize * PdfDefaults.LineHeightMultiplier + table.CellPadding * 2) : max;
        }
    }
}
