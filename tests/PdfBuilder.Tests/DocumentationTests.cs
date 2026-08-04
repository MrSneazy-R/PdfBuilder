using FluentAssertions;
using Xunit;

namespace PdfBuilder.Tests;

public sealed class DocumentationTests
{
    [Fact]
    public void Readme_UsesCanonicalQuickStart()
    {
        var readme = File.ReadAllText(FindRepositoryFile("README.md"));

        readme.Should().Contain("PdfDocument.Create");
        readme.Should().Contain("dotnet add package PdfBuilder --prerelease");
        readme.Should().NotContain("new PdfWriter().Save");
    }

    [Theory]
    [InlineData("samples/HelloPdf/HelloPdf.csproj")]
    [InlineData("samples/Invoice/Invoice.csproj")]
    [InlineData("samples/MultiPageReport/MultiPageReport.csproj")]
    [InlineData("samples/MultiLanguage/MultiLanguage.csproj")]
    [InlineData("samples/AspNetCorePdfApi/AspNetCorePdfApi.csproj")]
    [InlineData("samples/LayoutDiagnostics/LayoutDiagnostics.csproj")]
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
}
