using System;
using System.Collections.Generic;

namespace PdfBuilder.Document.Layout.Components
{
    public sealed class GridComponent : IMeasurable
    {
        private readonly List<IMeasurable> _children = new();

        public int Columns { get; set; } = 2;
        public float RowGap { get; set; } = 12f;
        public float ColumnGap { get; set; } = 12f;

        public GridComponent Add(IMeasurable child)
        {
            if (child == null) throw new ArgumentNullException(nameof(child));
            _children.Add(child);
            return this;
        }

        public LayoutMeasurement Measure(LayoutMeasureContext context)
        {
            if (_children.Count == 0 || Columns < 1)
            {
                return new LayoutMeasurement(0f, 0f, 0f, 0f, new GridMetadata(null, null), avoidBreakInside: false);
            }

            var column = new ColumnComponent { Spacing = RowGap };
            int index = 0;
            while (index < _children.Count)
            {
                var row = new RowComponent { Gap = ColumnGap };
                for (int c = 0; c < Columns && index < _children.Count; c++, index++)
                {
                    row.Add(_children[index]);
                }
                column.Add(row);
            }

            var columnMeasurement = column.Measure(context);
            var metadata = new GridMetadata(column, columnMeasurement.Metadata);

            return new LayoutMeasurement(
                columnMeasurement.MarginTop,
                columnMeasurement.ContentHeight,
                columnMeasurement.MarginBottom,
                columnMeasurement.UsedWidth,
                metadata,
                columnMeasurement.AvoidBreakInside,
                columnMeasurement.Result,
                columnMeasurement.Remainder);
        }

        public void Draw(LayoutDrawContext context, LayoutMeasurement measurement)
        {
            if (measurement.Metadata is not GridMetadata metadata)
                throw new InvalidOperationException("Grid layout metadata missing.");

            if (metadata.Column == null || metadata.ColumnMetadata == null)
                return;

            var columnMeasurement = new LayoutMeasurement(
                measurement.MarginTop,
                measurement.ContentHeight,
                measurement.MarginBottom,
                measurement.UsedWidth,
                metadata.ColumnMetadata,
                measurement.AvoidBreakInside,
                measurement.Result,
                null);

            metadata.Column.Draw(context, columnMeasurement);
        }

        private sealed class GridMetadata
        {
            public GridMetadata(ColumnComponent? column, object? columnMetadata)
            {
                Column = column;
                ColumnMetadata = columnMetadata;
            }

            public ColumnComponent? Column { get; }
            public object? ColumnMetadata { get; }
        }
    }
}
