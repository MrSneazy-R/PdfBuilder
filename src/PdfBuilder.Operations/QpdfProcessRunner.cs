using System.Diagnostics;
using System.Text;

namespace PdfBuilder.Operations;

internal sealed class QpdfProcessRunner
{
    private readonly QpdfBackendOptions _options;

    internal QpdfProcessRunner(QpdfBackendOptions options) => _options = options;

    internal async Task<QpdfProcessResult> RunAsync(
        IEnumerable<string> arguments,
        string workingDirectory,
        CancellationToken cancellationToken)
    {
        ValidateOptions();
        var startInfo = new ProcessStartInfo
        {
            FileName = _options.QpdfPath,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (string argument in arguments)
        {
            if (argument.Contains('\0'))
                throw new ArgumentException("qpdf arguments cannot contain NUL characters.", nameof(arguments));
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = startInfo };
        try
        {
            if (!process.Start())
                throw new PdfOperationsException("The qpdf process could not be started.");
        }
        catch (Exception exception) when (exception is not PdfOperationsException)
        {
            throw new PdfOperationsException($"Unable to start qpdf from '{_options.QpdfPath}'.", exception);
        }

        using var timeout = new CancellationTokenSource(_options.ProcessTimeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        Task<string> stdout = ReadBoundedAsync(process.StandardOutput, linked.Token);
        Task<string> stderr = ReadBoundedAsync(process.StandardError, linked.Token);

        try
        {
            await Task.WhenAll(process.WaitForExitAsync(linked.Token), stdout, stderr).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            Kill(process);
            throw new PdfOperationTimeoutException(_options.ProcessTimeout);
        }
        catch
        {
            Kill(process);
            throw;
        }

        string standardOutput = await stdout.ConfigureAwait(false);
        string standardError = await stderr.ConfigureAwait(false);
        if (process.ExitCode != 0)
        {
            string diagnostic = string.IsNullOrWhiteSpace(standardError) ? standardOutput : standardError;
            throw new QpdfProcessException(process.ExitCode, diagnostic.Trim());
        }

        return new QpdfProcessResult(standardOutput, standardError);
    }

    private async Task<string> ReadBoundedAsync(StreamReader reader, CancellationToken cancellationToken)
    {
        var result = new StringBuilder(Math.Min(_options.MaximumCapturedCharacters, 16_384));
        char[] buffer = new char[4096];
        while (true)
        {
            int read = await reader.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false);
            if (read == 0)
                return result.ToString();
            if (result.Length + read > _options.MaximumCapturedCharacters)
                throw new PdfOperationsException($"qpdf process output exceeded {_options.MaximumCapturedCharacters} characters.");
            result.Append(buffer, 0, read);
        }
    }

    private void ValidateOptions()
    {
        if (string.IsNullOrWhiteSpace(_options.QpdfPath))
            throw new InvalidOperationException("A qpdf executable path is required.");
        if (_options.ProcessTimeout <= TimeSpan.Zero)
            throw new InvalidOperationException("The qpdf process timeout must be positive.");
        if (_options.MaximumCapturedCharacters <= 0)
            throw new InvalidOperationException("The qpdf captured-output limit must be positive.");
    }

    private static void Kill(Process process)
    {
        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch
        {
            // Preserve the primary timeout, cancellation, or output-bound failure.
        }
    }
}

internal sealed record QpdfProcessResult(string StandardOutput, string StandardError);
