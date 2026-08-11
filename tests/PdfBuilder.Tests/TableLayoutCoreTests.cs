using FluentAssertions;
using PdfBuilder.Document;
using PdfBuilder.Elements;
using PdfBuilder.Writer;
using Xunit;

namespace PdfBuilder.Tests;

public sealed class TableLayoutCoreTests
{
    [Fact]
    public void Table_SinglePage_RendersCorrectly()
    {
        var document = CreateDocument(3);
        var bytes = PdfContentHelper.Generate(document);

        document.Pages.Should().ContainSingle();
        PdfTextExtractor.ExtractTextBlocks(bytes).Should().Contain(block => block.Contains("row-2", StringComparison.Ordinal));
    }

    [Fact]
    public void Table_MultiplePages_RepeatsHeader()
    {
        var document = CreateDocument(120);

        document.Pages.Count.Should().BeGreaterThan(1);
        document.Pages.Should().OnlyContain(page =>
            page.Elements.OfType<TableElement>().Any(table => table.Rows.Count > 0 && table.Rows[0].IsHeader));
    }

    [Fact]
    public void Table_ContentAfterSplitTable_ContinuesCorrectly()
    {
        var document = CreateDocument(120, includeFollowingText: true);
        var bytes = PdfContentHelper.Generate(document);

        string.Join("\n", PdfTextExtractor.ExtractTextBlocks(bytes)).Should().Contain("After table");
    }

    [Fact]
    public void Table_ThousandRows_NoRowsLostOrDuplicated()
    {
        var document = CreateDocument(1_000);
        var extracted = PdfTextExtractor.ExtractTextBlocks(PdfContentHelper.Generate(document));

        for (var index = 0; index < 1_000; index++)
            extracted.Count(block => string.Equals(block, $"row-{index}", StringComparison.Ordinal)).Should().Be(1);
    }

    [Fact]
    public void Table_ContinuationPreservesColumnWidths()
    {
        var document = CreateDocument(120);
        var tables = document.Pages.SelectMany(page => page.Elements).OfType<TableElement>().ToList();

        tables.Should().HaveCountGreaterThan(1);
        tables.Select(table => table.TableWidth).Should().OnlyContain(width => width.HasValue && Math.Abs(width.Value - tables[0].TableWidth!.Value) < 0.01f);
    }

    [Fact]
    public void Table_NearPageBottom_MovesOrSplitsCorrectly()
    {
        var document = PdfDocument.Create(document => document.Page(page =>
        {
            page.Margin(40);
            page.Content().Column(column =>
            {
                for (var index = 0; index < 34; index++)
                    column.Item().Text($"leading-{index}");
                column.Item().Table(table =>
                {
                    table.Columns(columns => columns.RelativeColumn());
                    for (var index = 0; index < 25; index++)
                        table.Row(row => row.Cell().Text($"near-bottom-{index}"));
                });
            });
        }));

        document.Pages.Count.Should().BeGreaterThan(1);
        string text = string.Join("\n", PdfTextExtractor.ExtractTextBlocks(PdfContentHelper.Generate(document)));
        text.Should().Contain("near-bottom-24");
    }

    [Fact]
    public void Table_EmptyTable_RendersDefinedEmptyState()
    {
        var document = PdfDocument.Create(document => document.Page(page =>
        {
            page.Content().Table(table => table.Columns(columns => columns.RelativeColumn()));
        }));

        document.Invoking(value => PdfContentHelper.Generate(value)).Should().NotThrow();
    }

    [Fact]
    public void Table_HeaderOnly_DoesNotLoop()
    {
        var document = PdfDocument.Create(document => document.Page(page =>
        {
            page.Content().Table(table =>
            {
                table.Columns(columns => columns.RelativeColumn());
                table.Header(row => row.Cell().Text("Only header").Bold());
            });
        }));

        document.Pages.Should().ContainSingle();
        string.Join("\n", PdfTextExtractor.ExtractTextBlocks(PdfContentHelper.Generate(document))).Should().Contain("Only header");
    }

    [Fact]
    public void Table_OversizedRow_ThrowsOrSplitsAccordingToPolicy()
    {
        Action action = () => PdfDocument.Create(document => document.Page(page =>
        {
            page.Margin(40);
            page.Content().Table(table =>
            {
                table.Columns(columns => columns.RelativeColumn());
                table.Row(row => row.Cell().Text(string.Join("\n", Enumerable.Repeat("oversized", 1_000))));
            });
        }));

        action.Should().Throw<InvalidOperationException>().WithMessage("*larger than the available page height*");
    }

    [Fact]
    public void Writer_DoesNotInvokeLegacyTablePaginator()
    {
        var source = File.ReadAllText(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "Writer", "PdfWriter.cs")));
        source.Should().NotContain("TablePaginator.Paginate");
    }

    private static PdfDocument CreateDocument(int rows, bool includeFollowingText = false)
    {
        return PdfDocument.Create(document => document.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(40);
            page.Content().Column(column =>
            {
                column.Item().Table(table =>
                {
                    table.CellPadding(3);
                    table.Border(0.5f, "#000000");
                    table.HeaderBackground("#E8EEF7");
                    table.Columns(columns =>
                    {
                        columns.RelativeColumn();
                        columns.ConstantColumn(80);
                        columns.ConstantColumn(90);
                    });
                    table.Header(header =>
                    {
                        header.Cell().Text("Description").Bold();
                        header.Cell().AlignRight().Text("Quantity").Bold();
                        header.Cell().AlignRight().Text("Amount").Bold();
                    });
                    for (var index = 0; index < rows; index++)
                    {
                        table.Row(row =>
                        {
                            row.Cell().Text($"row-{index}");
                            row.Cell().AlignRight().Text(index, "N0");
                            row.Cell().AlignRight().Text(index + 0.5m, "N2");
                        });
                    }
                });
                if (includeFollowingText)
                    column.Item().Text("After table").Bold();
            });
        }));
    }
}
