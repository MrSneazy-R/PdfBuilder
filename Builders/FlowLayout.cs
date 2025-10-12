using System;
using PdfBuilder.Models;

namespace PdfBuilder.Document
{
    /// <summary>
    /// Represents a reserved rectangle inside a flow column.
    /// Top and Bottom use PDF coordinates (origin bottom-left).
    /// </summary>
    public readonly record struct FlowRect(float X, float Top, float Width, float Height)
    {
        public float Bottom => Top - Height;
    }

    /// <summary>
    /// Manages a single flow column cursor. Coordinates are expressed in PDF space
    /// (origin at the bottom-left). <see cref="Y"/> reflects the next available
    /// baseline from the top of the column moving downwards.
    /// </summary>
    public sealed class FlowColumn
    {
        private readonly float _topY;
        private readonly float _bottomY;
        private float _cursorY;

        internal FlowColumn(int index, float x, float width, float topY, float bottomY)
        {
            Index = index;
            X = x;
            Width = width;
            _topY = topY;
            _bottomY = bottomY;
            _cursorY = topY;
        }

        public int Index { get; }
        public float X { get; }
        public float Width { get; }
        public float Y => _cursorY;
        public float BottomY => _bottomY;
        public float TopY => _topY;
        public float Available => _cursorY - _bottomY;
        public float Capacity => _topY - _bottomY;

        public bool CanFit(float height, float epsilon = 0.1f)
        {
            if (height <= 0f) return true;
            return (_cursorY - height) >= (_bottomY - epsilon);
        }

        public FlowRect Reserve(float height)
        {
            float clamped = Math.Max(0f, height);
            if (!CanFit(clamped))
                throw new FlowOverflowException(this, clamped);

            float top = _cursorY;
            _cursorY -= clamped;
            return new FlowRect(X, top, Width, clamped);
        }

        public FlowRect ForceReserve(float height)
        {
            float clamped = Math.Max(0f, height);
            float top = _cursorY;
            _cursorY = Math.Max(_bottomY, _cursorY - clamped);
            return new FlowRect(X, top, Width, clamped);
        }

        public void Advance(float pixels)
        {
            _cursorY -= pixels;
        }

        public FlowColumn SwitchTo(FlowColumn nextColumn)
        {
            if (nextColumn == null) throw new ArgumentNullException(nameof(nextColumn));
            nextColumn.Reset();
            return nextColumn;
        }

        internal void Reset() => _cursorY = _topY;
    }

    public sealed class FlowOverflowException : Exception
    {
        public FlowOverflowException(FlowColumn column, float requestedHeight)
            : base($"Flow column {column.Index} cannot fit {requestedHeight}pt (available {column.Available}pt).")
        {
            Column = column;
            RequestedHeight = requestedHeight;
        }

        public FlowColumn Column { get; }
        public float RequestedHeight { get; }
    }

    public static class FlowGrid
    {
        public static FlowColumn[] Create(PdfPage page, float margin, float headerHeight, float footerHeight)
        {
            if (page == null) throw new ArgumentNullException(nameof(page));
            float top = page.Height - margin - headerHeight;
            float bottom = margin + footerHeight;
            if (bottom >= top)
                bottom = Math.Max(0, top - 20f); // ensure a minimal region to avoid negative height

            var layout = page.Columns ?? new ColumnLayoutSpec { Columns = 1, Gutter = 14f };
            int columnCount = Math.Max(1, layout.Widths?.Length ?? layout.Columns);
            var columns = new FlowColumn[columnCount];

            float contentLeft = margin;
            float contentRight = page.Width - margin;
            float contentWidth = Math.Max(0, contentRight - contentLeft);

            float gutter = layout.Gutter;
            if (columnCount == 1) gutter = 0f;

            if (layout.Widths != null && layout.Widths.Length == columnCount)
            {
                float x = contentLeft;
                for (int i = 0; i < columnCount; i++)
                {
                    float width = layout.Widths[i];
                    columns[i] = new FlowColumn(i, x, width, top, bottom);
                    x += width + gutter;
                }
            }
            else
            {
                float totalGutter = (columnCount - 1) * gutter;
                float width = columnCount > 0 ? (contentWidth - totalGutter) / columnCount : contentWidth;
                float x = contentLeft;
                for (int i = 0; i < columnCount; i++)
                {
                    columns[i] = new FlowColumn(i, x, width, top, bottom);
                    x += width + gutter;
                }
            }

            return columns;
        }
    }
}
