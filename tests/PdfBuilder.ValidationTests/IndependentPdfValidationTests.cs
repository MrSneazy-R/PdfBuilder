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
                text.Should().Contain(marker, $"{fixture.Name} must expose its declared marker through independent text extraction");

            CountPages(qpdf, pdfPath).Should().Be(fixture.PageCount, $"{fixture.Name} has a declared page count");
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
            actualPages.Should().HaveCount(fixture.PageCount);

            for (var index = 0; index < actualPages.Count; index++)
            {
                var approved = Path.Combine(baselineDirectory, $"{fixture.Name}-{index + 1}.png");
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
                PdfValidationHelpers.CompareImages(approved, actualPages[index], Path.Combine(failureDirectory, fixture.Name));
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
            ValidationTools.TryRequire("qpdf", out _, out var reason).Should().BeFalse();
            reason.Should().Be("Independent PDF validation skipped locally: 'qpdf' was not found on PATH. Install qpdf and Poppler, or run the Linux CI job where they are required.");
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
}
