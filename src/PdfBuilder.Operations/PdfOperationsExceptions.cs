namespace PdfBuilder.Operations;

/// <summary>Base exception for existing-PDF operations.</summary>
public class PdfOperationsException : Exception
{
    public PdfOperationsException(string message, Exception? innerException = null) : base(message, innerException) { }
}

/// <summary>Raised when qpdf exits unsuccessfully.</summary>
public sealed class QpdfProcessException : PdfOperationsException
{
    internal QpdfProcessException(int exitCode, string diagnostic)
        : base($"qpdf exited with code {exitCode}. {diagnostic}")
    {
        ExitCode = exitCode;
        Diagnostic = diagnostic;
    }

    public int ExitCode { get; }
    public string Diagnostic { get; }
}

/// <summary>Raised when a qpdf process exceeds its configured timeout.</summary>
public sealed class PdfOperationTimeoutException : PdfOperationsException
{
    internal PdfOperationTimeoutException(TimeSpan timeout)
        : base($"The qpdf operation exceeded the configured timeout of {timeout}.") => Timeout = timeout;

    public TimeSpan Timeout { get; }
}

/// <summary>Raised when an operation does not produce an independently recognisable PDF.</summary>
public sealed class PdfOutputValidationException : PdfOperationsException
{
    internal PdfOutputValidationException(string message) : base(message) { }
}
