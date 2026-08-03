using System.Diagnostics;
namespace PdfBuilder.ValidationTests;

internal static class ValidationTools
{
    public static bool TryRequire(string executable, out string executablePath, out string reason)
    {
        var candidates = OperatingSystem.IsWindows()
            ? new[] { executable, executable + ".exe", executable + ".cmd", executable + ".bat" }
            : new[] { executable };
        var path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        foreach (var directory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            foreach (var candidate in candidates)
            {
                var fullPath = Path.Combine(directory, candidate);
                if (File.Exists(fullPath) && CanRun(fullPath, executable))
                {
                    executablePath = fullPath;
                    reason = string.Empty;
                    return true;
                }
            }
        }

        executablePath = string.Empty;
        reason = $"Independent PDF validation skipped locally: '{executable}' was not found or could not be executed from PATH. Install qpdf and Poppler, or run the Linux CI job where they are required.";
        return false;
    }

    public static bool ReportUnavailable(string reason)
    {
        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("CI")))
            throw new Xunit.Sdk.XunitException(reason + " CI is required to have all independent PDF validation tools installed.");

        Console.WriteLine(reason);
        return false;
    }

    public static ProcessResult Run(string executable, params string[] arguments)
    {
        var startInfo = new ProcessStartInfo(executable) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        foreach (var argument in arguments) startInfo.ArgumentList.Add(argument);
        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException($"Could not start '{executable}'.");
        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();
        return new ProcessResult(process.ExitCode, stdout, stderr);
    }

    private static bool CanRun(string executablePath, string executableName)
    {
        try
        {
            var versionArgument = string.Equals(executableName, "qpdf", StringComparison.OrdinalIgnoreCase)
                ? "--version"
                : "-v";
            return Run(executablePath, versionArgument).ExitCode == 0;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }
}

internal sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError);
