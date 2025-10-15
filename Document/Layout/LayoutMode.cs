namespace PdfBuilder.Document.Layout
{
    /// <summary>
    /// Controls how components are processed during layout.
    /// </summary>
    public enum LayoutMode
    {
        /// <summary>
        /// Existing single-pass behaviour where builders emit content directly.
        /// </summary>
        SinglePass = 0,

        /// <summary>
        /// Two-phase measure/draw pipeline inspired by QuestPDF.
        /// </summary>
        MeasureDraw = 1
    }
}
