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
        private sealed class RecordingComponent : IMeasurable
        {
            public float DrawnContentTop { get; private set; }

            public LayoutMeasurement Measure(LayoutMeasureContext context)
                => new(3f, 10f, 0f, 20f);

            public void Draw(LayoutDrawContext context, LayoutMeasurement measurement)
                => DrawnContentTop = context.ContentTop;
        }

        private sealed class PositionRecordingComponent : IMeasurable
        {
            private readonly float _height;

            public PositionRecordingComponent(float height) => _height = height;

            public float DrawnContentLeft { get; private set; }
            public float DrawnContentTop { get; private set; }

            public LayoutMeasurement Measure(LayoutMeasureContext context)
                => new(0f, _height, 0f, context.Column.Width);

            public void Draw(LayoutDrawContext context, LayoutMeasurement measurement)
            {
                DrawnContentLeft = context.ContentLeft;
                DrawnContentTop = context.ContentTop;
            }
        }

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
        public void TextComponent_Draw_PositionsFirstGlyphInsideMeasuredTopPadding()
        {
            var document = new PdfDocument();
            var page = document.AddPage();
            var column = new FlowColumn(0, 20f, 240f, 200f, 20f);
            var options = new LayoutOptions();
            var source = new TextElement("Aligned text", 0f, 0f)
            {
                FontSize = 12f,
                MarginTop = 0f,
                PaddingTop = 5f
            };
            var component = new TextComponent(source, defaultSpacing: 0f);
            var measurement = component.Measure(new LayoutMeasureContext(page, column, options));

            component.Draw(new LayoutDrawContext(page, column, 20f, 200f, 240f, options), measurement);

            var rendered = page.Elements.OfType<TextElement>().Single();
            var firstLine = rendered.ShapedLayout!.Lines[0];
            (rendered.Y + firstLine.Ascent + rendered.PaddingTop!.Value).Should().BeApproximately(200f, 0.01f);
        }

        [Fact]
        public void PaddingComponent_Draw_PreservesTheTopInsetAppliedDuringMeasurement()
        {
            var document = new PdfDocument();
            var page = document.AddPage();
            var column = new FlowColumn(0, 20f, 240f, 200f, 20f);
            var options = new LayoutOptions();
            var child = new RecordingComponent();
            var component = new PaddingComponent(child, new PaddingValues(0f, 12f, 0f, 0f));
            var measurement = component.Measure(new LayoutMeasureContext(page, column, options));
            var contentTop = 200f - measurement.MarginTop;

            component.Draw(new LayoutDrawContext(page, column, 20f, contentTop, 240f, options), measurement);

            child.DrawnContentTop.Should().BeApproximately(contentTop, 0.01f);
        }

        [Fact]
        public void AlignComponent_Draw_RightAlignedTextKeepsItsTextBoxWithinAvailableWidth()
        {
            var document = new PdfDocument();
            var page = document.AddPage();
            var column = new FlowColumn(0, 20f, 150f, 200f, 20f);
            var options = new LayoutOptions();
            var text = new TextElement("1 of 1", 0f, 0f)
            {
                FontSize = 7.5f,
                Alignment = TextAlignment.Right,
                MarginTop = 0f
            };
            var component = new AlignComponent(
                new TextComponent(text, defaultSpacing: 0f),
                LayoutHorizontalAlignment.Right,
                LayoutVerticalAlignment.Top,
                minHeight: null);
            var measurement = component.Measure(new LayoutMeasureContext(page, column, options));

            component.Draw(new LayoutDrawContext(page, column, 20f, 200f, 150f, options), measurement);

            var rendered = page.Elements.OfType<TextElement>().Single();
            (rendered.X + rendered.MaxWidth!.Value).Should().BeLessThanOrEqualTo(170f);
        }

        [Fact]
        public void RowComponent_Draw_HonorsGapAndPerItemVerticalAlignment()
        {
            var document = new PdfDocument();
            var page = document.AddPage();
            var column = new FlowColumn(0, 20f, 100f, 200f, 20f);
            var options = new LayoutOptions();
            var tall = new PositionRecordingComponent(20f);
            var shortCentered = new PositionRecordingComponent(10f);
            var row = new RowComponent { Gap = 6f };
            row.Add(tall, RowComponent.RowWidthSpec.Fixed(20f), LayoutVerticalAlignment.Top);
            row.Add(shortCentered, RowComponent.RowWidthSpec.Fixed(20f), LayoutVerticalAlignment.Middle);
            var measurement = row.Measure(new LayoutMeasureContext(page, column, options));

            row.Draw(new LayoutDrawContext(page, column, 20f, 200f, 100f, options), measurement);

            tall.DrawnContentLeft.Should().BeApproximately(20f, 0.01f);
            tall.DrawnContentTop.Should().BeApproximately(200f, 0.01f);
            shortCentered.DrawnContentLeft.Should().BeApproximately(46f, 0.01f);
            shortCentered.DrawnContentTop.Should().BeApproximately(195f, 0.01f);
        }

        [Fact]
        public void CanonicalRow_AlignMiddle_CentersVisibleSvgAndTextContent()
        {
            const string svgMarkup = "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"10\" height=\"10\"><rect width=\"10\" height=\"10\" fill=\"#3514C9\"/></svg>";
            var document = PdfDocument.Create(descriptor => descriptor.Page(page => page.Content().Row(row =>
            {
                row.Gap(4f);
                row.ConstantItem(20f).AlignMiddle().Svg(svgMarkup, 10f, 10f);
                row.RelativeItem().AlignMiddle().Text("Aligned text").FontSize(8f);
            })));

            var page = document.Pages.Single();
            var icon = page.Elements.OfType<SvgElement>().Single();
            var text = page.Elements.OfType<TextElement>().Single();
            var firstLine = text.ShapedLayout!.Lines.Single();
            float iconCenter = icon.Y - icon.Height * 0.5f;
            float textCenter = text.Y + firstLine.Ascent - firstLine.LineHeight * 0.5f;

            textCenter.Should().BeApproximately(iconCenter, 0.1f);
        }

        [Fact]
        public void ColumnComponent_Measure_DoesNotReserveTrailingSpacingBeforeAPageBreak()
        {
            var document = new PdfDocument();
            var page = document.AddPage();
            var column = new FlowColumn(0, 20f, 100f, 200f, 180f);
            var options = new LayoutOptions();
            var component = new ColumnComponent { Spacing = 5f };
            component.Add(new PositionRecordingComponent(20f));
            component.Add(new PositionRecordingComponent(10f));

            var measurement = component.Measure(new LayoutMeasureContext(page, column, options));

            measurement.IsPartial.Should().BeTrue();
            measurement.ReservedHeight.Should().BeApproximately(20f, 0.01f);
            measurement.Remainder.Should().NotBeNull();
        }

        [Fact]
        public void ColumnComponent_Measure_DefersAChildThatExceedsTheRemainingHeight()
        {
            var document = new PdfDocument();
            var page = document.AddPage();
            var column = new FlowColumn(0, 20f, 100f, 200f, 180f);
            var options = new LayoutOptions();
            var component = new ColumnComponent { Spacing = 2f };
            component.Add(new PositionRecordingComponent(15f));
            component.Add(new PositionRecordingComponent(10f));

            var measurement = component.Measure(new LayoutMeasureContext(page, column, options));

            measurement.IsPartial.Should().BeTrue();
            measurement.ReservedHeight.Should().BeApproximately(15f, 0.01f);
            measurement.Remainder.Should().NotBeNull();
        }

        [Fact]
        public void KeepTogetherComponent_Measure_WrapsInsteadOfReturningAPartialFragment()
        {
            var document = new PdfDocument();
            var page = document.AddPage();
            var column = new FlowColumn(0, 20f, 100f, 200f, 180f);
            var options = new LayoutOptions();
            var children = new ColumnComponent { Spacing = 2f };
            children.Add(new PositionRecordingComponent(15f));
            children.Add(new PositionRecordingComponent(10f));
            var component = new KeepTogetherComponent(children);

            var measurement = component.Measure(new LayoutMeasureContext(page, column, options));

            measurement.IsWrap.Should().BeTrue();
            measurement.ReservedHeight.Should().Be(0f);
        }

        [Fact]
        public void CellDrawBuffer_DoesNotApplyASecondTextBaselineOffset()
        {
            var document = new PdfDocument();
            var page = document.AddPage();
            var column = new FlowColumn(0, 20f, 240f, 200f, 20f);
            var options = new LayoutOptions();
            var source = new TextElement("Cell text", 0f, 0f)
            {
                FontSize = 12f,
                MarginTop = 0f
            };
            var component = new TextComponent(source, defaultSpacing: 0f);
            var measurement = component.Measure(new LayoutMeasureContext(page, column, options));
            var buffer = new CellDrawBuffer(page, options);

            var rendered = buffer.Draw(component, measurement, 20f, 200f, 240f, measurement.ReservedHeight)
                .OfType<TextElement>()
                .Single();

            var firstLine = rendered.ShapedLayout!.Lines[0];
            (rendered.Y + firstLine.Ascent).Should().BeApproximately(200f, 0.01f);
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
