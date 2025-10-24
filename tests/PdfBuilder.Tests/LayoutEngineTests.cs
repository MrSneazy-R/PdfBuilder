using System.Linq;
using FluentAssertions;
using PdfBuilder.Document;
using PdfBuilder.Document.Layout;
using PdfBuilder.Document.Layout.Components;
using PdfBuilder.Elements;
using PdfBuilder.Models;
using Xunit;

namespace PdfBuilder.Tests
{
    public class LayoutEngineTests
    {
        [Fact]
        public void TextBuilder_MeasureDraw_AdvancesFlowAndAddsElement()
        {
            var document = new PdfDocument();
            var builder = new PdfDocumentBuilder(document);
            builder.UseLayout(options => options.Mode = LayoutMode.MeasureDraw);

            var page = document.AddPage();
            var pageBuilder = new PdfPageBuilder(page);

            pageBuilder.Content(column =>
            {
                var startY = column.GetCurrentY();
                column.Text("QuestPDF style flow").FontSize(16).Add();
                var endY = column.GetCurrentY();

                page.Elements.OfType<TextElement>().Should().ContainSingle();
                endY.Should().BeLessThan(startY);
            });
        }

        [Fact]
        public void Compose_WithColumnAndRow_RendersChildrenWithExpectedPositions()
        {
            var document = new PdfDocument();
            var builder = new PdfDocumentBuilder(document);
            builder.UseLayout(options => options.Mode = LayoutMode.MeasureDraw);

            var page = document.AddPage();
            var pageBuilder = new PdfPageBuilder(page);

            pageBuilder.Content(column =>
            {
                column.Compose(comp =>
                {
                    comp.Column(col =>
                    {
                        col.Text("Header", e =>
                        {
                            e.FontSize = 18;
                            e.Bold = true;
                        });

                        col.Row(row =>
                        {
                            row.Text("Left cell");
                            row.Text("Right cell");
                        }, gap: 6f);
                    }, spacing: 10f);
                });
            });

            var texts = page.Elements.OfType<TextElement>().ToList();
            texts.Should().HaveCount(3);

            var header = texts[0];
            var leftCell = texts[1];
            var rightCell = texts[2];

            leftCell.Y.Should().BeLessThan(header.Y);
            rightCell.Y.Should().BeApproximately(leftCell.Y, 0.01f);
            rightCell.X.Should().BeGreaterThan(leftCell.X);
        }

        [Fact]
        public void MeasureDraw_Compose_IsDeterministicAcrossPages()
        {
            static PdfPage BuildPage()
            {
                var doc = new PdfDocument();
                var docBuilder = new PdfDocumentBuilder(doc);
                docBuilder.UseLayout(o => o.Mode = LayoutMode.MeasureDraw);

                var page = doc.AddPage();
                var pageBuilder = new PdfPageBuilder(page);

                pageBuilder.Content(column =>
                {
                    column.Text("Intro paragraph").FontSize(12).Add();

                    column.Compose(comp =>
                    {
                        comp.Row(row =>
                        {
                            row.Text("A");
                            row.Text("B");
                            row.Text("C");
                        });

                        comp.List(list =>
                        {
                            list.Marker = ListMarker.Decimal;
                            list.FontFamily = "Helvetica";
                            list.FontSize = 10f;
                            list.ItemSpacing = 2f;
                            list.Items.Add(new ListItem { Content = { new RichRun { Text = "First" } } });
                            list.Items.Add(new ListItem { Content = { new RichRun { Text = "Second" } } });
                        });
                    });
                });

                return page;
            }

            var first = BuildPage();
            var second = BuildPage();

            var firstText = first.Elements.OfType<TextElement>()
                .Select(t => (t.Text, t.X, t.Y, t.MaxWidth))
                .ToArray();
            var secondText = second.Elements.OfType<TextElement>()
                .Select(t => (t.Text, t.X, t.Y, t.MaxWidth))
                .ToArray();

            secondText.Should().BeEquivalentTo(firstText);

            var firstList = first.Elements.OfType<ListElement>().Single();
            var secondList = second.Elements.OfType<ListElement>().Single();

            secondList.X.Should().Be(firstList.X);
            secondList.Y.Should().Be(firstList.Y);
            secondList.MaxWidth.Should().Be(firstList.MaxWidth);
        }

        [Fact]
        public void DocumentComposer_PageCompose_AddsContent()
        {
            var document = new PdfDocument();
            var builder = new PdfDocumentBuilder(document).DefaultContentMargin(36f);

            builder.Compose(doc =>
            {
                doc.Page(page =>
                {
                    page.Compose(flow =>
                    {
                        flow.Padding(12f, inner => inner.Text("Composer API"));
                    });
                });
            });

            document.Pages.Should().HaveCount(1);
            var page = document.Pages[0];
            page.Elements.OfType<TextElement>().Single().Text.Should().Be("Composer API");
        }

        [Fact]
        public void Align_Center_PositionsTextInMiddleOfColumn()
        {
            var document = new PdfDocument();
            var builder = new PdfDocumentBuilder(document).DefaultContentMargin(36f);

            builder.Compose(doc =>
            {
                doc.Page(page =>
                {
                    page.Compose(flow =>
                    {
                        flow.Align(LayoutHorizontalAlignment.Center, LayoutVerticalAlignment.Top, inner =>
                        {
                            inner.Text("Aligned content", t => t.FontSize = 12f);
                        });
                    });
                });
            });

            var page = document.Pages.Single();
            var text = page.Elements.OfType<TextElement>().Single();

            float margin = 36f;
            float columnWidth = page.Width - (margin * 2);
            float textWidth = PdfLayoutUtils.EstimateTextWidth(text.Text, text.FontFamily, text.FontSize, text.Monospace, text.Bold);
            float expectedX = margin + Math.Max(0f, (columnWidth - textWidth) / 2f);

            text.X.Should().BeApproximately(expectedX, 3f);
        }

        [Fact]
        public void Layer_CombinesBackgroundContentAndForeground()
        {
            var document = new PdfDocument();
            var builder = new PdfDocumentBuilder(document);

            builder.Compose(doc =>
            {
                doc.Page(page =>
                {
                    page.Compose(flow =>
                    {
                        flow.Layer(layer =>
                        {
                            layer.Background(b => b.Text("BG"));
                            layer.Content(c => c.Text("Main"));
                            layer.Foreground(f => f.Text("FG"));
                        });
                    });
                });
            });

            var page = document.Pages.Single();
            var texts = page.Elements.OfType<TextElement>().Select(t => t.Text).ToArray();
            texts.Should().BeEquivalentTo(new[] { "BG", "Main", "FG" });
        }

        [Fact]
        public void Decoration_BackgroundAndForeground_AreInvoked()
        {
            var document = new PdfDocument();
            var builder = new PdfDocumentBuilder(document);

            var recorded = new System.Collections.Generic.List<FlowRect>();

            builder.Compose(doc =>
            {
                doc.Page(page =>
                {
                    page.Compose(flow =>
                    {
                        flow.Decorate(deco =>
                        {
                            deco.Background(ctx => recorded.Add(ctx.Rect));
                            deco.Foreground(ctx => recorded.Add(ctx.Rect));
                        }, inner => inner.Text("Decorated"));
                    });
                });
            });

            recorded.Should().HaveCount(2);
            recorded[0].Height.Should().BeGreaterThan(0f);
            recorded[0].Width.Should().BeGreaterThan(0f);
        }

        [Fact]
        public void Relative_FillsAvailableHeightForItems()
        {
            var document = new PdfDocument();
            var builder = new PdfDocumentBuilder(document).DefaultContentMargin(36f);
            var captured = new System.Collections.Generic.List<FlowRect>();

            builder.Compose(doc =>
            {
                doc.Page(page =>
                {
                    page.Compose(flow =>
                    {
                        flow.Decorate(d => d.Background(ctx => captured.Add(ctx.Rect)), content =>
                        {
                            content.Relative(rel =>
                            {
                                rel.Item(1f, item => item.Text("Top"));
                                rel.Item(1f, item => item.Text("Bottom"));
                            });
                        });
                    });
                });
            });

            captured.Should().ContainSingle();
            captured[0].Height.Should().BeApproximately(document.Pages[0].Height - (36f * 2), 1f);
        }

        [Fact]
        public void Dynamic_AddsOneComponentPerItem()
        {
            var document = new PdfDocument();
            var builder = new PdfDocumentBuilder(document);

            builder.Compose(doc =>
            {
                doc.Page(page =>
                {
                    page.Compose(flow =>
                    {
                        flow.Dynamic(new[] { "One", "Two", "Three" }, (item, inner) =>
                        {
                            inner.Text(item);
                        });
                    });
                });
            });

            document.Pages.Single().Elements.OfType<TextElement>()
                .Select(t => t.Text)
                .Should().BeEquivalentTo(new[] { "One", "Two", "Three" });
        }

        [Fact]
        public void DefaultTextStyle_OnDocument_AppliesToAllTextElements()
        {
            var document = new PdfDocument();
            var builder = new PdfDocumentBuilder(document);

            builder.DefaultTextStyle(style =>
            {
                style.FontFamily = "Times New Roman";
                style.FontSize = 18f;
                style.Color = "#123456";
                style.FlowDirection = FlowDirection.RightToLeft;
            });

            builder.Compose(doc =>
            {
                doc.Page(page =>
                {
                    page.Column(column =>
                    {
                        column.Text("Heading").Add();
                        column.Compose(comp => comp.Text("Body"));
                    });
                });
            });

            document.TextDefaults.FlowDirection.Should().Be(FlowDirection.RightToLeft);
            document.Pages.Single().TextDefaults.FlowDirection.Should().Be(FlowDirection.RightToLeft);

            var texts = document.Pages.Single().Elements.OfType<TextElement>().ToList();
            texts.Should().HaveCount(2);
            texts.Should().AllSatisfy(t =>
            {
                t.FontFamily.Should().Be("Times New Roman");
                t.FontSize.Should().Be(18f);
                t.Color.Should().Be("#123456");
                t.FlowDirection.Should().Be(FlowDirection.RightToLeft);
            });
        }

        [Fact]
        public void DefaultTextStyle_OnPage_OverridesDocumentDefaults()
        {
            var document = new PdfDocument();
            var builder = new PdfDocumentBuilder(document);

            builder.DefaultTextStyle(style =>
            {
                style.FontFamily = "Helvetica";
                style.FontSize = 12f;
            });

            builder.Compose(doc =>
            {
                doc.Page(page =>
                {
                    page.DefaultTextStyle(style =>
                    {
                        style.FontFamily = "Courier";
                        style.FontSize = 10f;
                        style.Color = "#FF0000";
                        style.FlowDirection = FlowDirection.RightToLeft;
                    });

                    page.Column(column =>
                    {
                        column.Text("Page Text").Add();
                        column.Compose(comp => comp.Text("More Text"));
                    });
                });
            });

            var texts = document.Pages.Single().Elements.OfType<TextElement>().ToList();
            texts.Should().HaveCount(2);
            texts.Should().AllSatisfy(t =>
            {
                t.FontFamily.Should().Be("Courier");
                t.FontSize.Should().Be(10f);
                t.Color.Should().Be("#FF0000");
                t.FlowDirection.Should().Be(FlowDirection.RightToLeft);
            });
        }

        [Fact]
        public void DefaultTextStyle_OnColumn_AppliesWithinScope()
        {
            var document = new PdfDocument();
            var builder = new PdfDocumentBuilder(document);

            builder.Compose(doc =>
            {
                doc.Page(page =>
                {
                    page.Column(column =>
                    {
                        column.DefaultTextStyle(style =>
                        {
                            style.FontFamily = "Calibri";
                            style.FontSize = 14f;
                            style.FlowDirection = FlowDirection.RightToLeft;
                        });

                        column.Text("Column Text").Add();
                        column.Compose(comp => comp.Text("Nested Text"));
                    });
                });
            });

            var texts = document.Pages.Single().Elements.OfType<TextElement>().ToList();
            texts.Should().HaveCount(2);
            texts.Should().AllSatisfy(t =>
            {
                t.FontFamily.Should().Be("Calibri");
                t.FontSize.Should().Be(14f);
                t.FlowDirection.Should().Be(FlowDirection.RightToLeft);
            });
        }
    }
}
