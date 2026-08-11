using System;
using System.IO;
using System.Linq;
using System.Text;
using FluentAssertions;
using PdfBuilder.Document;
using PdfBuilder.Elements;
using PdfBuilder.Models;
using PdfBuilder.Writer;
using Xunit;

namespace PdfBuilder.Tests
{
    public sealed class SerializerEncodingTests
    {
        [Theory]
        [InlineData("M\u00FCnchen")]
        [InlineData("Jos\u00E9")]
        [InlineData("S\u00E3o Paulo")]
        [InlineData("TT Logistics \u2013 D\u00E9p\u00F4t")]
        public void PdfStringEncoder_PdfDocEncodingText_UsesLiteralString(string value)
        {
            PdfStringEncoder.Encode(value).Should().StartWith("(").And.EndWith(")");
        }

        [Theory]
        [InlineData("\u0634\u0631\u0643\u0629 \u0627\u0644\u0646\u0642\u0644")]
        [InlineData("\u5317\u4EAC\u8FD0\u8F93")]
        public void PdfStringEncoder_UnicodeText_UsesUtf16BigEndianHex(string value)
        {
            string encoded = PdfStringEncoder.Encode(value);
            encoded.Should().StartWith("<FEFF").And.EndWith(">");
            encoded.Should().Contain(Convert.ToHexString(Encoding.BigEndianUnicode.GetBytes(value)));
        }

        [Fact]
        public void PdfStringEncoder_LiteralText_EscapesDelimitersAndControls()
        {
            PdfStringEncoder.Encode("a(b)\\c\n\t").Should().Be("(a\\(b\\)\\\\c\\n\\t)");
        }

        [Fact]
        public void PdfStringEncoder_EmptyAndMalformedSurrogate_AreEncodedSafely()
        {
            PdfStringEncoder.Encode(string.Empty).Should().Be("()");
            PdfStringEncoder.Encode("\uD800").Should().Be("<FEFFFFFD>");
        }

        [Fact]
        public void PdfNameEncoder_EscapesUnsafeUtf8Bytes()
        {
            PdfNameEncoder.Encode("Font name#\u5317\u4EAC").Should().Be("/Font#20name#23#E5#8C#97#E4#BA#AC");
        }

        [Fact]
        public void PdfNameEncoder_EmptyAndMalformedSurrogate_AreEncodedSafely()
        {
            PdfNameEncoder.Encode(string.Empty).Should().Be("/");
            PdfNameEncoder.Encode("\uD800").Should().Be("/#EF#BF#BD");
        }

        [Theory]
        [InlineData("2026-01-02T03:04:05+00:00", "(D:20260102030405Z)")]
        [InlineData("2026-01-02T03:04:05+05:30", "(D:20260102030405+05'30')")]
        [InlineData("2026-01-02T03:04:05-04:00", "(D:20260102030405-04'00')")]
        public void PdfDateEncoder_PreservesOffset(string value, string expected)
        {
            PdfDateEncoder.Encode(DateTimeOffset.Parse(value)).Should().Be(expected);
        }

        [Fact]
        public void UnicodeMetadataAndUri_AreCentrallyEncoded()
        {
            var document = CreateDocument("\u0634\u0631\u0643\u0629 \u0627\u0644\u0646\u0642\u0644");
            document.Title = "\u5317\u4EAC\u8FD0\u8F93";
            document.Metadata.Author = "Jos\u00E9";
            const string uri = "https://example.com/a(b)\\c?q=S\u00E3o%20Paulo/\u5317\u4EAC";
            document.Pages[0].Elements.Add(new LinkRectElement(40, 700, 100, 20) { Url = uri });

            string pdf = Encoding.Latin1.GetString(new PdfWriter().GenerateBytes(document));
            pdf.Should().Contain($"/Title {PdfStringEncoder.Encode(document.Title)}");
            pdf.Should().Contain($"/Author {PdfStringEncoder.Encode(document.Metadata.Author)}");
            pdf.Should().Contain($"/URI {PdfStringEncoder.Encode(uri)}");
        }

        [Fact]
        public void CallerControlledMetadataOutlineAndUri_EdgeCasesUseCentralEncoding()
        {
            const string title = "Title (draft) \\ control\u0001";
            const string author = "\u5317\u4EAC\u8FD0\u8F93";
            const string outline = "\u7AE0\u7BC0 (\u4E00) \\";
            const string uri = "https://example.test/\u8DEF\u5F84?q=\u043F\u0440\u0438\u0432\u0435\u0442(1)\\x";
            var document = CreateDocument("Serializer edge cases");
            document.Title = title;
            document.Metadata.Author = author;
            document.Metadata.Subject = string.Empty;
            document.Metadata.Keywords = "a\tb\rc\n";
            document.Pages[0].Elements.Add(new AnchorElement("edge", 40, 700) { Title = outline });
            document.Pages[0].Elements.Add(new LinkRectElement(40, 680, 100, 20) { Url = uri });

            string pdf = Encoding.Latin1.GetString(new PdfWriter().GenerateBytes(document));

            pdf.Should().Contain($"/Title {PdfStringEncoder.Encode(title)}");
            pdf.Should().Contain($"/Author {PdfStringEncoder.Encode(author)}");
            pdf.Should().Contain($"/Keywords {PdfStringEncoder.Encode(document.Metadata.Keywords)}");
            pdf.Should().Contain($"/Title {PdfStringEncoder.Encode(outline)}");
            pdf.Should().Contain($"/URI {PdfStringEncoder.Encode(uri)}");
            pdf.Should().NotContain("/Subject ");
        }

        [Fact]
        public void ExtremelyLongMetadata_WithinOutputLimit_GeneratesSuccessfully()
        {
            var document = CreateDocument("Long metadata");
            document.Metadata.Subject = new string('x', 100_000);
            document.RenderLimits.MaximumOutputBytes = 250_000;

            byte[] bytes = new PdfWriter().GenerateBytes(document);

            bytes.Should().HaveCountLessThan(250_000);
            Encoding.Latin1.GetString(bytes).Should().Contain($"/Subject {PdfStringEncoder.Encode(document.Metadata.Subject)}");
        }

        [Fact]
        public void StreamDictionaryCallerControlledNames_UseCentralNameEncoding()
        {
            using var stream = new MemoryStream();
            using (var writer = new PdfStreamWriter(stream))
            {
                writer.WriteHeader();
                writer.BeginObject();
                writer.WriteStream(Array.Empty<byte>(), ("Caller key#", "/Value"));
                writer.EndObject();
            }

            Encoding.Latin1.GetString(stream.ToArray()).Should().Contain("/Caller#20key#23 /Value");
        }

        [Fact]
        public void DeterministicMode_EquivalentDocumentsProduceIdenticalBytes()
        {
            var first = CreateDocument("Deterministic");
            var second = CreateDocument("Deterministic");
            ConfigureDeterminism(first);
            ConfigureDeterminism(second);

            new PdfWriter().GenerateBytes(first).Should().Equal(new PdfWriter().GenerateBytes(second));
        }

        [Fact]
        public void DeterministicMode_EquivalentResourceRichDocumentsProduceIdenticalBytesAndIds()
        {
            var first = CreateResourceRichDocument();
            var second = CreateResourceRichDocument();
            ConfigureDeterminism(first);
            ConfigureDeterminism(second);

            byte[] firstBytes = new PdfWriter().GenerateBytes(first);
            byte[] secondBytes = new PdfWriter().GenerateBytes(second);
            string pdf = Encoding.Latin1.GetString(firstBytes);

            firstBytes.Should().Equal(secondBytes);
            pdf.Should().MatchRegex(@"/ID \[<[0-9A-F]{64}> <[0-9A-F]{64}>\]");
            pdf.Should().Contain("/XObject").And.Contain("/Outlines").And.Contain("/URI ");
        }

        [Fact]
        public void ContentStreams_AreCompressedByDefault_AndReadableOnRequest()
        {
            var compressed = CreateDocument("Compressed");
            var readable = CreateDocument("Readable");
            readable.OutputOptions.ReadableContentStreams = true;

            Encoding.ASCII.GetString(new PdfWriter().GenerateBytes(compressed)).Should().Contain("/Filter /FlateDecode");
            Encoding.ASCII.GetString(new PdfWriter().GenerateBytes(readable)).Should().NotContain("/Filter /FlateDecode");
        }

        private static PdfDocument CreateDocument(string text)
            => PdfDocument.Create(document => document.Page(page => page.Content().Text(text)));

        private static PdfDocument CreateResourceRichDocument()
        {
            byte[] image = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "TestLogo.png"));
            var document = PdfDocument.Create(descriptor =>
            {
                descriptor.Metadata(metadata =>
                {
                    metadata.Title = "Resource-rich deterministic document";
                    metadata.Author = "PdfBuilder";
                });
                descriptor.Page(page => page.Content().Column(column =>
                {
                    column.Item().Text("Resource-rich document").Bold();
                    column.Item().Image(image, 80, 40);
                    column.Item().Table(table =>
                    {
                        table.Columns(columns => { columns.RelativeColumn(); columns.ConstantColumn(60); });
                        table.Header(header => { header.Cell().Text("Item").Bold(); header.Cell().Text("Value").Bold(); });
                        table.Row(row => { row.Cell().Text("One"); row.Cell().Text("1"); });
                    });
                }));
            });
            document.Pages[0].Elements.Add(new AnchorElement("resource", 40, 700) { Title = "Resource section" });
            document.Pages[0].Elements.Add(new LinkRectElement(40, 660, 100, 20) { Url = "https://example.test/resource" });
            return document;
        }

        private static void ConfigureDeterminism(PdfDocument document)
        {
            document.GenerationOptions.Deterministic = true;
            document.GenerationOptions.DocumentIdSeed = "serializer-test";
            document.GenerationOptions.CreationTime = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);
            document.GenerationOptions.ModificationTime = document.GenerationOptions.CreationTime;
        }
    }
}
