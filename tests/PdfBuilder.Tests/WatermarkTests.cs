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
            var stream = PdfContentHelper.ExtractFirstStream(pdf);

            stream.Should().Contain("q /GSwm");
            int gsIndex = stream.IndexOf("/GSwm gs", StringComparison.Ordinal);
            int watermarkIndex = stream.IndexOf("CONFIDENTIAL", StringComparison.Ordinal);
            gsIndex.Should().BeGreaterThanOrEqualTo(0);
            watermarkIndex.Should().BeGreaterThan(gsIndex);
        }

        [Fact]
        public void Watermark_Layer_Order_IsRespected()
        {
            var docBehind = CreateDocumentWithWatermark(WatermarkLayer.BehindContent);
            var streamBehind = PdfContentHelper.ExtractFirstStream(PdfContentHelper.Generate(docBehind));
            int watermarkBehind = streamBehind.IndexOf("CONFIDENTIAL", StringComparison.Ordinal);
            int bodyBehind = streamBehind.IndexOf("Body text", StringComparison.Ordinal);
            watermarkBehind.Should().BeGreaterThanOrEqualTo(0);
            bodyBehind.Should().BeGreaterThanOrEqualTo(0);
            watermarkBehind.Should().BeLessThan(bodyBehind);

            var docAbove = CreateDocumentWithWatermark(WatermarkLayer.AboveContent);
            var streamAbove = PdfContentHelper.ExtractFirstStream(PdfContentHelper.Generate(docAbove));
            int watermarkAbove = streamAbove.IndexOf("CONFIDENTIAL", StringComparison.Ordinal);
            int bodyAbove = streamAbove.IndexOf("Body text", StringComparison.Ordinal);
            watermarkAbove.Should().BeGreaterThan(bodyAbove);
        }

        [Fact]
        public void Watermark_Opacity_ChangesStream_WhenNot1()
        {
            var opaqueDoc = CreateDocumentWithWatermark(WatermarkLayer.BehindContent, 1f);
            var translucentDoc = CreateDocumentWithWatermark(WatermarkLayer.BehindContent, 0.25f);

            var opaqueStream = PdfContentHelper.ExtractFirstStream(PdfContentHelper.Generate(opaqueDoc));
            var translucentStream = PdfContentHelper.ExtractFirstStream(PdfContentHelper.Generate(translucentDoc));

            opaqueStream.Should().NotContain("/GSwm");
            translucentStream.Should().Contain("/GSwm");
            translucentStream.Should().NotBe(opaqueStream);
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
