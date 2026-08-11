using System.Globalization;
using FluentAssertions;
using PdfBuilder.Document;
using PdfBuilder.Document.Layout;
using PdfBuilder.Writer;
using Xunit;

namespace PdfBuilder.Tests;

public sealed class GraphicsPrimitivesTests
{
    [Fact]
    public void CanonicalCanvas_UsesAvailableSizeAndStableLayerOrder()
    {
        CanvasSize observed = default;
        PdfDocument document = PdfDocument.Create(descriptor =>
        {
            descriptor.Theme(theme => theme
                .Color("Back", "#FF0000")
                .Color("Middle", "#00FF00")
                .Color("Front", "#0000FF"));
            descriptor.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(40);
                page.Content().Canvas(90, (canvas, size) =>
                {
                    observed = size;
                    canvas.Layer(CanvasLayer.Foreground, layer => layer.FillColor("Front").Rectangle(4, 4, 20, 20, stroke: false, fill: true));
                    canvas.Layer(CanvasLayer.Background, layer => layer.FillColor("Back").Rectangle(0, 0, size.Width, size.Height, stroke: false, fill: true));
                    canvas.Layer(CanvasLayer.Content, layer => layer.FillColor("Middle").Rectangle(2, 2, 20, 20, stroke: false, fill: true));
                });
            });
        });

        string stream = PdfContentHelper.ExtractFirstStream(new PdfWriter().GenerateBytes(document));

        observed.Width.Should().BeApproximately(PageSizes.A4.Width - 80, 0.01f);
        observed.Height.Should().Be(90);
        stream.Should().Contain("1 0 0 1 40 704 cm", "flow layout supplies the canvas bottom edge in PDF coordinates");
        stream.IndexOf("1 0 0 rg", StringComparison.Ordinal).Should().BeLessThan(stream.IndexOf("0 1 0 rg", StringComparison.Ordinal));
        stream.IndexOf("0 1 0 rg", StringComparison.Ordinal).Should().BeLessThan(stream.IndexOf("0 0 1 rg", StringComparison.Ordinal));
    }

    [Fact]
    public void CanonicalCanvas_TransformsClipAndLinePatterns_AreRecordedInCallOrder()
    {
        PdfDocument document = PdfDocument.Create(descriptor => descriptor.Page(page => page.Content().Canvas(180, 80, canvas =>
        {
            canvas.State(state => state
                .Translate(10, 20)
                .Rotate(90)
                .Scale(2, 3)
                .ClipRectangle(0, 0, 40, 30)
                .StrokeColor("#123456")
                .LineWidth(2)
                .LinePattern(CanvasLinePattern.Dashed, 4, 2)
                .Line(0, 5, 80, 5));
            canvas.LinePattern(CanvasLinePattern.Dotted, gapLength: 3).Line(0, 12, 80, 12);
            canvas.State(state => state.FlipHorizontal().FlipVertical().Circle(20, 20, 8));
        })));

        string stream = PdfContentHelper.ExtractFirstStream(new PdfWriter().GenerateBytes(document));
        int translate = stream.IndexOf("1 0 0 1 10 20 cm", StringComparison.Ordinal);
        int rotate = stream.IndexOf("0 1 -1 0 0 0 cm", StringComparison.Ordinal);
        int scale = stream.IndexOf("2 0 0 3 0 0 cm", StringComparison.Ordinal);

        translate.Should().BeGreaterThanOrEqualTo(0);
        rotate.Should().BeGreaterThan(translate);
        scale.Should().BeGreaterThan(rotate);
        stream.Should().Contain("0 0 40 30 re W n");
        stream.Should().Contain("[4 2] 0 d");
        stream.Should().Contain("[0.01 3] 0 d");
        stream.Should().Contain("-1 0 0 1 180 0 cm");
        stream.Should().Contain("1 0 0 -1 0 80 cm");
    }

    [Fact]
    public void CanonicalCanvas_GradientsAndShadow_AreBoundedVectorContent()
    {
        PdfDocument document = PdfDocument.Create(descriptor => descriptor.Page(page => page.Content().Canvas(240, 100, canvas =>
        {
            canvas.LinearGradient(0, 0, 100, 40, "#112233", "#88AACC", 30, 12);
            canvas.RadialGradient(145, 22, 20, "#FFFFFF", "#225588", 10);
            canvas.RectangleShadow(180, 8, 40, 28, "#333333", blurRadius: 3, steps: 6);
            canvas.FillColor("#FFFFFF").Rectangle(180, 8, 40, 28, stroke: false, fill: true);
        })));

        string stream = PdfContentHelper.ExtractFirstStream(new PdfWriter().GenerateBytes(document));

        stream.Should().Contain("W n");
        stream.Count(character => character == 'f').Should().BeGreaterThan(20);
        stream.Should().NotContain("/Shading");
    }

    [Fact]
    public void CanonicalCanvas_InvalidOrUnbalancedTransforms_FailClearly()
    {
        Action nonFinite = () => PdfDocument.Create(descriptor => descriptor.Page(page =>
            page.Content().Canvas(100, 40, canvas => canvas.Transform(float.NaN, 0, 0, 1, 0, 0))));
        Action unbalanced = () => PdfDocument.Create(descriptor => descriptor.Page(page =>
            page.Content().Canvas(100, 40, canvas => canvas.Save().Line(0, 0, 10, 10))));

        nonFinite.Should().Throw<ArgumentOutOfRangeException>().WithMessage("*finite*");
        unbalanced.Should().Throw<PdfDrawingException>().WithMessage("*unbalanced graphics-state*");
    }

    [Fact]
    public void CanonicalCanvas_RenderLimitsRejectExcessiveEffectsAndCommands()
    {
        Action excessiveEffect = () => PdfDocument.Create(descriptor =>
        {
            descriptor.RenderLimits(limits => limits.MaximumCanvasEffectSteps = 4);
            descriptor.Page(page => page.Content().Canvas(100, 40, canvas =>
                canvas.LinearGradient(0, 0, 100, 40, "#000000", "#FFFFFF", steps: 5)));
        });
        Action excessiveCommands = () => PdfDocument.Create(descriptor =>
        {
            descriptor.RenderLimits(limits => limits.MaximumCanvasCommands = 3);
            descriptor.Page(page => page.Content().Canvas(100, 40, canvas =>
            {
                canvas.Line(0, 1, 10, 1);
                canvas.Line(0, 2, 10, 2);
            }));
        });
        Action excessiveSvg = () => PdfDocument.Create(descriptor =>
        {
            descriptor.RenderLimits(limits => limits.MaximumSvgBytes = 16);
            descriptor.Page(page => page.Content().DynamicSvg(20, _ =>
                "<svg xmlns='http://www.w3.org/2000/svg'><rect width='10' height='10'/></svg>"));
        });

        excessiveEffect.Should().Throw<PdfRenderLimitException>().Which.LimitName.Should().Be(nameof(PdfRenderLimits.MaximumCanvasEffectSteps));
        excessiveCommands.Should().Throw<PdfRenderLimitException>().Which.LimitName.Should().Be(nameof(PdfRenderLimits.MaximumCanvasCommands));
        excessiveSvg.Should().Throw<PdfRenderLimitException>().Which.LimitName.Should().Be(nameof(PdfRenderLimits.MaximumSvgBytes));
    }

    [Fact]
    public void DynamicSvg_UsesFinalAvailableSizeAndRetainsSanitisation()
    {
        CanvasSize observed = default;
        PdfDocument document = PdfDocument.Create(descriptor => descriptor.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(36);
            page.Content().DynamicSvg(42, size =>
            {
                observed = size;
                return string.Create(CultureInfo.InvariantCulture, $"<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 {size.Width} {size.Height}'><rect width='{size.Width}' height='{size.Height}' fill='#336699'/></svg>");
            });
        }));

        byte[] pdf = new PdfWriter().GenerateBytes(document);

        observed.Width.Should().BeApproximately(PageSizes.A4.Width - 72, 0.01f);
        observed.Height.Should().Be(42);
        System.Text.Encoding.ASCII.GetString(pdf).Should().Contain("/Subtype /Image");

        Action unsafeSvg = () => PdfDocument.Create(descriptor => descriptor.Page(page =>
            page.Content().DynamicSvg(20, _ => "<svg xmlns='http://www.w3.org/2000/svg' onload='alert(1)'/>")));
        unsafeSvg.Should().Throw<PdfMediaException>();
    }

    [Fact]
    public async Task DynamicCanvas_ParallelDocumentsRemainDeterministic()
    {
        static byte[] Generate()
        {
            PdfDocument document = PdfDocument.Create(descriptor => descriptor.Page(page =>
                page.Content().Canvas(64, (canvas, size) => canvas
                    .LinearGradient(0, 0, size.Width, size.Height, "#102030", "#8090A0", steps: 8)
                    .StrokeColor("#000000")
                    .LinePattern(CanvasLinePattern.Dotted)
                    .Line(0, size.Height / 2, size.Width, size.Height / 2))));
            document.GenerationOptions.Deterministic = true;
            document.GenerationOptions.CreationTime = DateTimeOffset.UnixEpoch;
            return new PdfWriter().GenerateBytes(document);
        }

        byte[][] outputs = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(Generate)));
        outputs.Skip(1).Should().OnlyContain(bytes => bytes.SequenceEqual(outputs[0]));
    }
}
