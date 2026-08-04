using System.Text;
using System.Text.RegularExpressions;
using FluentAssertions;
using PdfBuilder.Document;
using PdfBuilder.Elements;
using PdfBuilder.Writer;
using Xunit;

namespace PdfBuilder.Tests;

public sealed class StreamingResourceTests
{
    [Fact]
    public void StreamGeneration_DoesNotBufferAllPageContent()
    {
        var document = CreateTextDocument(pageCount: 40);
        var writer = new PdfWriter();
        using var stream = new MemoryStream();

        writer.GenerateStream(document, stream);

        writer.LastGenerationMetrics!.PagesPlanned.Should().Be(40);
        writer.LastGenerationMetrics.PageContentStreamsWritten.Should().Be(40);
        writer.LastGenerationMetrics.MaximumRetainedPageContentStreams.Should().Be(1);
        stream.Length.Should().BeGreaterThan(0);
    }

    [Fact]
    public void RepeatedImage_IsEmbeddedOnce()
    {
        byte[] logo = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "TestLogo.png"));
        var document = new PdfDocument();
        document.OutputOptions.CompressContentStreams = false;

        for (int pageIndex = 0; pageIndex < 3; pageIndex++)
        {
            var page = document.AddPage();
            page.AddElement(new ImageElement(logo, 72, 600, 80, 40));
        }

        string pdf = Encoding.ASCII.GetString(new PdfWriter().GenerateBytes(document));
        Regex.Matches(pdf, @"/Im\d+\s+\d+\s+0\s+R")
            .Select(match => Regex.Match(match.Value, @"/Im(\d+)").Groups[1].Value)
            .Distinct(StringComparer.Ordinal)
            .Should().ContainSingle();
    }

    [Fact]
    public void RepeatedFont_IsNotDuplicatedUnnecessarily()
    {
        var document = CreateTextDocument(pageCount: 4);
        string pdf = Encoding.ASCII.GetString(new PdfWriter().GenerateBytes(document));

        Regex.Matches(pdf, @"/Type /Font /Subtype /Type1 /BaseFont /Helvetica").Should().ContainSingle();
    }

    [Fact]
    public void ParallelGeneration_DoesNotCrossContaminateResources()
    {
        var outputs = new byte[50][];
        Parallel.For(0, outputs.Length, index =>
        {
            var document = CreateTextDocument(1, $"document-{index}");
            outputs[index] = new PdfWriter().GenerateBytes(document);
        });

        for (int index = 0; index < outputs.Length; index++)
            PdfTextExtractor.ExtractTextBlocks(outputs[index]).Should().Contain($"document-{index}");
    }

    [Fact]
    public void ParallelGeneration_DoesNotThrowNativeLifetimeErrors()
    {
        Action generate = () => Parallel.For(0, 50, index =>
        {
            var document = CreateTextDocument(1, $"parallel-{index}");
            new PdfWriter().GenerateBytes(document).Should().NotBeEmpty();
        });

        generate.Should().NotThrow();
    }

    [Fact]
    public void Cancellation_StopsLargeDocumentGeneration()
    {
        var document = CreateTextDocument(pageCount: 100);
        using var cancellation = new CancellationTokenSource();
        using var destination = new CancellingStream(cancellation);

        Action generate = () => new PdfWriter().GenerateStream(document, destination, cancellation.Token);

        generate.Should().Throw<OperationCanceledException>();
    }

    [Fact]
    public void GenerateBytesAndStream_AreStructurallyEquivalent()
    {
        var document = CreateTextDocument(3);
        document.Metadata.CreatedUtc = new DateTime(2026, 8, 4, 0, 0, 0, DateTimeKind.Utc);
        var writer = new PdfWriter();
        byte[] bytes = writer.GenerateBytes(document);
        using var stream = new MemoryStream();
        writer.GenerateStream(document, stream);

        Encoding.ASCII.GetString(stream.ToArray()).Should().Contain("/Type /Catalog");
        stream.ToArray().Should().Equal(bytes);
    }

    [Fact]
    public void DeterministicMode_RemainsDeterministicAfterStreamingRefactor()
    {
        var document = CreateTextDocument(2);
        document.Metadata.CreatedUtc = new DateTime(2026, 8, 4, 0, 0, 0, DateTimeKind.Utc);

        var first = new PdfWriter().GenerateBytes(document);
        var second = new PdfWriter().GenerateBytes(document);

        second.Should().Equal(first);
    }

    private static PdfDocument CreateTextDocument(int pageCount, string? text = null)
    {
        var document = new PdfDocument();
        document.OutputOptions.CompressContentStreams = false;
        for (int pageIndex = 0; pageIndex < pageCount; pageIndex++)
            document.AddPage().AddElement(new TextElement(text ?? $"page-{pageIndex}", 72, 720) { FontFamily = "Helvetica", MaxWidth = 300 });
        return document;
    }

    private sealed class CancellingStream : MemoryStream
    {
        private readonly CancellationTokenSource _cancellation;
        private bool _hasCancelled;

        public CancellingStream(CancellationTokenSource cancellation) => _cancellation = cancellation;

        public override void Write(byte[] buffer, int offset, int count)
        {
            base.Write(buffer, offset, count);
            if (!_hasCancelled)
            {
                _hasCancelled = true;
                _cancellation.Cancel();
            }
        }
    }
}
