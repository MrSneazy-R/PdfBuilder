using System;
using System.Collections.Generic;
using System.Linq;
using PdfBuilder.Document;
using PdfBuilder.Elements;
using PdfBuilder.Elements.Table;
using PdfBuilder.Models;

namespace PdfBuilder.Document.Layout
{
    internal static class TableColumnWidthCalculator
    {
        private sealed class ColumnSpec
        {
            public bool IsFixed;
            public float FixedWidth;
            public float Weight = 1f;
            public float? Min;
            public float? Max;
        }

        public static float[] Calculate(TableElement table, int totalCols, float tableWidth)
        {
            if (totalCols <= 0)
                return Array.Empty<float>();

            var specs = BuildSpecs(table, totalCols);

            var fixedW = new float[totalCols];
            var isFixed = new bool[totalCols];

            for (int i = 0; i < totalCols; i++)
            {
                if (!specs[i].IsFixed)
                    continue;

                isFixed[i] = true;
                fixedW[i] = Math.Max(0f, specs[i].FixedWidth);
            }

            float fixedSum = fixedW.Sum();
            if (tableWidth <= 0f)
            {
                float defaultWidth = totalCols * 40f;
                tableWidth = Math.Max(defaultWidth + fixedSum, 1f);
            }

            float avail = Math.Max(1f, tableWidth - fixedSum);
            var minW = Enumerable.Repeat(40f, totalCols).ToArray();
            var wantW = Enumerable.Repeat(40f, totalCols).ToArray();

            float padDefault = Math.Max(0f, table.CellPadding);

            void Consider(int col, TableCell c, string text)
            {
                if (isFixed[col]) return;

                float padL = c.PaddingLeft ?? c.Padding ?? padDefault;
                float padR = c.PaddingRight ?? c.Padding ?? padDefault;
                string font = string.IsNullOrWhiteSpace(c.Font) ? table.DefaultFont : c.Font;
                float size = c.FontSize > 0 ? c.FontSize : table.DefaultFontSize;

                float head = PdfLayoutUtils.EstimateTextWidth(text ?? string.Empty, font, size) + padL + padR;
                if (head > minW[col]) minW[col] = head;

                bool numericish = c.HorizontalAlign == HorizontalAlign.Right || IsNumericLike(c.Text);
                float want;
                if (numericish)
                {
                    want = Math.Clamp(head, 40f, 90f);
                }
                else
                {
                    string longestWord = LongestWord(c.Text);
                    float lw = PdfLayoutUtils.EstimateTextWidth(longestWord, font, size) + padL + padR;
                    float cap = tableWidth * 0.55f;
                    want = Math.Min(Math.Max(minW[col], Math.Max(head, lw)), cap);
                }
                if (want > wantW[col]) wantW[col] = want;
            }

            int headerCount = CountLeadingHeaders(table);
            for (int r = 0; r < Math.Min(headerCount, table.Rows.Count); r++)
            {
                int cpos = 0;
                foreach (var cell in table.Rows[r].Cells)
                {
                    int span = Math.Max(1, cell.ColSpan);
                    if (span == 1) Consider(cpos, cell, cell.Text ?? string.Empty);
                    cpos += span;
                }
            }

            for (int r = headerCount; r < table.Rows.Count; r++)
            {
                int cpos = 0;
                foreach (var cell in table.Rows[r].Cells)
                {
                    int span = Math.Max(1, cell.ColSpan);
                    if (span == 1) Consider(cpos, cell, cell.Text ?? string.Empty);
                    cpos += span;
                }
            }

            var autoIndices = new List<int>();
            for (int i = 0; i < totalCols; i++)
            {
                if (isFixed[i]) continue;

                minW[i] = Math.Min(minW[i], avail);
                if (specs[i].Min.HasValue)
                    minW[i] = Math.Max(minW[i], Math.Max(0f, specs[i].Min!.Value));

                wantW[i] = Math.Max(minW[i], wantW[i]);
                if (specs[i].Max.HasValue)
                    wantW[i] = Math.Min(Math.Max(minW[i], specs[i].Max!.Value), wantW[i]);

                autoIndices.Add(i);
            }

            var result = fixedW.ToArray();
            if (autoIndices.Count == 0)
                return NudgeToWidth(result, tableWidth);

            float sumMin = autoIndices.Sum(i => minW[i]);
            float sumWant = autoIndices.Sum(i => wantW[i]);

            float totalWeight = autoIndices.Sum(i => Math.Max(0.0001f, specs[i].Weight));
            if (totalWeight <= 0f) totalWeight = autoIndices.Count;
            float slack = Math.Max(0f, avail - sumMin);
            if (slack > 0f)
            {
                foreach (int i in autoIndices)
                {
                    float share = slack * (Math.Max(0.0001f, specs[i].Weight) / totalWeight);
                    float target = minW[i] + share;
                    if (specs[i].Max.HasValue)
                        target = Math.Min(target, specs[i].Max!.Value);
                    wantW[i] = Math.Max(wantW[i], target);
                }

                sumWant = autoIndices.Sum(i => wantW[i]);
            }

            if (sumMin > avail + 0.01f)
            {
                float scale = avail / Math.Max(sumMin, 1e-3f);
                foreach (int i in autoIndices)
                    result[i] = Math.Max(24f, minW[i] * scale);
            }
            else if (sumWant <= avail + 0.01f)
            {
                foreach (int i in autoIndices)
                    result[i] = wantW[i];
            }
            else
            {
                float extra = avail - sumMin;
                float denom = Math.Max(1e-6f, sumWant - sumMin);
                foreach (int i in autoIndices)
                    result[i] = minW[i] + extra * ((wantW[i] - minW[i]) / denom);
            }

            for (int i = 0; i < totalCols; i++)
            {
                if (isFixed[i]) continue;
                if (specs[i].Max.HasValue)
                    result[i] = Math.Min(result[i], specs[i].Max!.Value);
            }

            return NudgeToWidth(result, tableWidth);
        }

        private static ColumnSpec[] BuildSpecs(TableElement table, int totalCols)
        {
            var specs = new ColumnSpec[totalCols];
            for (int i = 0; i < totalCols; i++)
                specs[i] = new ColumnSpec();

            if (table.ColumnDefinitions != null && table.ColumnDefinitions.Count == totalCols)
            {
                for (int i = 0; i < totalCols; i++)
                {
                    var def = table.ColumnDefinitions[i] ?? new TableColumnDefinition();
                    specs[i].Min = def.MinWidth;
                    specs[i].Max = def.MaxWidth;

                    switch (def.Mode)
                    {
                        case TableColumnWidthMode.Fixed:
                            specs[i].IsFixed = true;
                            specs[i].FixedWidth = Math.Max(0f, def.Value);
                            if (def.MinWidth.HasValue)
                                specs[i].FixedWidth = Math.Max(specs[i].FixedWidth, def.MinWidth.Value);
                            if (def.MaxWidth.HasValue)
                                specs[i].FixedWidth = Math.Min(specs[i].FixedWidth, def.MaxWidth.Value);
                            break;
                        case TableColumnWidthMode.Relative:
                            specs[i].Weight = Math.Max(0.0001f, def.Value);
                            break;
                        default:
                            specs[i].Weight = 1f;
                            break;
                    }
                }
            }
            else if (table.ColumnWidths != null && table.ColumnWidths.Count > 0)
            {
                int n = Math.Min(totalCols, table.ColumnWidths.Count);
                for (int i = 0; i < n; i++)
                {
                    if (table.ColumnWidths[i] > 0f)
                    {
                        specs[i].IsFixed = true;
                        specs[i].FixedWidth = table.ColumnWidths[i];
                    }
                }
            }

            for (int i = 0; i < totalCols; i++)
            {
                if (!specs[i].IsFixed && specs[i].Weight <= 0f)
                    specs[i].Weight = 1f;
            }

            return specs;
        }

        private static float[] NudgeToWidth(float[] widths, float targetWidth)
        {
            float diff = targetWidth - widths.Sum();
            if (Math.Abs(diff) <= 0.01f)
                return widths;

            if (widths.Length == 0)
                return widths;

            int lastIndex = widths.Length - 1;
            widths[lastIndex] += diff;
            if (widths[lastIndex] < 0f)
                widths[lastIndex] = 0f;

            return widths;
        }

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
            if (string.IsNullOrWhiteSpace(s)) return string.Empty;
            string[] parts = s.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            string longest = string.Empty;
            foreach (var p in parts) if (p.Length > longest.Length) longest = p;
            return longest;
        }
    }
}

