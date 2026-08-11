using System.Text;
using FluentAssertions;
using PdfBuilder.Document;
using Xunit;

namespace PdfBuilder.Tests;

public sealed class TaggedPdfTests
{
    [Fact]
    public void TaggedDocument_WritesMarkedContentStructureTreeRoleMapAndArtifacts()
    {
        PdfDocument document = CreateTaggedDocument();
        document.OutputOptions.ReadableContentStreams = true;

        string pdf = Encoding.Latin1.GetString(document.GenerateBytes());

        pdf.Should().Contain("/MarkInfo << /Marked true >>")
            .And.Contain("/StructTreeRoot")
            .And.Contain("/ParentTree")
            .And.Contain("/RoleMap << /Footer /Sect /Header /Sect >>")
            .And.Contain("/StructParents 0")
            .And.Contain("/S /Document")
            .And.Contain("/S /Sect")
            .And.Contain("/S /H1")
            .And.Contain("/S /P")
            .And.Contain("/S /L")
            .And.Contain("/S /LI")
            .And.Contain("/S /Table")
            .And.Contain("/S /TH")
            .And.Contain("/S /TD")
            .And.Contain("/S /Figure")
            .And.Contain("/Alt (Quarterly revenue chart)")
            .And.Contain("/Artifact BMC")
            .And.Contain("<</MCID 0>> BDC");
    }

    [Fact]
    public void TaggedLink_AssociatesStructureElementWithAnnotation()
    {
        PdfDocument document = PdfDocument.Create(descriptor =>
        {
            descriptor.Tagged(tagged => tagged.Language("en-ZA"));
            descriptor.Output(output => output.ReadableContentStreams = true);
            descriptor.Page(page => page.Content().ExternalLink("Open example", "https://example.com"));
        });

        string pdf = Encoding.Latin1.GetString(document.GenerateBytes());

        pdf.Should().Contain("/S /Link")
            .And.Contain("/Subtype /Link")
            .And.Contain("/StructParent ")
            .And.Contain("/Type /OBJR")
            .And.Contain("/Obj ");
    }

    [Fact]
    public void TaggedDocument_RequiresLanguage()
    {
        PdfDocument document = PdfDocument.Create(descriptor =>
        {
            descriptor.Tagged(tagged => tagged.Enabled());
            descriptor.Page(page => page.Content().Text("Missing language"));
        });

        document.Invoking(value => value.GenerateBytes())
            .Should().Throw<InvalidOperationException>()
            .WithMessage("*language*tagged*");
    }

    [Fact]
    public void UntaggedDocument_DoesNotChangeLegacyCatalogOrContent()
    {
        PdfDocument document = PdfDocument.Create(descriptor =>
            descriptor.Page(page => page.Content().Text("Legacy output")));
        document.OutputOptions.ReadableContentStreams = true;

        string pdf = Encoding.Latin1.GetString(document.GenerateBytes());

        pdf.Should().NotContain("/StructTreeRoot")
            .And.NotContain("/MarkInfo")
            .And.NotContain("/MCID");
    }

    [Fact]
    public void TaggedDocument_IsDeterministicAndExtractableAcrossConcurrentGeneration()
    {
        PdfDocument document = CreateTaggedDocument();
        document.GenerationOptions.Deterministic = true;
        document.GenerationOptions.DocumentIdSeed = "tagged-fixture";

        byte[][] outputs = Enumerable.Range(0, 8)
            .AsParallel()
            .Select(_ => document.GenerateBytes())
            .ToArray();

        outputs.Skip(1).Should().OnlyContain(output => output.SequenceEqual(outputs[0]));
        string extracted = string.Join(" ", PdfTextExtractor.ExtractTextBlocks(outputs[0]));
        string normalized = string.Join(" ", extracted.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        normalized.Should().Contain("Tagged report").And.Contain("Open example");
    }

    private static PdfDocument CreateTaggedDocument()
        => PdfDocument.Create(descriptor =>
        {
            descriptor.Tagged(tagged => tagged.Language("en-ZA"));
            descriptor.Page(page =>
            {
                page.Background().Decorative().Background("#F6F8FA");
                page.Header().Text("Tagged report header");
                page.Footer().Text("Tagged report footer");
                page.Content().Semantic(PdfSemanticRole.Section).Column(column =>
                {
                    column.Item().Heading(1).Text("Tagged report");
                    column.Item().Semantic(PdfSemanticRole.Paragraph).Text("Semantic paragraph");
                    column.Item().Semantic(PdfSemanticRole.List).Column(list =>
                    {
                        list.Item().Semantic(PdfSemanticRole.ListItem).Text("First item");
                        list.Item().Semantic(PdfSemanticRole.ListItem).Text("Second item");
                    });
                    column.Item().Table(table =>
                    {
                        table.Columns(columns => columns.RelativeColumn());
                        table.Header(row => row.Cell().Semantic(PdfSemanticRole.TableHeaderCell).Text("Heading"));
                        table.Row(row => row.Cell().Semantic(PdfSemanticRole.TableCell).Text("Value"));
                    });
                    column.Item().Semantic(PdfSemanticRole.Figure)
                        .AlternativeText("Quarterly revenue chart")
                        .Canvas(120, 30, canvas => canvas.Line(0, 15, 120, 15));
                    column.Item().Semantic(PdfSemanticRole.Caption).Text("Figure 1");
                    column.Item().ExternalLink("Open example", "https://example.com");
                    column.Item().Decorative().Text("Decorative marker");
                });
            });
        });
}
