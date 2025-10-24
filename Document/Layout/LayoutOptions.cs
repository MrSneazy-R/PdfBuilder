namespace PdfBuilder.Document.Layout
{
    public sealed class LayoutDebugOptions
    {
        public bool DrawBoundingBoxes { get; set; }
        public bool ShowFlowGuides { get; set; }
        public bool TraceLayout { get; set; }
    }

    /// <summary>
    /// Stores layout engine preferences for a document or individual page.
    /// </summary>
    public sealed class LayoutOptions
    {
        public LayoutMode Mode { get; set; } = LayoutMode.MeasureDraw;

        public bool EnableMeasurementCaching { get; set; }

        public LayoutDebugOptions Debug { get; } = new LayoutDebugOptions();

        public LayoutProfilerConfig Profiler { get; } = new LayoutProfilerConfig();

        /// <summary>
        /// Backwards compatible access to trace flag.
        /// </summary>
        public bool TraceLayout
        {
            get => Debug.TraceLayout;
            set => Debug.TraceLayout = value;
        }

        /// <summary>
        /// Clone to avoid sharing mutable configuration between document and page instances.
        /// </summary>
        public LayoutOptions Clone()
        {
            var clone = new LayoutOptions
            {
                Mode = Mode,
                EnableMeasurementCaching = EnableMeasurementCaching
            };

            clone.Debug.DrawBoundingBoxes = Debug.DrawBoundingBoxes;
            clone.Debug.ShowFlowGuides = Debug.ShowFlowGuides;
            clone.Debug.TraceLayout = Debug.TraceLayout;
            clone.Profiler.Enabled = Profiler.Enabled;
            clone.Profiler.OutputPath = Profiler.OutputPath;
            clone.Profiler.OnCompleted = Profiler.OnCompleted;
            return clone;
        }
    }
}

