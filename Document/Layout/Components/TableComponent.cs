using System;
using System.Collections.Generic;
using System.Linq;
using PdfBuilder.Document.Layout;
using PdfBuilder.Elements;
using PdfBuilder.Writer;

namespace PdfBuilder.Document.Layout.Components
{
    internal sealed class TableComponent : IMeasurable
    {
        private const float Epsilon = 0.1f;

        private readonly TableElement _workingTable;
        private readonly TableElement _rootTable;
        private readonly bool _isContinuation;

        public TableComponent(TableElement table)
            : this(table ?? throw new ArgumentNullException(nameof(table)), table, isContinuation: false)
        {
        }

        private TableComponent(TableElement workingTable, TableElement rootTable, bool isContinuation)
        {
            _workingTable = workingTable ?? throw new ArgumentNullException(nameof(workingTable));
            _rootTable = rootTable ?? throw new ArgumentNullException(nameof(rootTable));
            _isContinuation = isContinuation;
        }

        public LayoutMeasurement Measure(LayoutMeasureContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));

            var width = ResolveWidth(context.AvailableWidth);
            var metrics = TableMeasurementHelper.Measure(_workingTable, width);
            var rowHeights = metrics.RowHeights;

            float captionHeight = ComputeCaptionHeight(_workingTable);
            float totalBodyHeight = SumRows(rowHeights, 0, rowHeights.Length);
            float totalHeight = captionHeight + totalBodyHeight;

            if (totalHeight <= context.AvailableHeight + Epsilon)
            {
                var drawPieces = new List<TablePiece>
                {
                    new TablePiece(CreateSliceTable(_workingTable, 0, rowHeights.Length, width, includeCaption: true), totalHeight)
                };

                var metadata = new TableLayoutMetadata(drawPieces, width);
                return new LayoutMeasurement(
                    marginTop: 0f,
                    contentHeight: totalHeight,
                    marginBottom: 0f,
                    usedWidth: width,
                    metadata: metadata,
                    avoidBreakInside: _workingTable.AvoidBreakInside,
                    result: LayoutResultKind.Full,
                    remainder: null);
            }

            if (!_workingTable.EnablePageBreaks || _workingTable.AvoidBreakInside || context.AvailableHeight <= Epsilon)
            {
                return LayoutMeasurement.Wrap(width);
            }

            var headerInfo = ResolveHeaderInfo(width);
            float availableHeight = context.AvailableHeight;

            if (captionHeight > 0f)
            {
                if (captionHeight > availableHeight + Epsilon)
                    return LayoutMeasurement.Wrap(width);

                availableHeight -= captionHeight;
            }

            var pieces = new List<TablePiece>();
            bool captionAppliesToBody = captionHeight > 0f;

            if (headerInfo != null)
            {
                if (headerInfo.Value.Height > availableHeight + Epsilon)
                    return LayoutMeasurement.Wrap(width);

                availableHeight -= headerInfo.Value.Height;
                pieces.Add(headerInfo.Value.ToPiece());
            }

            if (rowHeights.Length == 0)
            {
                // Only caption/header to render this turn.
                if (pieces.Count == 0)
                    return LayoutMeasurement.Wrap(width);

                float totalUsedHeight = captionHeight + pieces.Sum(p => p.Height);
                var metadata = new TableLayoutMetadata(pieces, width);
                return new LayoutMeasurement(
                    marginTop: 0f,
                    contentHeight: totalUsedHeight,
                    marginBottom: 0f,
                    usedWidth: width,
                    metadata: metadata,
                    avoidBreakInside: _workingTable.AvoidBreakInside,
                    result: LayoutResultKind.Partial,
                    remainder: CreateRemainderComponent(0));
            }

            int lastRowIndex = FindFittingRowIndex(_workingTable, rowHeights, availableHeight);
            if (lastRowIndex < 0)
            {
                // Force first row so we always make progress.
                lastRowIndex = 0;
            }

            int rowsTaken = Math.Min(rowHeights.Length, lastRowIndex + 1);
            float bodyHeight = SumRows(rowHeights, 0, rowsTaken);

            bool includeCaption = captionAppliesToBody && pieces.Count == 0;
            var bodyTable = CreateSliceTable(_workingTable, 0, rowsTaken, width, includeCaption);
            float bodyPieceHeight = bodyHeight + (includeCaption ? captionHeight : 0f);
            pieces.Add(new TablePiece(bodyTable, bodyPieceHeight));

            float totalSliceHeight = pieces.Sum(p => p.Height);
            var metadataForSlice = new TableLayoutMetadata(pieces, width);

            TableComponent? remainder = null;
            if (rowsTaken < rowHeights.Length)
            {
                remainder = CreateRemainderComponent(rowsTaken);
            }

            var resultKind = remainder == null ? LayoutResultKind.Full : LayoutResultKind.Partial;
            return new LayoutMeasurement(
                marginTop: 0f,
                contentHeight: totalSliceHeight,
                marginBottom: 0f,
                usedWidth: width,
                metadata: metadataForSlice,
                avoidBreakInside: _workingTable.AvoidBreakInside,
                result: resultKind,
                remainder: remainder);
        }

        public void Draw(LayoutDrawContext context, LayoutMeasurement measurement)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (measurement == null) throw new ArgumentNullException(nameof(measurement));

            if (measurement.Metadata is not TableLayoutMetadata metadata)
                throw new InvalidOperationException("Table measurement metadata missing.");

            float cursorY = context.ContentTop;
            foreach (var piece in metadata.Pieces)
            {
                var element = piece.Element;
                element.EnablePageBreaks = false;
                element.X = context.ContentLeft;
                element.Y = cursorY;
                element.TableWidth ??= metadata.Width;
                element.PageTopY = context.Column.TopY;
                element.PageBottomY = context.Column.BottomY;
                context.Page.AddElement(element);

                cursorY -= piece.Height;
            }
        }

        private float ResolveWidth(float availableWidth)
        {
            if (_workingTable.TableWidth.HasValue && _workingTable.TableWidth.Value > 0f)
                return Math.Min(availableWidth, _workingTable.TableWidth.Value);

            return availableWidth;
        }

        private static float ComputeCaptionHeight(TableElement table)
        {
            if (table == null) throw new ArgumentNullException(nameof(table));
            if (string.IsNullOrWhiteSpace(table.CaptionText))
                return 0f;

            float capSize = Math.Max(table.DefaultFontSize, 11f);
            return capSize * PdfDefaults.LineHeightMultiplier + 4f;
        }

        private static float SumRows(float[] heights, int start, int count)
        {
            float sum = 0f;
            int end = Math.Min(heights.Length, start + count);
            for (int i = start; i < end; i++)
                sum += heights[i];
            return sum;
        }

        private HeaderInfo? ResolveHeaderInfo(float width)
        {
            if (!_isContinuation || !_workingTable.RepeatHeaders)
                return null;

            int headerCount = (_rootTable.HeaderRowCount ?? CountLeadingHeaders(_rootTable));
            if (headerCount <= 0)
                return null;

            var headerRows = _rootTable.Rows.Take(headerCount).ToList();
            if (headerRows.Count == 0)
                return null;

            var headerTable = LayoutSplitUtils.CloneTableWithRows(_rootTable, headerRows);
            headerTable.CaptionText = null;
            headerTable.EnablePageBreaks = false;
            headerTable.TableWidth = width;

            var metrics = TableMeasurementHelper.Measure(headerTable, width);
            float height = ComputeCaptionHeight(headerTable) + SumRows(metrics.RowHeights, 0, metrics.RowHeights.Length);
            return new HeaderInfo(headerTable, height);
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

        private int FindFittingRowIndex(TableElement table, float[] rowHeights, float budget)
        {
            if (rowHeights.Length == 0)
                return -1;

            var blockedBreak = ComputeBlockedBreaks(table);

            float running = 0f;
            int end = -1;
            for (int i = 0; i < rowHeights.Length; i++)
            {
                running += rowHeights[i];
                if (running <= budget + Epsilon || i == 0)
                {
                    end = i;
                }
                else
                {
                    break;
                }
            }

            if (end < 0)
                return -1;

            while (end < rowHeights.Length - 1 && blockedBreak[end])
            {
                end++;
            }

            return Math.Min(end, rowHeights.Length - 1);
        }

        private static bool[] ComputeBlockedBreaks(TableElement table)
        {
            int rowCount = table.Rows.Count;
            if (rowCount == 0) return Array.Empty<bool>();

            int totalCols = table.Rows.Max(r => r.Cells.Sum(c => Math.Max(1, c.ColSpan)));
            var covered = new HashSet<(int row, int col)>();
            var blocked = new bool[rowCount];

            for (int rowIndex = 0; rowIndex < rowCount; rowIndex++)
            {
                var row = table.Rows[rowIndex];
                int colIndex = 0;

                while (colIndex < totalCols && covered.Contains((rowIndex, colIndex))) colIndex++;

                foreach (var cell in row.Cells)
                {
                    while (colIndex < totalCols && covered.Contains((rowIndex, colIndex))) colIndex++;

                    int rowSpan = Math.Max(1, cell.RowSpan);
                    int colSpan = Math.Max(1, cell.ColSpan);

                    for (int r = rowIndex; r < Math.Min(rowCount - 1, rowIndex + rowSpan - 1); r++)
                        blocked[r] = true;

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

            return blocked;
        }

        private TableElement CreateSliceTable(TableElement source, int start, int count, float width, bool includeCaption)
        {
            var rows = source.Rows.Skip(start).Take(count).ToList();
            var slice = LayoutSplitUtils.CloneTableWithRows(source, rows);
            slice.EnablePageBreaks = false;
            slice.TableWidth = width;
            if (!includeCaption)
                slice.CaptionText = null;

            return slice;
        }

        private TableComponent? CreateRemainderComponent(int rowsConsumed)
        {
            var remainingRows = _workingTable.Rows.Skip(rowsConsumed).ToList();
            if (remainingRows.Count == 0)
                return null;

            var remainderTable = LayoutSplitUtils.CloneTableWithRows(_workingTable, remainingRows);
            remainderTable.CaptionText = null;
            remainderTable.EnablePageBreaks = false;
            remainderTable.RowBandOffset = _workingTable.RowBandOffset + rowsConsumed;
            remainderTable.TableWidth = _workingTable.TableWidth;

            return new TableComponent(remainderTable, _rootTable, isContinuation: true);
        }

        private readonly struct HeaderInfo
        {
            public HeaderInfo(TableElement table, float height)
            {
                Table = table;
                Height = height;
            }

            public TableElement Table { get; }
            public float Height { get; }

            public TablePiece ToPiece() => new TablePiece(Table, Height);
        }

        private sealed class TableLayoutMetadata
        {
            public TableLayoutMetadata(List<TablePiece> pieces, float width)
            {
                Pieces = pieces;
                Width = width;
            }

            public List<TablePiece> Pieces { get; }
            public float Width { get; }
        }

        private readonly struct TablePiece
        {
            public TablePiece(TableElement element, float height)
            {
                Element = element;
                Height = height;
            }

            public TableElement Element { get; }
            public float Height { get; }
        }
    }
}
