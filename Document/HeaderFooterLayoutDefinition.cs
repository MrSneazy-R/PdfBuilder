using System;
using System.Threading;
using PdfBuilder.Document.Layout;
using PdfBuilder.Models;

namespace PdfBuilder.Document
{
    /// <summary>
    /// Captures a ContentComposer configuration used to render header/footer content.
    /// </summary>
    public sealed class HeaderFooterLayoutDefinition
    {
        public HeaderFooterLayoutDefinition(Action<ContentComposer> configure)
        {
            Configure = configure ?? throw new ArgumentNullException(nameof(configure));
        }

        internal Action<ContentComposer> Configure { get; }

        /// <summary>
        /// Optional override for the default spacing used by the layout surface.
        /// </summary>
        public float? DefaultSpacing { get; set; }

        internal HeaderFooterLayoutDefinition Clone()
        {
            return new HeaderFooterLayoutDefinition(Configure)
            {
                DefaultSpacing = DefaultSpacing
            };
        }
    }

    internal readonly record struct HeaderFooterRenderContext(
        PdfDocument Document,
        PdfPage Page,
        int PageNumber,
        int PageCount,
        DateTime TimestampUtc);

    internal static class HeaderFooterRenderScope
    {
        private static readonly AsyncLocal<HeaderFooterRenderContext?> CurrentContext = new();

        public static HeaderFooterRenderContext Current
            => CurrentContext.Value ?? throw new InvalidOperationException("Header/footer tokens are accessed outside of a render scope.");

        public static IDisposable Push(HeaderFooterRenderContext context)
        {
            var previous = CurrentContext.Value;
            CurrentContext.Value = context;
            return new Scope(previous);
        }

        private sealed class Scope : IDisposable
        {
            private readonly HeaderFooterRenderContext? _previous;
            private bool _disposed;

            public Scope(HeaderFooterRenderContext? previous)
            {
                _previous = previous;
            }

            public void Dispose()
            {
                if (_disposed) return;
                CurrentContext.Value = _previous;
                _disposed = true;
            }
        }
    }

    /// <summary>
    /// Provides access to dynamic values (page numbers, title, timestamp) during header/footer layout.
    /// </summary>
    public static class HeaderFooterTokens
    {
        public static int PageNumber => HeaderFooterRenderScope.Current.PageNumber;

        public static int PageCount => HeaderFooterRenderScope.Current.PageCount;

        /// <summary>Gets the page number within the current section when a section-specific counter is available.</summary>
        public static int SectionPageNumber => PageNumber;

        /// <summary>Gets the total pages in the current section when a section-specific counter is available.</summary>
        public static int SectionPageCount => PageCount;

        public static string? Title => HeaderFooterRenderScope.Current.Document?.Title;

        public static DateTime NowUtc => HeaderFooterRenderScope.Current.TimestampUtc;

        public static PdfDocument Document => HeaderFooterRenderScope.Current.Document;

        public static PdfPage Page => HeaderFooterRenderScope.Current.Page;
    }
}
