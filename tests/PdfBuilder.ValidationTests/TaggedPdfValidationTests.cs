using FluentAssertions;
using PdfBuilder.Document;
using Xunit;

namespace PdfBuilder.ValidationTests;

public sealed class TaggedPdfValidationTests
{
    [Fact]
    public void TaggedFixture_PassesQpdfAndPopplerValidation()
    {
        if (!ValidationTools.TryRequire("qpdf", out string qpdf, out string qpdfReason))
        {
            ValidationTools.ReportUnavailable(qpdfReason);
            return;
        }
        if (!ValidationTools.TryRequire("pdftotext", out string pdftotext, out string popplerReason))
        {
            ValidationTools.ReportUnavailable(popplerReason);
            return;
        }

        string directory = PdfValidationHelpers.CreateTemporaryDirectory("tagged-pdf");
        string pdfPath = Path.Combine(directory, "tagged-pdf.pdf");
        PdfDocument document = PdfDocument.Create(descriptor =>
        {
            descriptor.Tagged(tagged => tagged.Language("en-ZA"));
            descriptor.Page(page =>
            {
                page.Header().Text("Semantic header");
                page.Content().Semantic(PdfSemanticRole.Section).Column(column =>
                {
                    column.Item().Heading(1).Text("Accessible structure fixture");
                    column.Item().Text("Independent extraction marker");
                    column.Item().ExternalLink("Project site", "https://example.com");
                    column.Item().Semantic(PdfSemanticRole.Figure)
                        .AlternativeText("Decorative trend line")
                        .Canvas(100, 20, canvas => canvas.Line(0, 10, 100, 10));
                });
            });
        });
        File.WriteAllBytes(pdfPath, document.GenerateBytes());

        PdfValidationHelpers.AssertStructuralValidity(qpdf, pdfPath);
        string text = PdfValidationHelpers.ExtractText(pdftotext, pdfPath, directory);
        text.Should().Contain("Accessible structure fixture")
            .And.Contain("Independent extraction marker")
            .And.Contain("Project site");
    }
}
