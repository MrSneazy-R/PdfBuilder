using System;
using System.Diagnostics;

namespace PdfBuilder.Writer.Fonts
{
    /// <summary>
    /// Provides hooks for observing font-related diagnostics such as subset fallbacks.
    /// </summary>
    public static class FontDiagnostics
    {
        /// <summary>
        /// Gets or sets whether diagnostics are written via <see cref="Trace.WriteLine(string)"/> when no custom writer is supplied.
        /// </summary>
        public static bool Enabled { get; set; }

        /// <summary>
        /// Gets or sets a custom sink for diagnostic messages.
        /// </summary>
        public static Action<string>? Writer { get; set; }

        internal static void Report(string message)
        {
            if (Writer != null)
            {
                try
                {
                    Writer(message);
                }
                catch
                {
                    // Swallow logging exceptions.
                }
            }
            else if (Enabled)
            {
                Trace.WriteLine($"[PdfBuilder.Fonts] {message}");
            }
        }
    }
}
