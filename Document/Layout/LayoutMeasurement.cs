namespace PdfBuilder.Document.Layout
{
    public enum LayoutResultKind
    {
        Full = 0,
        Partial = 1,
        Wrap = 2
    }

    /// <summary>
    /// Measurement data returned from the measure phase. Components may attach arbitrary metadata
    /// that can be reused during drawing to avoid repeating expensive calculations.
    /// </summary>
    public sealed class LayoutMeasurement
    {
        public LayoutMeasurement(
            float marginTop,
            float contentHeight,
            float marginBottom,
            float usedWidth,
            object? metadata = null,
            bool avoidBreakInside = false,
            LayoutResultKind result = LayoutResultKind.Full,
            IMeasurable? remainder = null)
        {
            MarginTop = marginTop;
            ContentHeight = contentHeight;
            MarginBottom = marginBottom;
            UsedWidth = usedWidth;
            Metadata = metadata;
            AvoidBreakInside = avoidBreakInside;
            Result = result;
            Remainder = remainder;
        }

        public float MarginTop { get; }

        public float ContentHeight { get; }

        public float MarginBottom { get; }

        public float UsedWidth { get; }

        public object? Metadata { get; }

        public bool AvoidBreakInside { get; }

        public LayoutResultKind Result { get; }

        public IMeasurable? Remainder { get; }

        public bool IsWrap => Result == LayoutResultKind.Wrap;

        public bool IsPartial => Result == LayoutResultKind.Partial;

        public float ReservedHeight => MarginTop + ContentHeight + MarginBottom;

        public static LayoutMeasurement Wrap(float usedWidth = 0f, object? metadata = null)
        {
            return new LayoutMeasurement(
                marginTop: 0f,
                contentHeight: 0f,
                marginBottom: 0f,
                usedWidth: usedWidth,
                metadata: metadata,
                avoidBreakInside: true,
                result: LayoutResultKind.Wrap);
        }
    }
}
