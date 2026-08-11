using System.Text;
using FluentAssertions;
using PdfBuilder.Document;
using Xunit;

namespace PdfBuilder.Compliance.Tests;

public sealed class PdfComplianceTests
{
    [Fact]
    public async Task PdfA2B_CandidateEmbedsIdentificationAndCallerApprovedOutputIntent()
    {
        PdfComplianceOptions options = CreateOptions(requireValidator: false);
        options.SetIccProfile(CreateRgbIccHeader());

        PdfComplianceResult result = await PdfCompliance.GenerateAsync(
            PdfComplianceProfile.PdfA2B,
            options,
            document =>
            {
                document.Metadata(metadata => metadata.Title = "PDF/A fixture");
                document.Page(page => page.Content().Decorative().Canvas(40, 20, canvas => canvas.Line(0, 0, 40, 20)));
            });

        string pdf = Encoding.Latin1.GetString(result.Candidate);
        result.Report.PreflightPassed.Should().BeTrue();
        result.Report.IndependentValidationPerformed.Should().BeFalse();
        result.Report.IsConformant.Should().BeFalse("metadata and local preflight alone are never a conformance claim");
        pdf.Should().Contain("pdfaid:part=\"2\"")
            .And.Contain("pdfaid:conformance=\"B\"")
            .And.Contain("/OutputIntents [")
            .And.Contain("/DestOutputProfile")
            .And.Contain("/N 3")
            .And.Contain("acsp");
        result.Invoking(value => value.EnsureConformant()).Should().Throw<PdfComplianceException>();
    }

    [Fact]
    public async Task PdfUa1_CandidateRequiresIndependentValidationAndWritesTaggedIdentity()
    {
        PdfComplianceOptions options = CreateOptions(requireValidator: false);

        PdfComplianceResult result = await PdfCompliance.GenerateAsync(
            PdfComplianceProfile.PdfUa1,
            options,
            document =>
            {
                document.Metadata(metadata => metadata.Title = "PDF/UA fixture");
                document.Page(page => page.Content()
                    .Semantic(PdfSemanticRole.Figure)
                    .AlternativeText("Diagonal line")
                    .Canvas(40, 20, canvas => canvas.Line(0, 0, 40, 20)));
            });

        string pdf = Encoding.Latin1.GetString(result.Candidate);
        result.Report.PreflightPassed.Should().BeTrue();
        result.Report.IsConformant.Should().BeFalse();
        pdf.Should().Contain("pdfuaid:part=\"1\"")
            .And.Contain("/StructTreeRoot")
            .And.Contain("/Alt (Diagonal line)");
    }

    [Fact]
    public async Task PdfUa1_TableGetsRowsAndHeaderDataCellSemantics()
    {
        PdfComplianceOptions options = CreateOptions(requireValidator: false);
        PdfComplianceResult result = await PdfCompliance.GenerateAsync(PdfComplianceProfile.PdfUa1, options, document =>
        {
            document.Metadata(metadata => metadata.Title = "Tagged table fixture");
            document.Page(page => page.Content().Table(table =>
            {
                table.Columns(columns => columns.RelativeColumn());
                table.Header(row => row.Cell().Text(string.Empty));
                table.Row(row => row.Cell().Text(string.Empty));
            }));
        });

        string pdf = Encoding.Latin1.GetString(result.Candidate);
        pdf.Should().Contain("/S /Table")
            .And.Contain("/S /TR")
            .And.Contain("/S /TH")
            .And.Contain("/S /TD");
    }

    [Fact]
    public async Task Base14Text_FailsClosedInsteadOfClaimingPdfUaConformance()
    {
        PdfComplianceOptions options = CreateOptions(requireValidator: false);
        PdfComplianceResult result = await PdfCompliance.GenerateAsync(
            PdfComplianceProfile.PdfUa1,
            options,
            document => document.Page(page => page.Content().Text("Unembedded Helvetica")));

        result.Report.PreflightPassed.Should().BeFalse();
        result.Report.Findings.Should().Contain(finding => finding.Code == "font.base14");
        result.Report.IsConformant.Should().BeFalse();
    }

    [Fact]
    public async Task PdfA_WithoutIccProfile_FailsBeforeGeneration()
    {
        PdfComplianceOptions options = CreateOptions(requireValidator: false);
        Func<Task> action = () => PdfCompliance.GenerateAsync(
            PdfComplianceProfile.PdfA3B,
            options,
            document => document.Page(page => page.Content().Text("No profile")));

        await action.Should().ThrowAsync<ArgumentException>().WithMessage("*caller-approved ICC profile*");
    }

    [Theory]
    [InlineData("<report><validationReport isCompliant='true'/></report>", true)]
    [InlineData("<report><validationReport isCompliant='false'/></report>", false)]
    public void VeraPdfReport_ParsesExplicitConformanceOnly(string xml, bool expected)
        => VeraPdfValidator.ParseIsCompliant(xml).Should().Be(expected);

    [Fact]
    public void VeraPdfReport_RejectsDtdInput()
    {
        Action action = () => VeraPdfValidator.ParseIsCompliant("<!DOCTYPE report SYSTEM 'file:///etc/passwd'><report/>");
        action.Should().Throw<System.Xml.XmlException>();
    }

    [Fact]
    public async Task CommandFileValidator_IsRejectedWithoutShellInvocation()
    {
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".cmd");
        await File.WriteAllTextAsync(path, "@exit /b 0");
        try
        {
            PdfComplianceOptions options = CreateOptions(requireValidator: true);
            options.VeraPdfExecutablePath = path;
            Func<Task> action = () => PdfCompliance.GenerateAsync(
                PdfComplianceProfile.PdfUa1,
                options,
                document =>
                {
                    document.Metadata(metadata => metadata.Title = "Validator safety fixture");
                    document.Page(page => page.Content()
                        .Semantic(PdfSemanticRole.Figure)
                        .AlternativeText("Line")
                        .Canvas(40, 20, canvas => canvas.Line(0, 0, 40, 20)));
                });

            await action.Should().ThrowAsync<ArgumentException>().WithMessage("*Shell scripts and command files*");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Generation_ObservesCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        PdfComplianceOptions options = CreateOptions(requireValidator: false);

        Func<Task> action = () => PdfCompliance.GenerateAsync(
            PdfComplianceProfile.PdfUa1,
            options,
            document => document.Page(page => page.Content().Text("Cancelled")),
            cancellation.Token);

        await action.Should().ThrowAsync<OperationCanceledException>();
    }

    private static PdfComplianceOptions CreateOptions(bool requireValidator)
        => new()
        {
            Language = "en-ZA",
            RequireIndependentValidation = requireValidator,
            ValidationTimeout = TimeSpan.FromSeconds(10)
        };

    private static byte[] CreateRgbIccHeader()
    {
        byte[] profile = new byte[128];
        profile[0] = 0;
        profile[1] = 0;
        profile[2] = 0;
        profile[3] = 128;
        Encoding.ASCII.GetBytes("RGB ").CopyTo(profile, 16);
        Encoding.ASCII.GetBytes("acsp").CopyTo(profile, 36);
        return profile;
    }
}
