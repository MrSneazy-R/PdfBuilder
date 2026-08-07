using System;
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
        [InlineData("München")]
        [InlineData("José")]
        [InlineData("São Paulo")]
        [InlineData("TT Logistics – Dépôt")]
        public void PdfStringEncoder_PdfDocEncodingText_UsesLiteralString(string value)
        {
            PdfStringEncoder.Encode(value).Should().StartWith("(").And.EndWith(")");
        }

        [Theory]
        [InlineData("شركة النقل")]
        [InlineData("北京运输")]
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
        public void PdfNameEncoder_EscapesUnsafeUtf8Bytes()
        {
            PdfNameEncoder.Encode("Font name#北京").Should().Be("/Font#20name#23#E5#8C#97#E4#BA#AC");
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
            var document = CreateDocument("شركة النقل");
            document.Title = "北京运输";
            document.Metadata.Author = "José";
            document.Pages[0].Elements.Add(new LinkRectElement(40, 700, 100, 20)
            {
                Url = "https://example.com/a(b)\\c?q=São%20Paulo/北京"
            });

            string pdf = Encoding.Latin1.GetString(new PdfWriter().GenerateBytes(document));
            pdf.Should().Contain($"/Title {PdfStringEncoder.Encode(document.Title)}");
            pdf.Should().Contain($"/Author {PdfStringEncoder.Encode(document.Metadata.Author)}");
            pdf.Should().Contain($"/URI {PdfStringEncoder.Encode("https://example.com/a(b)\\c?q=São%20Paulo/北京")}");
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

        private static void ConfigureDeterminism(PdfDocument document)
        {
            document.GenerationOptions.Deterministic = true;
            document.GenerationOptions.DocumentIdSeed = "serializer-test";
            document.GenerationOptions.CreationTime = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);
            document.GenerationOptions.ModificationTime = document.GenerationOptions.CreationTime;
        }
    }
}
