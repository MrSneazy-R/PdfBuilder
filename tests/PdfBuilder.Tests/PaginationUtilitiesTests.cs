using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using FluentAssertions;
using PdfBuilder.Document;
using PdfBuilder.Elements;
using PdfBuilder.Models;
using Xunit;

namespace PdfBuilder.Tests
{
    public class PaginationUtilitiesTests
    {
        [Fact]
        public void SectionRegistration_ProducesHierarchicalNumbers()
        {
            var document = new PdfDocument();
            var page = document.AddPage();

            var column = new ColumnBuilder(
                page,
                margin: 0f,
                layoutOptions: page.LayoutOptions,
                textDefaults: page.TextDefaults,
                columnFactory: null,
                document: document);

            column.Section("Introduction", ctx =>
            {
                column.Text(ctx.TitleWithNumber).Add();
            }, level: 1);

            column.Section("Overview", ctx =>
            {
                column.Text(ctx.TitleWithNumber).Add();
            }, level: 2);

            var sections = document.Pagination.Sections;
            sections.Should().HaveCount(2);
            sections[0].Number.Should().Be("1");
            sections[1].Number.Should().Be("1.1");
        }

        [Fact]
        public void TableOfContents_BindsPageNumbers()
        {
            var document = new PdfDocument();
            var page = document.AddPage();

            var column = new ColumnBuilder(
                page,
                margin: 0f,
                layoutOptions: page.LayoutOptions,
                textDefaults: page.TextDefaults,
                columnFactory: null,
                document: document);

            column.Section("Chapter One", level: 1);
            column.Section("Background", level: 2);
            column.TableOfContents();

            var tocTable = page.Elements.OfType<TableElement>().Last();
            tocTable.Rows.Should().HaveCount(2);

            var lookup = new Dictionary<string, (int pageIndex, float x, float y)>();
            foreach (var section in document.Pagination.Sections)
                lookup[section.AnchorId] = (0, 0f, 0f);

            InvokeApplyPageLookup(document.Pagination, lookup);

            document.Pagination.Sections.All(s => s.PageNumber == 1).Should().BeTrue();
            tocTable.Rows[0].Cells[1].Text.Should().Be("1");
            tocTable.Rows[1].Cells[1].Text.Should().Be("1");
        }

        [Fact]
        public void LayoutProfiler_CollectsTimingData()
        {
            var document = new PdfDocument();
            document.LayoutOptions.Profiler.Enabled = true;

            var page = document.AddPage();
            var column = new ColumnBuilder(
                page,
                margin: 0f,
                layoutOptions: page.LayoutOptions,
                textDefaults: page.TextDefaults,
                columnFactory: null,
                document: document);

            column.Text("Profiler sample").Add();

            var snapshot = document.ProfilerSession.Snapshot();
            snapshot.Entries.Should().NotBeEmpty();
            snapshot.Entries.Sum(e => e.MeasureCount + e.DrawCount).Should().BeGreaterThan(0);
        }

        private static void InvokeApplyPageLookup(PaginationRegistry registry, Dictionary<string, (int pageIndex, float x, float y)> lookup)
        {
            var method = typeof(PaginationRegistry).GetMethod("ApplyPageLookup", BindingFlags.Instance | BindingFlags.NonPublic);
            method!.Invoke(registry, new object[] { lookup });
        }
    }
}
