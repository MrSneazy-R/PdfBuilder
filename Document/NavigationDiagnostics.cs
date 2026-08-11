using System.Collections.Concurrent;
using PdfBuilder.Document.Layout;

namespace PdfBuilder.Document;

/// <summary>Represents a non-fatal canonical navigation diagnostic.</summary>
public sealed class PdfNavigationDiagnostic
{
    internal PdfNavigationDiagnostic(string code, string message, string target)
    {
        Code = code;
        Message = message;
        Target = target;
    }

    /// <summary>Gets the stable diagnostic code.</summary>
    public string Code { get; }
    /// <summary>Gets the actionable diagnostic message.</summary>
    public string Message { get; }
    /// <summary>Gets the unresolved or rejected navigation target.</summary>
    public string Target { get; }
}

/// <summary>Thread-safe document-scoped navigation diagnostics.</summary>
public sealed class PdfNavigationDiagnostics
{
    private readonly ConcurrentQueue<PdfNavigationDiagnostic> _entries = new();

    /// <summary>Gets retained diagnostics in discovery order.</summary>
    public IReadOnlyList<PdfNavigationDiagnostic> Entries => _entries.ToArray();

    /// <summary>Clears retained navigation diagnostics.</summary>
    public void Clear()
    {
        while (_entries.TryDequeue(out _)) { }
    }

    internal void Add(string code, string message, string target)
        => _entries.Enqueue(new PdfNavigationDiagnostic(code, message, target));
}

/// <summary>Raised when canonical navigation cannot be composed safely.</summary>
public sealed class PdfNavigationException : PdfCompositionException
{
    /// <summary>Initializes a navigation composition exception.</summary>
    public PdfNavigationException(string message) : base(message) { }
}
