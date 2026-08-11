using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace PdfBuilder.Writer.Fonts
{
    /// <summary>
    /// Provides hooks for observing font-related diagnostics such as subset fallbacks.
    /// </summary>
    public static class FontDiagnostics
    {
        private static readonly ConcurrentQueue<string> _recentMessages = new();
        /// <summary>
        /// Gets or sets whether diagnostics are written via <see cref="Trace.WriteLine(string)"/> when no custom writer is supplied.
        /// </summary>
        public static bool Enabled { get; set; }

        /// <summary>
        /// Gets or sets a custom sink for diagnostic messages.
        /// </summary>
        public static Action<string>? Writer { get; set; }

        /// <summary>Gets retained font diagnostics, including full-font fallbacks, for the current process.</summary>
        public static IReadOnlyList<string> RecentMessages => _recentMessages.ToArray();

        /// <summary>Clears retained font diagnostics.</summary>
        public static void Clear() { while (_recentMessages.TryDequeue(out _)) { } }

        internal static void Report(string message)
        {
            _recentMessages.Enqueue(message);
            while (_recentMessages.Count > 256 && _recentMessages.TryDequeue(out _)) { }
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
