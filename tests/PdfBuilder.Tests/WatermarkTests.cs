using System;
using System.Linq;
using System.Text;
using FluentAssertions;
using PdfBuilder.Document;
using PdfBuilder.Models;
using Xunit;

namespace PdfBuilder.Tests
{
    public class WatermarkTests
    {
        static WatermarkTests()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }

        [Fact]
        public void Watermark_Opacity_GraphicsStateApplied()
        {
            var doc = new PdfDocument
            {
                Master = new MasterPageSpec
                {
                    Watermark = new WatermarkSpec
                    {
                        Text = "CONFIDENTIAL",
                        FontFamily = "Helvetica",
                        FontSize = 48,
                        Opacity = 0.5f
                    }
                }
            };

            var page = doc.AddPage();
            new PdfPageBuilder(page)
                .Margin(40)
                .Content(col =>
                {
                    col.Text("Body text").FontSize(12).Add();
                });

            var pdf = PdfContentHelper.Generate(doc);
            var contentStream = PdfContentHelper.ExtractFirstStream(pdf);
            var blocks = PdfTextExtractor.ExtractTextBlocks(pdf);

            contentStream.Should().Contain("q /GSwm");
            int gsIndex = contentStream.IndexOf("/GSwm gs", StringComparison.Ordinal);
            var watermarkIndex = blocks.FindIndex(t => t.Contains("CONFIDENTIAL", StringComparison.Ordinal));
            gsIndex.Should().BeGreaterThanOrEqualTo(0);
            watermarkIndex.Should().BeGreaterThanOrEqualTo(0);
        }

        [Fact]
        public void Watermark_Layer_Order_IsRespected()
        {
            var docBehind = CreateDocumentWithWatermark(WatermarkLayer.BehindContent);
            var pdfBehind = PdfContentHelper.Generate(docBehind);
            var blocksBehind = PdfTextExtractor.ExtractTextBlocks(pdfBehind);
            int watermarkBehind = blocksBehind.FindIndex(t => t.Contains("CONFIDENTIAL", StringComparison.Ordinal));
            int bodyBehind = blocksBehind.FindIndex(t => t.Contains("Body text", StringComparison.Ordinal));
            watermarkBehind.Should().BeGreaterThanOrEqualTo(0);
            bodyBehind.Should().BeGreaterThanOrEqualTo(0);
            watermarkBehind.Should().BeLessThan(bodyBehind);

            var docAbove = CreateDocumentWithWatermark(WatermarkLayer.AboveContent);
            var pdfAbove = PdfContentHelper.Generate(docAbove);
            var blocksAbove = PdfTextExtractor.ExtractTextBlocks(pdfAbove);
            int watermarkAbove = blocksAbove.FindIndex(t => t.Contains("CONFIDENTIAL", StringComparison.Ordinal));
            int bodyAbove = blocksAbove.FindIndex(t => t.Contains("Body text", StringComparison.Ordinal));
            watermarkAbove.Should().BeGreaterThan(bodyAbove);
        }

        [Fact]
        public void Watermark_Opacity_ChangesStream_WhenNot1()
        {
            var opaqueDoc = CreateDocumentWithWatermark(WatermarkLayer.BehindContent, 1f);
            var translucentDoc = CreateDocumentWithWatermark(WatermarkLayer.BehindContent, 0.25f);

            var opaqueContent = PdfContentHelper.ExtractFirstStream(PdfContentHelper.Generate(opaqueDoc));
            var translucentContent = PdfContentHelper.ExtractFirstStream(PdfContentHelper.Generate(translucentDoc));

            opaqueContent.Should().NotContain("/GSwm");
            translucentContent.Should().Contain("/GSwm");
            translucentContent.Should().NotBe(opaqueContent);
        }

        private static PdfDocument CreateDocumentWithWatermark(WatermarkLayer layer, float opacity = 0.5f)
        {
            var doc = new PdfDocument
            {
                Master = new MasterPageSpec
                {
                    Watermark = new WatermarkSpec
                    {
                        Text = "CONFIDENTIAL",
                        FontFamily = "Helvetica",
                        FontSize = 48,
                        Layer = layer,
                        Opacity = opacity
                    }
                }
            };

            var page = doc.AddPage();
            new PdfPageBuilder(page)
                .Margin(36)
                .Content(col =>
                {
                    col.Text("Body text").FontSize(14).Add();
                });

            return doc;
        }
    }
}
