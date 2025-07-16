namespace PdfBuilder.Document
{
    /// <summary>
    /// Abstract base class for all drawable PDF elements.
    /// </summary>
    public abstract class PdfElement
    {
        /// <summary>
        /// X coordinate (in points) for element placement on the page.
        /// </summary>
        public float X { get; set; }

        /// <summary>
        /// Y coordinate (in points) for element placement on the page.
        /// </summary>
        public float Y { get; set; }

        protected PdfElement(float x, float y)
        {
            X = x;
            Y = y;
        }
    }
}
