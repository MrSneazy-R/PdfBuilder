using System;
using System.Collections.Generic;
using System.Drawing;
using PdfBuilder.Document;
using PdfBuilder.Elements;
using PdfBuilder.Models;
using TableModels = PdfBuilder.Elements.Table;

namespace PdfBuilder
{
    public static class DecodedPdf
    {
        public static PdfDocument Build()
        {
            var doc = new PdfDocument { Title = "Revenue Pulse | FY25 Q3" };

            var builder = new PdfDocumentBuilder(doc)
                .Title("Revenue Pulse | FY25 Q3")
                .HeaderFooter(hf =>
                {
                    hf.HeaderTemplate = "{title}";
                    hf.FooterTemplate = "Generated {date:yyyy-MM-dd HH:mm} · Page {page}/{pages}";
                    hf.HeaderAlign = TextAlignment.Left;
                    hf.FooterAlign = TextAlignment.Right;
                    hf.FontFamily = "Helvetica";
                    hf.FontSize = 9f;
                    hf.Color = "#4A4D57";
                })
                .Master(master =>
                {
                    master.BackgroundColor = "#F7F9FC";
                    master.Watermark = new WatermarkSpec
                    {
                        Text = "INTERNAL",
                        FontFamily = "Helvetica",
                        FontSize = 60f,
                        Color = "#1C64F2",
                        Opacity = 0.08f,
                        RotationDegrees = 45f,
                        Layer = WatermarkLayer.BehindContent
                    };
                });

            var page = doc.AddPage();
            page.Columns = new ColumnLayoutSpec { Columns = 2, Gutter = 18f };
            builder.ApplySectionTo(page);

            new PdfPageBuilder(page)
                .Margin(36f)
                .AutoPaginate(doc)
                .Content(col =>
                {
                    AppendHeadline(col);
                    AppendRevenueTable(col);
                    AppendRevenueNotes(col);

                    col.ActivateColumn(1, reset: true);
                    AppendPipelineSection(col);
                    AppendRetentionSection(col);
                    AppendNextSteps(col);
                });

            return doc;
        }

        private static void AppendHeadline(ColumnBuilder col)
        {
            col.Text("Revenue Pulse")
                .FontFamily("Helvetica")
                .FontSize(20f)
                .Bold()
                .Color("#101828")
                .MarginBottom(4f)
                .Add();

            col.Text("FY25 · Q3 Executive Summary")
                .FontFamily("Helvetica")
                .FontSize(12f)
                .Color("#475467")
                .MarginBottom(6f)
                .Add();

            new RichTextBuilder(col, 0, 0, 0)
                .Font("Helvetica", 11f)
                .LineHeight(1.32f)
                .Span("A condensed dashboard covering revenue acceleration, pipeline health, and retention signals. Designed for leadership review and portfolio planning.")
                    .Color("#344054")
                    .EndSpan()
                .Add();

            col.GetFlow().Advance(12f);
        }

        private static void AppendRevenueTable(ColumnBuilder col)
        {
            var table = col.Table(0, 0, 0, 0)
                .Caption("Regional Revenue Breakdown")
                .DefaultFont("Helvetica")
                .DefaultFontSize(10f)
                .CellPadding(6f)
                .Border(Color.FromArgb(28, 100, 242), 0.5f)
                .RepeatHeaders(true)
                .EnablePageBreaks(true);

            table.HeaderRow(
                c => c.Text("Region").Bold().BorderBottom(Color.FromArgb(28, 100, 242), 1f),
                c => c.Text("YoY").Bold().AlignRight().BorderBottom(Color.FromArgb(28, 100, 242), 1f),
                c => c.Text("FY25 Q3").Bold().AlignRight().BorderBottom(Color.FromArgb(28, 100, 242), 1f),
                c => c.Text("Notes").Bold().BorderBottom(Color.FromArgb(28, 100, 242), 1f)
            );

            var rows = new[]
            {
                new { Region = "North America", Growth = "18%", Revenue = "$22.4M", Notes = "Enterprise upsell and cross-sell momentum sustained." },
                new { Region = "EMEA",          Growth = "11%", Revenue = "$17.9M", Notes = "Renewals steady; fintech pipeline accelerating." },
                new { Region = "APAC",          Growth = "24%", Revenue = "$13.2M", Notes = "Net-new marketplace integrations driving lift." },
                new { Region = "LATAM",         Growth = "7%",  Revenue = "$6.8M",  Notes = "Retail demand offsets FX pressure." }
            };

            foreach (var row in rows)
            {
                table.Row(
                    c => c.Text(row.Region),
                    c => c.Text(row.Growth).AlignRight(),
                    c => c.Text(row.Revenue).AlignRight(),
                    c => c.Text(row.Notes).LineHeight(1.15f)
                );
            }

            table.FooterRow(
                c => c.Text("Global Forecast").Bold().ColSpan(2),
                c =>
                {
                    c.Text("$69.1M").Bold().AlignRight();
                    c.BorderTop(Color.FromArgb(28, 100, 242), 1f);
                },
                c => c.Text("Confidence: High").AlignRight()
            );

            var tableElement = table.Build();
            tableElement.BorderCollapse = TableModels.BorderCollapseMode.Collapse;
            tableElement.ResolveBorderConflicts = true;
            tableElement.OuterBorder = new TableModels.BorderStyle
            {
                Color = Color.FromArgb(28, 100, 242),
                Width = 1f
            };
            tableElement.InnerBorder = new TableModels.BorderStyle
            {
                Color = Color.FromArgb(197, 205, 222),
                Width = 0.5f
            };
            tableElement.RowBanding = new TableModels.RowBandingSpec
            {
                Step = 2,
                Fills = new List<TableModels.BandFill>
                {
                    new TableModels.BandFill { FillColor = Color.FromArgb(246, 249, 255) },
                    new TableModels.BandFill { FillColor = Color.White }
                }
            };
            tableElement.ColumnBanding = new TableModels.ColumnBandingSpec
            {
                Step = 2,
                Fills = new List<TableModels.BandFill>
                {
                    new TableModels.BandFill { FillColor = null },
                    new TableModels.BandFill { FillColor = Color.FromArgb(250, 252, 255) }
                }
            };
            tableElement.OuterCornerRadiusTopLeft = 6f;
            tableElement.OuterCornerRadiusTopRight = 6f;
            tableElement.OuterCornerRadiusBottomLeft = 6f;
            tableElement.OuterCornerRadiusBottomRight = 6f;

            table.Add();

            col.GetFlow().Advance(10f);
        }

        private static void AppendRevenueNotes(ColumnBuilder col)
        {
            col.Text("Top Highlights")
                .FontFamily("Helvetica")
                .FontSize(12f)
                .Bold()
                .Color("#101828")
                .MarginBottom(4f)
                .Add();

            new ListBuilder(col, 0, 0, 0)
                .Marker(ListMarker.Bullet)
                .Font("Helvetica", 10f)
                .LineHeight(1.2f)
                .Item(new RichRun { Text = "APAC leads growth with 24% YoY increase driven by partner marketplaces." })
                .Item(new RichRun { Text = "EMEA renewals steady; mid-market fintech wins add $3.2M incremental ARR." })
                .Item(new RichRun { Text = "North America focus: pipeline conversion improving yet still below target in Enterprise West." })
                .Add();

            col.GetFlow().Advance(12f);
        }

        private static void AppendPipelineSection(ColumnBuilder col)
        {
            col.Text("Pipeline vs Plan")
                .FontFamily("Helvetica")
                .FontSize(12f)
                .Bold()
                .Color("#101828")
                .MarginBottom(4f)
                .Add();

            col.Chart(0, 0, 0, 0)
                .Title("Net Revenue vs Target")
                .TitleFont("Helvetica-Bold", 11f)
                .NumericX(0f, 3f, 4, value => new[] { "Q1", "Q2", "Q3", "Q4" }[(int)value])
                .NumericY(0f, 35f, 8, value => $"{value:0}M")
                .GridY(true)
                .AddBars("Actual",
                    Color.FromArgb(28, 100, 242),
                    Color.FromArgb(16, 92, 200),
                    0.5f, 18f, 21f, 26f, 29f)
                .AddLine("Target", Color.FromArgb(12, 166, 120), 1.5f, 20f, 22f, 28f, 32f)
                .Legend(false)
                .Add();

            col.GetFlow().Advance(12f);
        }

        private static void AppendRetentionSection(ColumnBuilder col)
        {
            col.Text("Retention Outlook")
                .FontFamily("Helvetica")
                .FontSize(12f)
                .Bold()
                .Color("#101828")
                .MarginBottom(4f)
                .Add();

            col.Chart(0, 0, 0, 0)
                .Title("Logo Retention Trend")
                .TitleFont("Helvetica-Bold", 11f)
                .NumericX(0f, 5f, 6, value => new[] { "Apr", "May", "Jun", "Jul", "Aug", "Sep" }[(int)value])
                .NumericY(88f, 101f, 7, value => $"{value:0}%")
                .AddLine("Retention", Color.FromArgb(234, 88, 12), 1.8f, 89.2f, 90.4f, 92.1f, 93.5f, 95.8f, 97.3f)
                .Legend(false)
                .Add();

            col.GetFlow().Advance(12f);
        }

        private static void AppendNextSteps(ColumnBuilder col)
        {
            new RichTextBuilder(col, 0, 0, 0)
                .Font("Helvetica", 10.5f)
                .LineHeight(1.25f)
                .Span("Next Focus · ")
                    .Bold()
                    .Color("#1C64F2")
                    .EndSpan()
                .Span("Finalize Q4 pipeline acceleration playbook, reinforce APAC enablement, and pressure-test LATAM currency hedges.")
                    .Color("#475467")
                    .EndSpan()
                .Add();
        }
    }
}
