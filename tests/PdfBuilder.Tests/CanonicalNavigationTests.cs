using System.Text;
using System.Text.RegularExpressions;
using FluentAssertions;
using PdfBuilder.Document;
using Xunit;

namespace PdfBuilder.Tests;

public sealed class CanonicalNavigationTests
{
    [Fact]
    public void TableOfContents_BeforeSections_ResolvesFinalPageNumbersAndExtraction()
    {
        PdfDocument document = PdfDocument.Create(descriptor =>
        {
            descriptor.Page(page =>
            {
                page.Content().Text("Contents").Bold();
                page.Content().TableOfContents(options => options.PageNumberFormat("page {0}"));
            });
            descriptor.Page(page => page.Content().Section("intro", "Introduction", section =>
                section.Text("Introduction body")));
            descriptor.Page(page => page.Content().Section("details", "Details", section =>
                section.Text("Details body")));
        });

        byte[] bytes = document.GenerateBytes();
        string text = NormalizeWhitespace(ExtractText(bytes));

        text.Should().Contain("1 Introduction").And.Contain("page 2");
        text.Should().Contain("2 Details").And.Contain("page 3");
        Encoding.Latin1.GetString(bytes).Split("/Subtype /Link", StringSplitOptions.None)
            .Should().HaveCountGreaterThanOrEqualTo(3, "each TOC title links to its final section target");
    }

    [Fact]
    public void Sections_NestedNumberingAndDuplicateTitles_ComposePredictably()
    {
        PdfDocument document = PdfDocument.Create(descriptor => descriptor.Page(page =>
        {
            page.Content().Section("overview-a", "Overview", content => content.Text("First"));
            page.Content().Section("overview-b", "Overview", content => content.Text("Second"), section => section.Level(2));
            page.Content().Section("appendix", "Appendix", content => content.Text("Third"), section => section.Numbered(false));
            page.Content().TableOfContents();
        }));

        document.Pagination.Sections.Select(section => section.Number)
            .Should().Equal("1", "1.1", string.Empty);
        document.Pagination.Sections.Select(section => section.Title)
            .Should().Equal("Overview", "Overview", "Appendix");
        NormalizeWhitespace(ExtractText(document.GenerateBytes()))
            .Should().Contain("1.1 Overview").And.Contain("Appendix");
    }

    [Fact]
    public void DuplicateAnchorIds_FailExplicitlyDuringComposition()
    {
        Action compose = () => PdfDocument.Create(descriptor => descriptor.Page(page =>
        {
            page.Content().Anchor("duplicate");
            page.Content().Bookmark("duplicate", "Duplicate");
        }));

        compose.Should().Throw<PdfNavigationException>()
            .WithMessage("*Duplicate navigation anchor id 'duplicate'*");
    }

    [Fact]
    public void Section_StartOnNewPage_UsesOneBoundedBreakOnlyWhenPriorContentExists()
    {
        PdfDocument withPriorContent = PdfDocument.Create(descriptor => descriptor.Page(page =>
        {
            page.Content().Text("Preface");
            page.Content().Section("chapter", "Chapter", content => content.Text("Chapter body"),
                section => section.StartOnNewPage());
        }));
        PdfDocument withoutPriorContent = PdfDocument.Create(descriptor => descriptor.Page(page =>
            page.Content().Section("chapter", "Chapter", content => content.Text("Chapter body"),
                section => section.StartOnNewPage())));

        withPriorContent.Pages.Should().HaveCount(2);
        withoutPriorContent.Pages.Should().ContainSingle("a leading start-on-new-page section must not create a blank page");
    }

    [Fact]
    public void InternalAndExternalLinks_WriteSafeAnnotationsAcrossPages()
    {
        PdfDocument document = PdfDocument.Create(descriptor =>
        {
            descriptor.Page(page =>
            {
                page.Content().InternalLink("Jump to target", "target").Underline();
                page.Content().ExternalLink("Web", "https://example.com/path?q=(safe)").Underline();
                page.Content().ExternalLink("Email", "mailto:docs@example.com").Underline();
            });
            descriptor.Page(page => page.Content().Anchor("target").Text("Target page"));
        });
        document.OutputOptions.ReadableContentStreams = true;

        string pdf = Encoding.Latin1.GetString(document.GenerateBytes());

        pdf.Should().Contain("/Subtype /Link").And.Contain("/Dest [");
        pdf.Should().Contain("/URI (https://example.com/path?q=\\(safe\\))");
        pdf.Should().Contain("/URI (mailto:docs@example.com)");
        NormalizeWhitespace(ExtractText(Encoding.Latin1.GetBytes(pdf)))
            .Should().Contain("Jump to target").And.Contain("Target page");
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("file:///tmp/report.pdf")]
    [InlineData("data:text/plain,unsafe")]
    public void ExternalLinks_RejectUnsafeSchemes(string uri)
    {
        Action compose = () => PdfDocument.Create(descriptor => descriptor.Page(page =>
            page.Content().ExternalLink("unsafe", uri)));

        compose.Should().Throw<PdfNavigationException>().WithMessage("*not allowed*");
    }

    [Fact]
    public void BrokenInternalLinks_ProduceDiagnosticsAndNoDeadAnnotation()
    {
        PdfDocument document = PdfDocument.Create(descriptor => descriptor.Page(page =>
        {
            page.Content().InternalLink("Broken", "missing");
            page.Content().PageReference("missing", "p. {0}", "pending");
        }));

        string pdf = Encoding.Latin1.GetString(document.GenerateBytes());

        document.NavigationDiagnostics.Entries.Should().ContainSingle();
        document.NavigationDiagnostics.Entries[0].Code.Should().Be("PDFNAV001");
        document.NavigationDiagnostics.Entries[0].Target.Should().Be("missing");
        pdf.Should().NotContain("/Subtype /Link");
        ExtractText(Encoding.Latin1.GetBytes(pdf)).Should().Contain("pending");
    }

    [Fact]
    public void UnicodeOutlineTitlesAndHierarchy_UseCentralEncodingAndNestedParents()
    {
        PdfDocument document = PdfDocument.Create(descriptor => descriptor.Page(page =>
        {
            page.Content().Bookmark("root", "Résumé 日本語", 1);
            page.Content().Text("Root");
            page.Content().Bookmark("child", "子項目", 2);
            page.Content().Text("Child");
        }));
        document.OutputOptions.ReadableContentStreams = true;

        string pdf = Encoding.Latin1.GetString(document.GenerateBytes());

        pdf.Should().Contain("/Type /Outlines");
        pdf.Should().Contain("/Title <FEFF");
        pdf.Should().MatchRegex(@"/First \d+ 0 R\s*/Last \d+ 0 R\s*/Count 1");
    }

    [Fact]
    public void OutlineHierarchy_NormalizesInitialAndSkippedLevelsWithoutFalseNesting()
    {
        PdfDocument document = PdfDocument.Create(descriptor => descriptor.Page(page =>
        {
            page.Content().Bookmark("first", "First", 2);
            page.Content().Text("First body");
            page.Content().Bookmark("second", "Second", 2);
            page.Content().Text("Second body");
            page.Content().Bookmark("nested", "Nested", 4);
            page.Content().Text("Nested body");
        }));

        string pdf = Encoding.Latin1.GetString(document.GenerateBytes());

        pdf.Should().MatchRegex(@"/Type /Outlines\s*/First \d+ 0 R /Last \d+ 0 R /Count 3");
        pdf.Should().Contain("/Title (First)").And.Contain("/Title (Second)").And.Contain("/Title (Nested)");
    }

    [Fact]
    public void Navigation_AfterFlowingTableContinuation_RemainsValid()
    {
        PdfDocument document = PdfDocument.Create(descriptor => descriptor.Page(page =>
        {
            page.Size(new PageSize(300, 260));
            page.Margin(20);
            page.Content().Table(table =>
            {
                table.Columns(columns => columns.RelativeColumn());
                table.Header(row => row.Cell().Text("Header"));
                for (int index = 0; index < 60; index++)
                    table.Row(row => row.Cell().Text($"Row {index}"));
            });
            page.Content().Section("after-table", "After table", section => section.Text("Continuation target"));
            page.Content().InternalLink("Back to target", "after-table");
        }));

        string pdf = Encoding.Latin1.GetString(document.GenerateBytes());

        document.Pages.Should().HaveCountGreaterThan(1);
        document.NavigationDiagnostics.Entries.Should().BeEmpty();
        pdf.Should().Contain("/Subtype /Link").And.Contain("/Outlines");
        NormalizeWhitespace(ExtractText(Encoding.Latin1.GetBytes(pdf)))
            .Should().Contain("Row 59").And.Contain("Continuation target");
    }

    private static string ExtractText(byte[] bytes)
        => string.Join(" ", PdfTextExtractor.ExtractTextBlocks(bytes));

    private static string NormalizeWhitespace(string value)
        => Regex.Replace(value, @"\s+", " ");
}
