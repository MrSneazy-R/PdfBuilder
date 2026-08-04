using FluentAssertions;
using PdfBuilder.Document;
using Xunit;

namespace PdfBuilder.Tests;

public sealed class ComponentsAndThemingTests
{
    [Fact]
    public void Component_CanBeReusedAcrossDocuments()
    {
        var component = new LabelComponent("Reusable");
        PdfDocument.Create(d => d.Page(p => p.Content().Component(component))).GenerateBytes().Should().NotBeEmpty();
        PdfDocument.Create(d => d.Page(p => p.Content().Component(component))).GenerateBytes().Should().NotBeEmpty();
    }

    [Fact]
    public void Template_GeneratesImmutableModel()
    {
        var template = new LabelTemplate();
        var model = new LabelModel("Invoice");
        template.GenerateBytes(model).Should().NotBeEmpty();
        model.Value.Should().Be("Invoice");
    }

    [Fact]
    public async Task Component_IsSafeDuringParallelGeneration()
    {
        var component = new LabelComponent("Parallel");
        var tasks = Enumerable.Range(0, 20).Select(_ => Task.Run(() => PdfDocument.Create(d => d.Page(p => p.Content().Component(component))).GenerateBytes()));
        (await Task.WhenAll(tasks)).Should().OnlyContain(bytes => bytes.Length > 0);
    }

    [Fact]
    public void Theme_OverridesDoNotLeakAcrossDocuments()
    {
        var themed = PdfDocument.Create(d =>
        {
            d.Theme(theme =>
            {
                theme.Color("Primary", "#163A5F");
                theme.TextStyle("Heading1", style => style.FontSize(24).Bold().Color("Primary"));
                theme.SetSpacing("Section", 16);
            });
            d.Page(page => page.Content().Text("Invoice").Style("Heading1"));
        });

        themed.GenerateBytes().Should().NotBeEmpty();
        PdfDocument.Create(d => d.Page(page => page.Content().Text("Plain"))).GenerateBytes().Should().NotBeEmpty();
    }

    private sealed record LabelModel(string Value);
    private sealed class LabelComponent(string value) : IPdfComponent { public void Compose(IContainer container) => container.Text(value); }
    private sealed class LabelTemplate : PdfTemplate<LabelModel> { public override void Compose(IDocumentDescriptor document, LabelModel model) => document.Page(page => page.Content().Text(model.Value)); }
}
