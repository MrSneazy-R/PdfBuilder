using System;
using System.Collections.Concurrent;
using System.Drawing;
using System.IO;
using System.Linq;
using FluentAssertions;
using PdfBuilder.Document;
using PdfBuilder.Document.Layout;
using PdfBuilder.Elements;
using PdfBuilder.Writer;
using Xunit;

namespace PdfBuilder.Tests
{
    public sealed class ComponentsAndThemeTests
    {
        [Fact]
        public void Component_ReusedAcrossPagesAndDocuments_ComposesIndependently()
        {
            var component = new GreetingComponent();
            var first = CreateDocument(component, "First", pageCount: 2);
            var second = CreateDocument(component, "Second", pageCount: 1);

            string.Join(" ", PdfTextExtractor.ExtractTextBlocks(new PdfWriter().GenerateBytes(first))).Should().Contain("First");
            string.Join(" ", PdfTextExtractor.ExtractTextBlocks(new PdfWriter().GenerateBytes(second))).Should().Contain("Second");
            first.Pages.Should().HaveCount(2);
            second.Pages.Should().HaveCount(1);
        }

        [Fact]
        public void PdfTemplate_ConcurrentGeneration_DoesNotShareDocumentState()
        {
            var template = new GreetingTemplate();
            var failures = new ConcurrentQueue<Exception>();

            Enumerable.Range(0, 16).AsParallel().ForAll(index =>
            {
                try
                {
                    var bytes = template.GenerateBytes(new GreetingModel($"Customer {index}"));
                    string.Join(" ", PdfTextExtractor.ExtractTextBlocks(bytes)).Should().Contain($"Customer {index}");
                }
                catch (Exception exception)
                {
                    failures.Enqueue(exception);
                }
            });

            failures.Should().BeEmpty();
        }

        [Fact]
        public void PdfTemplate_FileSave_GeneratesPdfAndHonorsCancellation()
        {
            var template = new GreetingTemplate();
            string path = Path.Combine(Path.GetTempPath(), $"pdfbuilder-template-{Guid.NewGuid():N}.pdf");

            try
            {
                template.Save(path, new GreetingModel("File customer"));
                File.ReadAllBytes(path).Should().StartWith(new byte[] { (byte)'%', (byte)'P', (byte)'D', (byte)'F' });
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }

            Action cancelled = () => template.Save(path, new GreetingModel("Cancelled"), new CancellationToken(canceled: true));
            cancelled.Should().Throw<OperationCanceledException>();
            File.Exists(path).Should().BeFalse();
        }

        [Fact]
        public void Theme_NamedStyleAndColor_AreResolvedWithoutLeaking()
        {
            var themed = PdfDocument.Create(document =>
            {
                document.Theme(theme =>
                {
                    theme.Color("Primary", "#163A5F");
                    theme.TextStyle("Heading1", style => style.FontSize(24).Bold().Color("Primary"));
                    theme.Spacing("Section", 16);
                });
                document.Page(page => page.Content().Text("Invoice").Style("Heading1"));
            });

            var ordinary = PdfDocument.Create(document =>
                document.Page(page => page.Content().Text("Invoice")));

            var themedText = themed.Pages.Single().Elements.OfType<TextElement>().Single();
            var ordinaryText = ordinary.Pages.Single().Elements.OfType<TextElement>().Single();
            themedText.FontSize.Should().Be(24);
            themedText.Bold.Should().BeTrue();
            themedText.Color.Should().Be("#163A5F");
            themed.Theme.Spacing["Section"].Should().Be(16);
            ordinaryText.FontSize.Should().Be(12);
            ordinaryText.Color.Should().Be("black");
        }

        [Fact]
        public void Theme_SpacingTokens_ResolveInContainersComponentsColumnsAndGrids()
        {
            var document = PdfDocument.Create(descriptor =>
            {
                descriptor.Theme(theme =>
                {
                    theme.Spacing("Section", 14);
                    theme.Spacing("Compact", 1);
                });
                descriptor.Page(page =>
                {
                    page.Header().Padding("Compact").Text("Header");
                    page.Footer().Margin("Compact").Text("Footer");
                    page.Content().Component(new SpacingComponent());
                });
            });

            document.GenerateBytes().Should().NotBeEmpty();

            Action missing = () => PdfDocument.Create(descriptor =>
                descriptor.Page(page => page.Content().Padding("Missing").Text("Failure")));
            missing.Should().Throw<KeyNotFoundException>()
                .WithMessage("Theme spacing 'Missing' is not defined.");
        }

        [Fact]
        public void Theme_NamedColors_ResolveForTextDecorationsTablesHeadersFootersAndPageBackground()
        {
            var document = PdfDocument.Create(descriptor =>
            {
                descriptor.Theme(theme =>
                {
                    theme.Color("Ink", "#123456");
                    theme.Color("Surface", "#E8EEF7");
                    theme.Color("Rule", "#AABBCC");
                    theme.TextStyle("NamedCell", style => style.Bold().Color("Ink"));
                    theme.Page(page => page.BackgroundColor = "Surface");
                });
                descriptor.Page(page =>
                {
                    page.Header().Text("Themed header").Style("NamedCell");
                    page.Footer().Text("Themed footer").Style("NamedCell");
                    page.Background().Background("Surface").Text(string.Empty);
                    page.Content().Background("Surface").Border(1, "Rule").Column(column =>
                    {
                        column.Item().Text("Themed text").Style("NamedCell");
                        column.Item().Table(table =>
                        {
                            table.Columns(columns => columns.RelativeColumn());
                            table.Border(1, "Rule");
                            table.HeaderBackground("Surface");
                            table.Header(row => row.Cell().Background("Surface").Border(1, "Rule").Text("Header cell").Style("NamedCell"));
                            table.Row(row => row.Cell().Background("Surface").Border(1, "Rule").Text("Body cell").Style("NamedCell"));
                        });
                    });
                });
            });

            document.GenerateBytes().Should().NotBeEmpty();
            document.Pages.Should().OnlyContain(page => page.BackgroundColor == "#E8EEF7");
            document.Pages.SelectMany(page => page.Elements).OfType<TextElement>()
                .Where(text => text.Text.Contains("Themed", StringComparison.Ordinal))
                .Should().OnlyContain(text => text.Color == "#123456");

            var segment = document.Pages.SelectMany(page => page.Elements).OfType<TableSegmentElement>().First();
            var table = segment.SourceTable;
            table.BorderColor.Should().Be(Color.FromArgb(0xAA, 0xBB, 0xCC));
            table.HeaderBackground.Should().Be(Color.FromArgb(0xE8, 0xEE, 0xF7));
            segment.Rows.SelectMany(row => row.Row.Cells).Should().OnlyContain(cell =>
                cell.BackgroundColor == Color.FromArgb(0xE8, 0xEE, 0xF7)
                && cell.BorderColor == Color.FromArgb(0xAA, 0xBB, 0xCC));
            PdfContentHelper.FlattenElements(document.Pages.SelectMany(page => page.Elements)).OfType<TextElement>()
                .Where(text => text.Text.Contains("cell", StringComparison.Ordinal))
                .Should().OnlyContain(text => text.Color == "#123456" && text.Bold);
            document.Pages.SelectMany(page => page.Elements).OfType<SolidRectElement>()
                .Should().Contain(rect => rect.FillColor == "#E8EEF7")
                .And.Contain(rect => rect.StrokeColor == "#AABBCC");
        }

        [Fact]
        public void ComponentCycle_ThrowsWithComponentPath()
        {
            Action act = () => PdfDocument.Create(document =>
                document.Page(page => page.Content().Component(new RecursiveComponent())));

            var exception = act.Should().Throw<PdfComponentCompositionException>().Which;
            exception.ComponentPath.Should().Be("RecursiveComponent -> RecursiveComponent");
            exception.Message.Should().Contain("Circular PDF component composition");
        }

        [Fact]
        public void NestedComponentCycle_ThrowsWithCompleteComponentPath()
        {
            Action act = () => PdfDocument.Create(document =>
                document.Page(page => page.Content().Component(new NestedRecursiveComponent())));

            act.Should().Throw<PdfComponentCompositionException>()
                .Which.ComponentPath.Should().Be("NestedRecursiveComponent -> NestedRecursiveComponent");
        }

        [Fact]
        public void NestedComponentFailure_ReportsCompleteComponentPathAndPreservesCause()
        {
            Action act = () => PdfDocument.Create(document =>
                document.Page(page => page.Content().Component(new ParentComponent())));

            var exception = act.Should().Throw<PdfComponentCompositionException>().Which;
            exception.ComponentPath.Should().Be("ParentComponent -> FailingComponent");
            exception.InnerException.Should().BeOfType<InvalidOperationException>()
                .Which.Message.Should().Be("Intentional nested failure.");
        }

        [Fact]
        public void Theme_PageClone_IsolatedAcrossDocumentsAndAutomaticPagination()
        {
            var first = PdfDocument.Create(document =>
            {
                document.Theme(theme =>
                {
                    theme.Color("Primary", "#112233");
                    theme.Page(page => page.BackgroundColor = "Primary");
                });
                document.Page(page => page.Content().Column(column =>
                {
                    for (int index = 0; index < 160; index++) column.Item().Text($"Line {index}");
                }));
            });
            var second = PdfDocument.Create(document => document.Page(page => page.Content().Text("Ordinary")));

            first.Pages.Should().HaveCountGreaterThan(1);
            first.Pages.Should().OnlyContain(page => page.Theme.Colors["Primary"] == "#112233");
            first.Pages[0].Theme.Page.BackgroundColor = "#FFFFFF";
            first.Pages.Skip(1).Should().OnlyContain(page => page.Theme.Page.BackgroundColor == "Primary");
            second.Theme.Colors.Should().BeEmpty();
        }

        private static PdfDocument CreateDocument(IPdfComponent<string> component, string value, int pageCount)
        {
            return PdfDocument.Create(document =>
            {
                for (int index = 0; index < pageCount; index++)
                    document.Page(page => page.Content().Component(component, value));
            });
        }

        private sealed class GreetingComponent : IPdfComponent<string>
        {
            public void Compose(IContainer container, string model) => container.Text($"Hello {model}");
        }

        private sealed class RecursiveComponent : IPdfComponent
        {
            public void Compose(IContainer container) => container.Component(this);
        }

        private sealed class NestedRecursiveComponent : IPdfComponent
        {
            public void Compose(IContainer container) =>
                container.Column(column => column.Item().Component(this));
        }

        private sealed class ParentComponent : IPdfComponent
        {
            public void Compose(IContainer container) =>
                container.Column(column => column.Item().Component(new FailingComponent()));
        }

        private sealed class FailingComponent : IPdfComponent
        {
            public void Compose(IContainer container) => throw new InvalidOperationException("Intentional nested failure.");
        }

        private sealed class SpacingComponent : IPdfComponent
        {
            public void Compose(IContainer container)
            {
                container.Padding("Section").Margin("Compact").Column(column =>
                {
                    column.Spacing("Compact");
                    column.Item().Text("Spacing component");
                    column.Item().Grid(grid =>
                    {
                        grid.Columns(2);
                        grid.RowSpacing("Compact");
                        grid.ColumnSpacing("Section");
                        grid.Item().Text("One");
                        grid.Item().Text("Two");
                    });
                });
            }
        }

        private sealed record GreetingModel(string Name);

        private sealed class GreetingTemplate : PdfTemplate<GreetingModel>
        {
            public override void Compose(IDocumentDescriptor document, GreetingModel model)
                => document.Page(page => page.Content().Component(new GreetingComponent(), model.Name));
        }
    }
}
