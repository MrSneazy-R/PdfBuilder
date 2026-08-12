using FluentAssertions;
using Xunit;

namespace PdfBuilder.ValidationTests;

public sealed class IndependentPdfValidationTests
{
    [Fact]
    public void EveryFixture_PassesIndependentStructuralAndTextValidation()
    {
        if (!ValidationTools.TryRequire("qpdf", out var qpdf, out var qpdfReason))
        {
            ValidationTools.ReportUnavailable(qpdfReason);
            return;
        }

        if (!ValidationTools.TryRequire("pdftotext", out var pdftotext, out var pdftotextReason))
        {
            ValidationTools.ReportUnavailable(pdftotextReason);
            return;
        }

        foreach (var fixture in FixtureManifest.Load())
        {
            var directory = PdfValidationHelpers.CreateTemporaryDirectory(fixture.Name);
            var pdfPath = Path.Combine(directory, fixture.Name + ".pdf");
            File.WriteAllBytes(pdfPath, ValidationFixtureFactory.Generate(fixture.Name));

            PdfValidationHelpers.AssertStructuralValidity(qpdf, pdfPath);
            var text = PdfValidationHelpers.ExtractText(pdftotext, pdfPath, directory);
            foreach (var marker in fixture.TextMarkers)
                NormalizeWhitespace(text).Should().Contain(
                    NormalizeWhitespace(marker),
                    $"{fixture.Name} must expose its declared marker through independent text extraction, allowing only extractor whitespace differences");

            CountPages(qpdf, pdfPath).Should().Be(fixture.ExpectedPageCount, $"{fixture.Name} has a declared platform page count");
        }
    }

    [Fact]
    public void VisualFixtures_MatchApprovedBaselines()
    {
        if (!ValidationTools.TryRequire("pdftoppm", out var pdftoppm, out var pdftoppmReason))
        {
            ValidationTools.ReportUnavailable(pdftoppmReason);
            return;
        }
        var failureDirectory = Environment.GetEnvironmentVariable("PDFBUILDER_VISUAL_FAILURE_DIRECTORY")
            ?? Path.Combine(Path.GetTempPath(), "PdfBuilder.Validation.Failures");
        var baselineDirectory = Environment.GetEnvironmentVariable("PDFBUILDER_APPROVED_BASELINE_DIRECTORY")
            ?? Path.Combine(AppContext.BaseDirectory, "Baselines");
        var approveBaselines = string.Equals(Environment.GetEnvironmentVariable("PDFBUILDER_APPROVE_VISUAL_BASELINES"), "true", StringComparison.OrdinalIgnoreCase);

        foreach (var fixture in FixtureManifest.Load().Where(entry => entry.Visual))
        {
            var directory = PdfValidationHelpers.CreateTemporaryDirectory(fixture.Name);
            var pdfPath = Path.Combine(directory, fixture.Name + ".pdf");
            File.WriteAllBytes(pdfPath, ValidationFixtureFactory.Generate(fixture.Name));
            var actualPages = PdfValidationHelpers.Rasterize(pdftoppm, pdfPath, directory);
            actualPages.Should().HaveCount(fixture.ExpectedPageCount);
            var selectedPages = fixture.VisualPages?.ToHashSet() ?? Enumerable.Range(1, fixture.ExpectedPageCount).ToHashSet();

            for (var index = 0; index < actualPages.Count; index++)
            {
                var pageNumber = index + 1;
                if (!selectedPages.Contains(pageNumber))
                    continue;
                var approvedFileName = $"{fixture.Name}-{pageNumber}.png";
                var platformApproved = Path.Combine(baselineDirectory, GetPlatformBaselineDirectory(), approvedFileName);
                var approved = File.Exists(platformApproved)
                    ? platformApproved
                    : Path.Combine(baselineDirectory, approvedFileName);
                if (!File.Exists(approved))
                {
                    if (approveBaselines)
                    {
                        Directory.CreateDirectory(baselineDirectory);
                        File.Copy(actualPages[index], approved, overwrite: true);
                        continue;
                    }

                    var missingBaselineDirectory = Path.Combine(failureDirectory, fixture.Name);
                    Directory.CreateDirectory(missingBaselineDirectory);
                    File.Copy(actualPages[index], Path.Combine(missingBaselineDirectory, Path.GetFileName(actualPages[index])), overwrite: true);
                    throw new Xunit.Sdk.XunitException($"Approved baseline is missing for {fixture.Name} page {index + 1}. The actual raster was written to '{missingBaselineDirectory}'.");
                }
                try
                {
                    PdfValidationHelpers.CompareImages(
                        approved,
                        actualPages[index],
                        Path.Combine(failureDirectory, fixture.Name),
                        GetVisualTolerance(fixture.Name));
                }
                catch
                {
                    var output = Path.Combine(failureDirectory, fixture.Name);
                    Directory.CreateDirectory(output);
                    File.Copy(pdfPath, Path.Combine(output, fixture.Name + ".pdf"), overwrite: true);
                    File.WriteAllText(
                        Path.Combine(output, "layout-trace.json"),
                        $"{{\"fixture\":\"{fixture.Name}\",\"page\":{index + 1},\"event\":\"visual-regression-failure\",\"textIncluded\":false}}");
                    throw;
                }
            }
        }
    }

    [Fact]
    public void MissingValidatorTools_ProduceExplicitLocalSkipReason()
    {
        var originalPath = Environment.GetEnvironmentVariable("PATH");
        try
        {
            Environment.SetEnvironmentVariable("PATH", string.Empty);
            ValidationTools.TryRequire("qpdf", out _, out var reason, allowConfiguredPath: false).Should().BeFalse();
            reason.Should().Be("Independent PDF validation skipped locally: 'qpdf' was not found or could not be executed from PATH. Install qpdf and Poppler, or run the Linux CI job where they are required.");
        }
        finally
        {
            Environment.SetEnvironmentVariable("PATH", originalPath);
        }
    }

    private static int CountPages(string qpdf, string pdfPath)
    {
        var result = ValidationTools.Run(qpdf, "--show-npages", pdfPath);
        result.ExitCode.Should().Be(0, result.StandardOutput + result.StandardError);
        return int.Parse(result.StandardOutput.Trim(), System.Globalization.CultureInfo.InvariantCulture);
    }

    private static string NormalizeWhitespace(string value)
        => string.Concat(value.Where(character => !char.IsWhiteSpace(character)));

    private static string GetPlatformBaselineDirectory() =>
        OperatingSystem.IsLinux() ? "linux" : "default";

    private static double GetVisualTolerance(string fixtureName)
    {
        // The sanitised canonical-layout fixture uses platform font rasterisation for its
        // text and rounded decoration edges. Retain a fixture-specific Linux allowance
        // until a Linux-approved baseline is generated on a pinned rasteriser.
        return OperatingSystem.IsLinux() && string.Equals(fixtureName, "layout-primitives", StringComparison.Ordinal)
            ? 0.02d
            : OperatingSystem.IsLinux() ? 0.006d : 0.002d;
    }
}
