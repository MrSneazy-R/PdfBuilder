using System.Collections.Generic;
using System.Reflection;
using System.Text;
using FluentAssertions;
using PdfBuilder.Document;
using PdfBuilder.Document.Layout;
using PdfBuilder.Models;
using PdfBuilder.Writer;
using Xunit;

namespace PdfBuilder.Tests;

public sealed class OutputHardeningTests
{
    [Theory]
    [InlineData(PdfVersion.Pdf14, "%PDF-1.4")]
    [InlineData(PdfVersion.Pdf15, "%PDF-1.5")]
    [InlineData(PdfVersion.Pdf16, "%PDF-1.6")]
    [InlineData(PdfVersion.Pdf17, "%PDF-1.7")]
    [InlineData(PdfVersion.Pdf20, "%PDF-2.0")]
    public void PdfVersion_Selected_WritesExpectedHeader(PdfVersion version, string header)
    {
        PdfDocument document = CreateDocument();
        document.OutputOptions.PdfVersion = version;

        Encoding.Latin1.GetString(new PdfWriter().GenerateBytes(document)).Should().StartWith(header);
    }

    [Fact]
    public void DebugPreset_WritesReadableContent_AndPreservesExplicitOverrides()
    {
        PdfDocument document = CreateDocument();
        document.ApplyOutputPreset(PdfOutputPreset.Debug);
        document.OutputOptions.PdfVersion = PdfVersion.Pdf17;

        string pdf = Encoding.Latin1.GetString(new PdfWriter().GenerateBytes(document));

        pdf.Should().StartWith("%PDF-1.7");
        pdf.Should().NotContain("/FlateDecode");
        document.OutputOptions.ReadableContentStreams.Should().BeTrue();
    }

    [Fact]
    public void NamedPresets_ExposeCoherentImageAndCompressionDefaults()
    {
        var options = new PdfOutputOptions().ApplyPreset(PdfOutputPreset.SmallFile);
        options.CompressContentStreams.Should().BeTrue();
        options.DownsampleImages.Should().BeTrue();
        options.MaximumImageDpi.Should().Be(150f);
        options.JpegQuality.Should().Be(75);

        options.ApplyPreset(PdfOutputPreset.PrintQuality);
        options.DownsampleImages.Should().BeTrue();
        options.MaximumImageDpi.Should().Be(450f);
        options.JpegQuality.Should().Be(95);

        options.ApplyPreset(PdfOutputPreset.Balanced);
        options.DownsampleImages.Should().BeFalse();
        options.UsePngPredictor.Should().BeTrue();
    }

    [Fact]
    public void DeterministicPreset_ProducesIdenticalBytes()
    {
        PdfDocument first = CreateDocument();
        PdfDocument second = CreateDocument();
        first.ApplyOutputPreset(PdfOutputPreset.Deterministic);
        second.ApplyOutputPreset(PdfOutputPreset.Deterministic);

        byte[] firstBytes = new PdfWriter().GenerateBytes(first);
        byte[] secondBytes = new PdfWriter().GenerateBytes(second);

        first.GenerationOptions.Deterministic.Should().BeTrue();
        second.GenerationOptions.Deterministic.Should().BeTrue();
        firstBytes.Should().Equal(secondBytes);
    }

    [Fact]
    public void LanguageExplicitIdentifierAndXmp_AreWrittenToCatalogAndTrailer()
    {
        PdfDocument document = CreateDocument();
        document.OutputOptions.ReadableContentStreams = true;
        document.Metadata.Language = "en-ZA";
        document.Metadata.CustomXmp = "<x:xmpmeta xmlns:x=\"adobe:ns:meta/\"><sample>safe</sample></x:xmpmeta>";
        document.GenerationOptions.DocumentIdentifier = "00112233445566778899AABBCCDDEEFF";

        string pdf = Encoding.Latin1.GetString(new PdfWriter().GenerateBytes(document));

        pdf.Should().Contain("/Lang (en-ZA)");
        pdf.Should().Contain("/Type /Metadata");
        pdf.Should().Contain("<sample>safe</sample>");
        pdf.Should().Contain("/ID [<00112233445566778899AABBCCDDEEFF> <00112233445566778899AABBCCDDEEFF>]");
    }

    [Theory]
    [InlineData("not a language")]
    [InlineData("en_za")]
    public void Metadata_InvalidLanguage_FailsClearly(string language)
    {
        var metadata = new DocumentMetadata { Language = language };
        Action action = metadata.Validate;
        action.Should().Throw<ArgumentException>().WithMessage("*language*");
    }

    [Theory]
    [InlineData("<broken>")]
    [InlineData("<!DOCTYPE x [<!ENTITY e SYSTEM 'file:///tmp/secret'>]><x>&e;</x>")]
    public void Metadata_UnsafeOrMalformedXmp_FailsClearly(string xmp)
    {
        var metadata = new DocumentMetadata { CustomXmp = xmp };
        Action action = metadata.Validate;
        action.Should().Throw<ArgumentException>().WithMessage("*XMP*");
    }

    [Fact]
    public void ConfiguredMetadataAndXmpLimits_AreEnforcedBeforeWriting()
    {
        PdfDocument metadataDocument = CreateDocument();
        metadataDocument.Metadata.Title = "12345";
        metadataDocument.RenderLimits.MaximumMetadataCharacters = 4;

        Action metadataWrite = () => new PdfWriter().GenerateBytes(metadataDocument);
        metadataWrite.Should().Throw<ArgumentException>().WithMessage("*configured maximum*");

        PdfDocument xmpDocument = CreateDocument();
        xmpDocument.Metadata.CustomXmp = "<x>12345</x>";
        xmpDocument.RenderLimits.MaximumXmpBytes = 8;

        Action xmpWrite = () => new PdfWriter().GenerateBytes(xmpDocument);
        xmpWrite.Should().Throw<ArgumentException>().WithMessage("*configured maximum*");
    }

    [Fact]
    public void OutputLimit_IsEnforcedForByteAndStreamingGeneration()
    {
        PdfDocument byteDocument = CreateDocument();
        byteDocument.RenderLimits.MaximumOutputBytes = 64;
        Action byteWrite = () => new PdfWriter().GenerateBytes(byteDocument);
        byteWrite.Should().Throw<PdfRenderLimitException>().Which.LimitName.Should().Be(nameof(PdfRenderLimits.MaximumOutputBytes));

        PdfDocument streamDocument = CreateDocument();
        streamDocument.RenderLimits.MaximumOutputBytes = 64;
        using var destination = new MemoryStream();
        Action streamWrite = () => new PdfWriter().GenerateStream(streamDocument, destination);
        streamWrite.Should().Throw<PdfRenderLimitException>().Which.LimitName.Should().Be(nameof(PdfRenderLimits.MaximumOutputBytes));
    }

    [Fact]
    public void SuccessfulGeneration_PublishesReadOnlyMetrics()
    {
        PdfDocument document = CreateDocument();
        var writer = new PdfWriter();

        byte[] bytes = writer.GenerateBytes(document);

        writer.LastGenerationMetrics.Should().NotBeNull();
        PdfGenerationMetrics metrics = writer.LastGenerationMetrics!;
        metrics.PagesPlanned.Should().Be(1);
        metrics.PageContentStreamsWritten.Should().Be(1);
        metrics.ObjectsWritten.Should().BeGreaterThan(0);
        metrics.OutputBytes.Should().Be(bytes.LongLength);
        metrics.Elapsed.Should().BeGreaterThanOrEqualTo(TimeSpan.Zero);
        document.LastGenerationMetrics.Should().BeSameAs(metrics);
    }

    [Fact]
    public void PageAndElementCollections_AreReadOnly_WithObsoleteCompatibilityShims()
    {
        PdfDocument document = CreateDocument();

        ((ICollection<PdfPage>)document.Pages).IsReadOnly.Should().BeTrue();
        ((ICollection<PdfElement>)document.Pages[0].Elements).IsReadOnly.Should().BeTrue();

        typeof(PdfDocument).GetProperty("MutablePages")!
            .GetCustomAttribute<ObsoleteAttribute>()!.DiagnosticId.Should().Be("PDFB008");
        typeof(PdfPage).GetProperty("MutableElements")!
            .GetCustomAttribute<ObsoleteAttribute>()!.DiagnosticId.Should().Be("PDFB009");
    }

    [Fact]
    public void InvalidExplicitIdentifier_FailsBeforeOutput()
    {
        PdfDocument document = CreateDocument();
        document.GenerationOptions.DocumentIdentifier = "not-hex";
        Action action = () => new PdfWriter().GenerateBytes(document);
        action.Should().Throw<ArgumentException>().WithMessage("*32 or 64 hexadecimal*");
    }

    private static PdfDocument CreateDocument()
    {
        var document = new PdfDocument();
        PdfPage page = document.AddPage();
        page.AddElement(new TextElement("Output hardening", 40, 700));
        return document;
    }
}
