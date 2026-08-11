using System.Text;
using FluentAssertions;
using PdfBuilder.Document;
using Xunit;

namespace PdfBuilder.Tests;

public sealed class OutputIntentTests
{
    [Fact]
    public void OutputIntent_EmbedsValidatedIccProfileAndCatalogReference()
    {
        byte[] profile = CreateRgbIccHeader();
        PdfDocument document = PdfDocument.Create(descriptor =>
        {
            descriptor.OutputIntent(intent =>
            {
                intent.Profile(profile);
                intent.Identifier("Approved RGB");
                intent.Info("Application-approved test profile");
            });
            descriptor.Page(page => page.Content().Canvas(40, 20, canvas => canvas.Line(0, 0, 40, 20)));
        });
        profile[36] = 0;

        string pdf = Encoding.Latin1.GetString(document.GenerateBytes());

        pdf.Should().Contain("/OutputIntents [")
            .And.Contain("/Type /OutputIntent /S /GTS_PDFA1")
            .And.Contain("/OutputConditionIdentifier (Approved RGB)")
            .And.Contain("/DestOutputProfile")
            .And.Contain("/N 3")
            .And.Contain("acsp")
            .And.NotContain("/Subtype /Type1", "unused Base-14 resources must not be emitted");
    }

    [Fact]
    public void OutputIntent_MalformedOrIncompleteProfileFailsClearly()
    {
        Action malformed = () => PdfDocument.Create(descriptor => descriptor.OutputIntent(intent =>
        {
            intent.Profile(new byte[128]);
            intent.Identifier("Invalid");
        }));
        Action incomplete = () => PdfDocument.Create(descriptor => descriptor.OutputIntent(intent =>
        {
            intent.Profile(CreateRgbIccHeader());
        }));

        malformed.Should().Throw<ArgumentException>().WithMessage("*ICC 'acsp' signature*");
        incomplete.Should().Throw<InvalidOperationException>().WithMessage("*identifier*");
    }

    private static byte[] CreateRgbIccHeader()
    {
        byte[] profile = new byte[128];
        Encoding.ASCII.GetBytes("RGB ").CopyTo(profile, 16);
        Encoding.ASCII.GetBytes("acsp").CopyTo(profile, 36);
        return profile;
    }
}
