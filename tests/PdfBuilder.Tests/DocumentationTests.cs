using System.Text.RegularExpressions;
using System.Xml.Linq;
using FluentAssertions;
using Xunit;

namespace PdfBuilder.Tests;

public sealed class DocumentationTests
{
    [Fact]
    public void Readme_UsesCanonicalQuickStart()
    {
        var readme = File.ReadAllText(FindRepositoryFile("README.md"));
        string version = ReadProjectVersion();

        readme.Should().Contain("PdfDocument.Create");
        readme.Should().Contain($"dotnet add package PdfBuilder --version {version}");
        readme.Should().NotContain("new PdfWriter().Save");
    }

    [Theory]
    [InlineData("README.md")]
    [InlineData("CHANGELOG.md")]
    [InlineData("documentation/release/release-candidate.md")]
    [InlineData("documentation/engineering/PRODUCTION-READINESS.md")]
    [InlineData("documentation/engineering/BASELINE.md")]
    [InlineData(".github/workflows/release-candidate.yml")]
    public void DocumentedPreReleaseVersion_MatchesProjectVersion(string relativePath)
    {
        string projectVersion = ReadProjectVersion();
        string content = File.ReadAllText(FindRepositoryFile(relativePath));
        var documentedVersions = Regex.Matches(content, @"0\.1\.0-preview\.\d+")
            .Select(match => match.Value)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        projectVersion.Should().MatchRegex(@"^0\.1\.0-preview\.\d+$");
        documentedVersions.Should().ContainSingle().Which.Should().Be(projectVersion);
    }

    [Fact]
    public void ComponentsThemesDocumentation_UsesCompileValidCanonicalCalls()
    {
        string documentation = File.ReadAllText(FindRepositoryFile("documentation/Components_Templates_and_Themes.md"));
        string invoiceSample = File.ReadAllText(FindRepositoryFile("samples/Invoice/Program.cs"));

        documentation.Should().Contain("public sealed class InvoiceTemplate : PdfTemplate<Invoice>");
        documentation.Should().Contain("document.Theme(theme =>");
        documentation.Should().Contain("document.Page(page =>");
        documentation.Should().NotContain(".Compose(composer =>");
        invoiceSample.Should().Contain("public sealed class InvoiceTemplate : PdfTemplate<Invoice>");
        invoiceSample.Should().Contain("template.Save(");
        invoiceSample.Should().Contain("SellerHeaderComponent");
        invoiceSample.Should().Contain("CustomerAddressComponent");
        invoiceSample.Should().Contain("InvoiceTotalsComponent");
        invoiceSample.Should().NotContain("HttpClient");
        invoiceSample.Should().NotContain("DbContext");
        invoiceSample.Should().NotContain("IServiceProvider");
    }

    [Fact]
    public void CanonicalReportSample_SatisfiesPhaseOneExitGateWithoutRawElements()
    {
        string sample = File.ReadAllText(FindRepositoryFile("samples/CanonicalReport/Program.cs"));

        sample.Should().Contain(".FirstPageHeader()")
            .And.Contain(".ContinuationHeader()")
            .And.Contain("PageTextTokens.CurrentPage")
            .And.Contain("PageTextTokens.TotalPages")
            .And.Contain(".TableOfContents(")
            .And.Contain(".InternalLink(")
            .And.Contain(".ExternalLink(")
            .And.Contain(".Section(");
        sample.Should().NotContain("new PdfDocument")
            .And.NotContain("PdfPageBuilder")
            .And.NotContain("ColumnBuilder")
            .And.NotContain("TextElement")
            .And.NotContain("PdfElement")
            .And.NotContain("AddElement");
    }

    [Theory]
    [InlineData("samples/HelloPdf/HelloPdf.csproj")]
    [InlineData("samples/Invoice/Invoice.csproj")]
    [InlineData("samples/MultiPageReport/MultiPageReport.csproj")]
    [InlineData("samples/MultiLanguage/MultiLanguage.csproj")]
    [InlineData("samples/AspNetCorePdfApi/AspNetCorePdfApi.csproj")]
    [InlineData("samples/LayoutDiagnostics/LayoutDiagnostics.csproj")]
    [InlineData("samples/CanonicalReport/CanonicalReport.csproj")]
    public void PublishedSample_ProjectExists(string relativePath)
        => File.Exists(FindRepositoryFile(relativePath)).Should().BeTrue();

    private static string FindRepositoryFile(string relativePath)
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current != null)
        {
            var candidate = Path.Combine(current.FullName, relativePath);
            if (File.Exists(candidate)) return candidate;
            current = current.Parent;
        }

        throw new FileNotFoundException($"Repository file '{relativePath}' was not found.");
    }

    private static string ReadProjectVersion()
    {
        var project = XDocument.Load(FindRepositoryFile("PdfBuilder.csproj"));
        return project.Descendants("Version").Single().Value;
    }
}
