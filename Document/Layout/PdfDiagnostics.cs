using System.Collections.Concurrent;
using System.Text.Json;

namespace PdfBuilder.Document.Layout;

/// <summary>Configures optional diagnostics that do not affect normal document output.</summary>
public sealed class PdfDiagnosticsOptions
{
    /// <summary>Gets or sets whether structured layout events are recorded.</summary>
    public bool EnableLayoutTrace { get; set; }

    /// <summary>Gets or sets whether caller-provided text is included in trace events. Disabled by default.</summary>
    public bool IncludeTextContent { get; set; }

    /// <summary>Gets or sets the maximum number of placement attempts before a layout failure is reported.</summary>
    public int LayoutIterationLimit { get; set; } = 32;

    internal PdfDiagnosticsOptions Clone() => new()
    {
        EnableLayoutTrace = EnableLayoutTrace,
        IncludeTextContent = IncludeTextContent,
        LayoutIterationLimit = LayoutIterationLimit
    };
}

/// <summary>Represents one structured event emitted by the layout pipeline.</summary>
public sealed class PdfLayoutTraceEntry
{
    /// <summary>Gets the event category.</summary>
    public string Event { get; init; } = string.Empty;
    /// <summary>Gets the component path.</summary>
    public string ComponentPath { get; init; } = string.Empty;
    /// <summary>Gets the component type or debug label.</summary>
    public string Component { get; init; } = string.Empty;
    /// <summary>Gets the one-based page number.</summary>
    public int PageNumber { get; init; }
    /// <summary>Gets the zero-based column index.</summary>
    public int ColumnIndex { get; init; }
    /// <summary>Gets the available width in points.</summary>
    public float AvailableWidth { get; init; }
    /// <summary>Gets the available height in points.</summary>
    public float AvailableHeight { get; init; }
    /// <summary>Gets the result returned by measurement.</summary>
    public string? Result { get; init; }
    /// <summary>Gets whether a remainder was returned.</summary>
    public bool HasRemainder { get; init; }
    /// <summary>Gets the elapsed component operation time in milliseconds.</summary>
    public double ElapsedMilliseconds { get; init; }
    /// <summary>Gets whether the event came from the measurement cache.</summary>
    public bool CacheHit { get; init; }
    /// <summary>Gets a non-sensitive warning, when applicable.</summary>
    public string? Warning { get; init; }
}

/// <summary>Thread-safe, document-scoped collection of structured layout events.</summary>
public sealed class PdfLayoutTrace
{
    private readonly ConcurrentQueue<PdfLayoutTraceEntry> _entries = new();

    /// <summary>Gets trace events in the order they were recorded.</summary>
    public IReadOnlyList<PdfLayoutTraceEntry> Entries => _entries.ToArray();

    /// <summary>Removes all prior trace events.</summary>
    public void Clear()
    {
        while (_entries.TryDequeue(out _)) { }
    }

    /// <summary>Serializes the trace to JSON without document text content.</summary>
    public string ToJson(bool indented = true) => JsonSerializer.Serialize(Entries, new JsonSerializerOptions { WriteIndented = indented });

    internal void Record(PdfLayoutTraceEntry entry) => _entries.Enqueue(entry);
}

/// <summary>Base exception for document composition and rendering failures.</summary>
public class PdfCompositionException : Exception
{
    /// <summary>Initializes a composition exception.</summary>
    public PdfCompositionException(string message, Exception? innerException = null) : base(message, innerException) { }
}

/// <summary>Describes an actionable layout failure at a component boundary.</summary>
public sealed class PdfLayoutException : PdfCompositionException
{
    /// <summary>Initializes a layout exception with the supplied diagnostic context.</summary>
    public PdfLayoutException(string message, PdfLayoutFailureContext context, Exception? innerException = null) : base(message, innerException)
        => Context = context ?? throw new ArgumentNullException(nameof(context));

    /// <summary>Gets the detailed diagnostic context.</summary>
    public PdfLayoutFailureContext Context { get; }
}

/// <summary>Contains the data required to diagnose a layout failure.</summary>
public sealed class PdfLayoutFailureContext
{
    /// <summary>Gets or initializes the component path.</summary>
    public string ComponentPath { get; init; } = string.Empty;
    /// <summary>Gets or initializes the component type or debug label.</summary>
    public string Component { get; init; } = string.Empty;
    /// <summary>Gets or initializes the one-based page number.</summary>
    public int PageNumber { get; init; }
    /// <summary>Gets or initializes the section name when available.</summary>
    public string? Section { get; init; }
    /// <summary>Gets or initializes the zero-based column index.</summary>
    public int ColumnIndex { get; init; }
    /// <summary>Gets or initializes the available width in points.</summary>
    public float AvailableWidth { get; init; }
    /// <summary>Gets or initializes the available height in points.</summary>
    public float AvailableHeight { get; init; }
    /// <summary>Gets or initializes the requested width in points.</summary>
    public float? RequestedWidth { get; init; }
    /// <summary>Gets or initializes the requested height in points.</summary>
    public float? RequestedHeight { get; init; }
    /// <summary>Gets or initializes the measured width in points.</summary>
    public float? MeasuredWidth { get; init; }
    /// <summary>Gets or initializes the measured height in points.</summary>
    public float? MeasuredHeight { get; init; }
    /// <summary>Gets or initializes the effective break policy.</summary>
    public string BreakPolicy { get; init; } = string.Empty;
    /// <summary>Gets or initializes the number of layout attempts.</summary>
    public int LayoutIterationCount { get; init; }
    /// <summary>Gets or initializes relevant constraints, expressed as stable diagnostic strings.</summary>
    public IReadOnlyDictionary<string, string> StyleConstraints { get; init; } = new Dictionary<string, string>();
    /// <summary>Gets or initializes suggested corrective actions.</summary>
    public IReadOnlyList<string> SuggestedActions { get; init; } = Array.Empty<string>();
}

/// <summary>Represents an error raised while drawing a resolved layout.</summary>
public sealed class PdfDrawingException : PdfCompositionException
{
    /// <summary>Initializes a drawing exception.</summary>
    public PdfDrawingException(string message, Exception? innerException = null) : base(message, innerException) { }
}

/// <summary>Represents an error raised while resolving or embedding a font.</summary>
public sealed class PdfFontException : PdfCompositionException
{
    /// <summary>Initializes a font exception.</summary>
    public PdfFontException(string message, Exception? innerException = null) : base(message, innerException) { }
}

/// <summary>Represents an error raised while validating or rendering media.</summary>
public sealed class PdfMediaException : PdfCompositionException
{
    /// <summary>Initializes a media exception.</summary>
    public PdfMediaException(string message, Exception? innerException = null) : base(message, innerException) { }
}
