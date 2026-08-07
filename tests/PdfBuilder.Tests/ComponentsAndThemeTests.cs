using System;
using System.Collections.Concurrent;
using System.Linq;
using FluentAssertions;
using PdfBuilder.Document;
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
        public void ComponentCycle_ThrowsWithComponentPath()
        {
            Action act = () => PdfDocument.Create(document =>
                document.Page(page => page.Content().Component(new RecursiveComponent())));

            var exception = act.Should().Throw<PdfComponentCompositionException>().Which;
            exception.ComponentPath.Should().Be("RecursiveComponent -> RecursiveComponent");
            exception.Message.Should().Contain("Circular PDF component composition");
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

        private sealed record GreetingModel(string Name);

        private sealed class GreetingTemplate : PdfTemplate<GreetingModel>
        {
            public override void Compose(IDocumentDescriptor document, GreetingModel model)
                => document.Page(page => page.Content().Component(new GreetingComponent(), model.Name));
        }
    }
}
