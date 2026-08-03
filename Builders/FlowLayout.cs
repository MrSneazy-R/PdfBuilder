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
    /// Cursor manager for a flow column. Coordinates follow PDF space (origin
    /// at bottom-left). <see cref="Y"/> exposes the next available baseline
    /// moving downward from the column's top.
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
        public float Available => Math.Max(0f, _cursorY - _bottomY);
        public float Capacity => Math.Max(0f, _topY - _bottomY);

        public bool CanFit(float height, float epsilon = 0.1f)
        {
            if (height <= 0f) return true;
            return (_cursorY - height) >= (_bottomY - epsilon);
        }
        /// <summary>
        /// Reserves vertical space inside the column and advances the cursor.
        /// Throws <see cref="FlowOverflowException"/> if the requested height
        /// would pass <see cref="BottomY"/>.
        /// </summary>
        public FlowRect Reserve(float height)
        {
            float clamped = Math.Max(0f, height);
            if (!CanFit(clamped))
                throw new FlowOverflowException(this, clamped);


            float top = _cursorY;
            _cursorY = Math.Max(_bottomY, _cursorY - clamped);
            return new FlowRect(X, top, Width, clamped);
        }
        /// <summary>
        /// Advances the cursor without performing any capacity checks.
        /// Negative values move the cursor upwards.
        /// </summary>
        public void Advance(float pixels)
        {
            if (pixels == 0f) return;
            _cursorY = Math.Clamp(_cursorY - pixels, _bottomY, _topY);
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
        public static FlowColumn[] Create(
            PdfPage page,
            float margin,
            int columns,
            float gutter,
            float headerHeight = 0f,
            float footerHeight = 0f,
            float[]? explicitWidths = null)
        {
            if (page == null) throw new ArgumentNullException(nameof(page));
            int columnCount = Math.Max(1, columns);
            gutter = columnCount == 1 ? 0f : Math.Max(0f, gutter);

            float horizontalMargin = margin > 0f ? margin : page.MarginLeft;
            float rightMargin = margin > 0f ? margin : page.MarginRight;
            float topMargin = margin > 0f ? margin : page.MarginTop;
            float bottomMargin = margin > 0f ? margin : page.MarginBottom;

            float top = page.Height - topMargin - headerHeight;
            float bottom = bottomMargin + footerHeight;
            if (bottom >= top)
            {
                float mid = (page.Height - headerHeight - footerHeight) * 0.5f;
                bottom = Math.Max(0f, Math.Min(mid, top - 20f));
            }

            float contentLeft = horizontalMargin;
            float contentRight = page.Width - rightMargin;
            float contentWidth = Math.Max(0f, contentRight - contentLeft);

            var columnsArr = new FlowColumn[columnCount];
            float x = contentLeft;

            if (explicitWidths != null && explicitWidths.Length == columnCount)
            {
                float totalGutter = Math.Max(0f, (columnCount - 1) * gutter);
                if (totalGutter > contentWidth && columnCount > 1)
                {
                    float gutterScale = contentWidth / totalGutter;
                    gutter = Math.Max(0f, gutter * gutterScale);
                    totalGutter = Math.Max(0f, (columnCount - 1) * gutter);
                }

                float available = Math.Max(0f, contentWidth - totalGutter);

                // Normalize explicit widths so the total fits inside the content area.
                var widths = new float[columnCount];
                float widthSum = 0f;
                for (int i = 0; i < columnCount; i++)
                {
                    widths[i] = Math.Max(0f, explicitWidths[i]);
                    widthSum += widths[i];
                }

                if (widthSum <= 0f)
                {
                    float fallback = columnCount > 0 ? available / columnCount : 0f;
                    for (int i = 0; i < columnCount; i++)
                    {
                        columnsArr[i] = new FlowColumn(i, x, fallback, top, bottom);
                        x += fallback + gutter;
                    }
                }
                else
                {
                    float scale = available > 0f ? available / widthSum : 0f;
                    float used = 0f;

                    for (int i = 0; i < columnCount; i++)
                    {
                        float width = widths[i] * scale;
                        if (i == columnCount - 1)
                        {
                            width = Math.Max(0f, available - used);
                        }

                        used += width;
                        columnsArr[i] = new FlowColumn(i, x, width, top, bottom);
                        x += width + gutter;
                    }
                }
            }
            else
            {
                float totalGutter = Math.Max(0f, (columnCount - 1) * gutter);
                if (totalGutter > contentWidth && columnCount > 1)
                {
                    float gutterScale = contentWidth / totalGutter;
                    gutter = Math.Max(0f, gutter * gutterScale);
                    totalGutter = Math.Max(0f, (columnCount - 1) * gutter);
                }
                float width = columnCount > 0 ? (contentWidth - totalGutter) / columnCount : contentWidth;
                width = Math.Max(0f, width);

                for (int i = 0; i < columnCount; i++)
                {
                    columnsArr[i] = new FlowColumn(i, x, width, top, bottom);
                    x += width + gutter;
                }
            }

            return columnsArr;
        }
    }
}
