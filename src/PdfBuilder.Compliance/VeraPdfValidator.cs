using System.Diagnostics;
using System.Xml;

namespace PdfBuilder.Compliance;

internal sealed record VeraPdfValidationResult(bool Performed, bool Passed, string? Report, string? Failure);

internal static class VeraPdfValidator
{
    private const int MaximumReportCharacters = 4 * 1024 * 1024;

    internal static async Task<VeraPdfValidationResult> ValidateAsync(
        byte[] candidate,
        PdfComplianceProfile profile,
        PdfComplianceOptions options,
        CancellationToken cancellationToken)
    {
        (string executable, string[] prefixArguments)? command = ResolveCommand(options);
        if (command == null)
            return new VeraPdfValidationResult(false, false, null, "No shell-free veraPDF executable or Java/JAR pair was configured.");

        string temporaryBase = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "PdfBuilder.Compliance"));
        string root = Path.GetFullPath(Path.Combine(temporaryBase, Guid.NewGuid().ToString("N")));
        if (!root.StartsWith(temporaryBase + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The compliance temporary directory escaped its bounded root.");
        Directory.CreateDirectory(root);
        string input = Path.Combine(root, "candidate.pdf");
        try
        {
            await File.WriteAllBytesAsync(input, candidate, cancellationToken).ConfigureAwait(false);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(options.ValidationTimeout);
            var start = new ProcessStartInfo(command.Value.executable)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            foreach (string argument in command.Value.prefixArguments) start.ArgumentList.Add(argument);
            start.ArgumentList.Add("--format");
            start.ArgumentList.Add("xml");
            start.ArgumentList.Add("--flavour");
            start.ArgumentList.Add(ProfileArgument(profile));
            start.ArgumentList.Add("--maxfailuresdisplayed");
            start.ArgumentList.Add("100");
            start.ArgumentList.Add(input);

            using Process process = Process.Start(start) ?? throw new InvalidOperationException("Could not start veraPDF.");
            Task<string> stdout = ReadBoundedAsync(process.StandardOutput, timeout.Token);
            Task<string> stderr = ReadBoundedAsync(process.StandardError, timeout.Token);
            try
            {
                await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                if (!process.HasExited) process.Kill(entireProcessTree: true);
                throw;
            }
            string report = await stdout.ConfigureAwait(false);
            string error = await stderr.ConfigureAwait(false);
            bool passed;
            string? parseFailure = null;
            try
            {
                passed = process.ExitCode == 0 && ParseIsCompliant(report);
            }
            catch (XmlException exception)
            {
                passed = false;
                parseFailure = $"veraPDF returned malformed XML: {exception.Message}";
            }
            string? failure = passed
                ? null
                : parseFailure ?? (string.IsNullOrWhiteSpace(error) ? "veraPDF reported non-conformance." : error.Trim());
            return new VeraPdfValidationResult(true, passed, report, failure);
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    internal static bool ParseIsCompliant(string report)
    {
        using var reader = XmlReader.Create(new StringReader(report), new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = MaximumReportCharacters });
        while (reader.Read())
            if (reader.NodeType == XmlNodeType.Element && reader.LocalName.Equals("validationReport", StringComparison.OrdinalIgnoreCase))
                return bool.TryParse(reader.GetAttribute("isCompliant"), out bool compliant) && compliant;
        return false;
    }

    private static (string executable, string[] prefixArguments)? ResolveCommand(PdfComplianceOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.VeraPdfExecutablePath))
            return (ValidateExecutable(options.VeraPdfExecutablePath), Array.Empty<string>());
        if (!string.IsNullOrWhiteSpace(options.JavaExecutablePath) && !string.IsNullOrWhiteSpace(options.VeraPdfJarPath))
        {
            string java = ValidateExecutable(options.JavaExecutablePath);
            string jar = Path.GetFullPath(options.VeraPdfJarPath);
            if (!File.Exists(jar) || !jar.EndsWith(".jar", StringComparison.OrdinalIgnoreCase))
                throw new FileNotFoundException("The configured veraPDF CLI JAR was not found or is not a .jar file.", jar);
            return (java, new[] { "-jar", jar });
        }
        return null;
    }

    private static string ValidateExecutable(string path)
    {
        string fullPath = Path.GetFullPath(path);
        string extension = Path.GetExtension(fullPath);
        if (extension.Equals(".bat", StringComparison.OrdinalIgnoreCase) || extension.Equals(".cmd", StringComparison.OrdinalIgnoreCase) || extension.Equals(".sh", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Shell scripts and command files are not accepted; configure a native executable or a Java executable plus veraPDF JAR.", nameof(path));
        if (!File.Exists(fullPath)) throw new FileNotFoundException("The configured validator executable was not found.", fullPath);
        return fullPath;
    }

    private static async Task<string> ReadBoundedAsync(StreamReader reader, CancellationToken cancellationToken)
    {
        var buffer = new char[4096];
        var result = new System.Text.StringBuilder();
        while (true)
        {
            int read = await reader.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false);
            if (read == 0) return result.ToString();
            if (result.Length + read > MaximumReportCharacters)
                throw new InvalidDataException($"veraPDF output exceeded {MaximumReportCharacters} characters.");
            result.Append(buffer, 0, read);
        }
    }

    private static string ProfileArgument(PdfComplianceProfile profile) => profile switch
    {
        PdfComplianceProfile.PdfA2B => "2b",
        PdfComplianceProfile.PdfA3B => "3b",
        PdfComplianceProfile.PdfUa1 => "ua1",
        _ => throw new ArgumentOutOfRangeException(nameof(profile))
    };
}
