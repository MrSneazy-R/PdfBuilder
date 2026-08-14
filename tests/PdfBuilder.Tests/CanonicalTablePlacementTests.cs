using System.Drawing;
using FluentAssertions;
using PdfBuilder.Document;
using PdfBuilder.Document.Layout;
using PdfBuilder.Elements;
using PdfBuilder.Elements.Table;
using PdfBuilder.Models;
using Xunit;

namespace PdfBuilder.Tests;

public sealed class CanonicalTablePlacementTests
{
    [Fact]
    public void Table_ExplicitRowAndColumnPlacement_WithSpans_NormalizesBeforeOutput()
    {
        var document = PdfDocument.Create(descriptor => descriptor.Page(page => page.Content().Table(table =>
        {
            table.Columns(columns =>
            {
                columns.FixedColumn(70);
                columns.RelativeColumn(1, 50, 180);
                columns.AutoColumn(40, 120);
            });
            table.Row(row =>
            {
                row.Position(1);
                row.Cell().Position(1).Text("row-1-col-1");
                row.Cell().Position(2).Text("row-1-col-2");
            });
            table.Row(row =>
            {
                row.Position(0);
                row.Cell().Position(0).RowSpan(2).Text("row-span");
                row.Cell().Position(1).ColumnSpan(2).Text("column-span");
            });
        })));

        document.GenerateBytes().Should().NotBeEmpty();
        TableRow[] rendered = document.Pages.SelectMany(page => page.Elements).OfType<TableSegmentElement>().Single().Rows.Select(row => row.Row).ToArray();

        rendered.Should().HaveCount(2);
        rendered[0].Cells.Should().Contain(cell => cell.Text == "row-span" && cell.RowSpan == 2);
        rendered[0].Cells.Should().Contain(cell => cell.Text == "column-span" && cell.ColSpan == 2);
        rendered[1].Cells.Select(cell => cell.Text).Should().ContainInOrder("row-1-col-1", "row-1-col-2");
    }

    [Fact]
    public void Table_ExplicitOverlap_FailsBeforePdfOutput()
    {
        Action create = () => PdfDocument.Create(descriptor => descriptor.Page(page => page.Content().Table(table =>
        {
            table.Columns(columns =>
            {
                columns.RelativeColumn();
                columns.RelativeColumn();
                columns.RelativeColumn();
            });
            table.Row(row =>
            {
                row.Cell().Position(0).ColumnSpan(2).Text("wide");
                row.Cell().Position(1).Text("overlap");
            });
        })));

        create.Should().Throw<InvalidOperationException>()
            .WithMessage("*overlap*row 0, column 1*");
    }

    [Fact]
    public void Table_RowSpanAcrossGroups_FailsExplicitly()
    {
        Action create = () => PdfDocument.Create(descriptor => descriptor.Page(page => page.Content().Table(table =>
        {
            table.Columns(columns => columns.RelativeColumn());
            table.Header(row => row.Cell().RowSpan(2).Text("invalid header span"));
            table.Row(row => row.Cell().Text("body"));
        })));

        create.Should().Throw<InvalidOperationException>()
            .WithMessage("*cannot cross header, body, or footer group boundaries*");
    }

    [Theory]
    [InlineData(TableFooterRepeatMode.Never)]
    [InlineData(TableFooterRepeatMode.EveryPage)]
    [InlineData(TableFooterRepeatMode.ContinuationPages)]
    public void Table_FooterRepeatMode_HasExplicitSegmentSemantics(TableFooterRepeatMode mode)
    {
        PdfDocument document = CreateMultipageTable(mode, 32);
        byte[] bytes = document.GenerateBytes();
        int footerOccurrences = PdfTextExtractor.ExtractTextBlocks(bytes).Count(block => block == "table footer");

        document.Pages.Count.Should().BeGreaterThan(1);
        int expected = mode switch
        {
            TableFooterRepeatMode.Never => 1,
            TableFooterRepeatMode.EveryPage => document.Pages.Count,
            TableFooterRepeatMode.ContinuationPages => document.Pages.Count - 1,
            _ => throw new ArgumentOutOfRangeException(nameof(mode))
        };
        footerOccurrences.Should().Be(expected);
    }

    [Fact]
    public void Table_ContinuationWidthsAndBandIndices_AreStable()
    {
        PdfDocument document = CreateMultipageTable(TableFooterRepeatMode.EveryPage, 80, configureTable: table =>
        {
            table.RowBanding(banding =>
            {
                banding.Step(1);
                banding.Fill("#FFFFFF");
                banding.Fill("#EEF3F8");
            });
            table.ColumnBanding(banding =>
            {
                banding.Step(1);
                banding.Fill("#FFFFFF");
                banding.Fill("#F8F8F8");
            });
        }, threeColumns: true);

        document.GenerateBytes().Should().NotBeEmpty();
        List<TableSegmentElement> segments = document.Pages.SelectMany(page => page.Elements).OfType<TableSegmentElement>().ToList();
        segments.Should().HaveCount(document.Pages.Count);
        segments.Should().OnlyContain(segment => segment.ColumnWidths.SequenceEqual(segments[0].ColumnWidths));

        int[] indices = segments.SelectMany(segment => segment.Rows)
            .Where(row => !row.Row.IsHeader && !row.Row.IsFooter)
            .Select(row => row.BodyIndex)
            .ToArray();
        indices.Should().Equal(Enumerable.Range(0, 80));
    }

    [Fact]
    public void Table_BordersBandingCornersAlignmentAndOverflow_MapToCanonicalModels()
    {
        var document = PdfDocument.Create(descriptor =>
        {
            descriptor.Theme(theme => theme.Color("Rule", "#345678").Color("Band", "#EDF2F7"));
            descriptor.Page(page => page.Content().Table(table =>
            {
                table.Columns(columns => columns.RelativeColumn());
                table.BorderCollapse(TableBorderCollapseMode.Collapse);
                table.CornerRadius(5);
                table.OuterBorder(border => { border.Color("Rule"); border.Width(2); border.LineJoin(TableBorderLineJoin.Round); });
                table.InnerBorder(border => { border.Color("Rule"); border.Width(0.5f); border.DashPattern(2, 1); });
                table.RowBanding(banding => { banding.Step(1); banding.Fill("Band"); });
                table.Row(row => row.Background("Band").Height(36).Cell()
                    .AlignBottom()
                    .CornerRadius(3)
                    .BorderTop(border => { border.Color("Rule"); border.Width(1.5f); border.LineCap(TableBorderLineCap.Round); })
                    .NoWrap()
                    .Ellipsis()
                    .Text("non-wrapping table text"));
            }));
        });

        document.GenerateBytes().Should().NotBeEmpty();
        TableSegmentElement segment = document.Pages.SelectMany(page => page.Elements).OfType<TableSegmentElement>().Single();
        TableElement table = segment.SourceTable;
        TableCell cell = segment.Rows.Single().Row.Cells.Single();
        TextElement text = PdfContentHelper.FlattenElements(document.Pages.SelectMany(page => page.Elements)).OfType<TextElement>().Single();

        table.BorderCollapse.Should().Be(BorderCollapseMode.Collapse);
        table.OuterBorder!.Color.Should().Be(Color.FromArgb(0x34, 0x56, 0x78));
        table.OuterBorder.Width.Should().Be(2);
        table.InnerBorder!.DashPattern.Should().Equal(2, 1);
        table.OuterCornerRadiusTopLeft.Should().Be(5);
        cell.VerticalAlign.Should().Be(VerticalAlign.Bottom);
        cell.BorderStyleTop!.LineCap.Should().Be(BorderLineCap.Round);
        text.Wrapping.Should().Be(TextWrapping.NoWrap);
        text.EllipsisWhenConstrained.Should().BeTrue();
    }

    [Fact]
    public void Table_CellTextConvenience_ExposesWrapNoWrapAndHyphenation()
    {
        var document = PdfDocument.Create(descriptor => descriptor.Page(page => page.Content().Table(table =>
        {
            table.Columns(columns => columns.RelativeColumn());
            table.Row(row => row.Cell().Wrap().Text("wrap"));
            table.Row(row => row.Cell().NoWrap().Text("no-wrap"));
            table.Row(row => row.Cell().Hyphenate().Text("hyphenate"));
        })));

        document.GenerateBytes().Should().NotBeEmpty();
        Dictionary<string, TextWrapping> wrapping = PdfContentHelper
            .FlattenElements(document.Pages.SelectMany(page => page.Elements))
            .OfType<TextElement>()
            .ToDictionary(text => text.Text, text => text.Wrapping);

        wrapping["wrap"].Should().Be(TextWrapping.Wrap);
        wrapping["no-wrap"].Should().Be(TextWrapping.NoWrap);
        wrapping["hyphenate"].Should().Be(TextWrapping.Hyphenate);
    }

    [Fact]
    public void Table_WidowOrphanAndKeepWithNext_ChooseValidBreaks()
    {
        var document = PdfDocument.Create(descriptor => descriptor.Page(page =>
        {
            page.Size(new PageSize(260, 220));
            page.Margin(20);
            page.Content().Table(table =>
            {
                table.Columns(columns => columns.RelativeColumn());
                table.WidowOrphanRows(2, 2);
                table.Header(row => row.Height(20).Cell().Text("header"));
                for (int index = 0; index < 12; index++)
                {
                    int captured = index;
                    table.Row(row =>
                    {
                        row.Height(26);
                        if (captured == 5) row.KeepWithNext();
                        row.Cell().Text($"controlled-{captured}");
                    });
                }
            });
        }));

        document.GenerateBytes().Should().NotBeEmpty();
        List<List<TableRow>> pageRows = document.Pages
            .Select(page => page.Elements.OfType<TableSegmentElement>().Single().Rows.Select(layout => layout.Row).Where(row => !row.IsHeader && !row.IsFooter).ToList())
            .ToList();

        pageRows.Should().OnlyContain(rows => rows.Count >= 2);
        pageRows.Should().Contain(rows => rows.Any(row => row.Cells.Any(cell => cell.Text == "controlled-5"))
            && rows.Any(row => row.Cells.Any(cell => cell.Text == "controlled-6")));
    }

    private static PdfDocument CreateMultipageTable(
        TableFooterRepeatMode mode,
        int rowCount,
        Action<ITableDescriptor>? configureTable = null,
        bool threeColumns = false)
        => PdfDocument.Create(descriptor => descriptor.Page(page =>
        {
            page.Size(new PageSize(280, 240));
            page.Margin(20);
            page.Content().Table(table =>
            {
                table.Columns(columns =>
                {
                    if (threeColumns)
                    {
                        columns.AutoColumn(45, 90);
                        columns.RelativeColumn(2, 60, 150);
                        columns.FixedColumn(55, 50, 60);
                    }
                    else
                    {
                        columns.RelativeColumn();
                    }
                });
                table.RepeatHeaders();
                table.RepeatFooters(mode);
                table.Header(row =>
                {
                    row.Cell().Text("table header");
                    if (threeColumns) { row.Cell().Text("description"); row.Cell().Text("amount"); }
                });
                for (int index = 0; index < rowCount; index++)
                {
                    table.Row(row =>
                    {
                        row.Cell().Text($"placement-row-{index}");
                        if (threeColumns) { row.Cell().Text($"description-{index}"); row.Cell().AlignRight().Text(index, "N0"); }
                    });
                }
                table.Footer(row =>
                {
                    row.Cell().Text("table footer");
                    if (threeColumns) row.Cell().Position(2).Text("total");
                });
                configureTable?.Invoke(table);
            });
        }));
}
