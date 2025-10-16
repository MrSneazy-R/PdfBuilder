using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using PdfBuilder.Document;
using PdfBuilder.Elements;
using PdfBuilder.Models;

namespace PdfBuilder
{
    public static class DecodedPdf
    {
        private const float ContentMargin = 56f;
        private static byte[]? _bannerLogo;
        private static byte[]? _sealLogo;

        public static PdfDocument CreateTestDoc()
        {
            var doc = new PdfDocument();
            doc.Title = "Aurora Dynamics Q2 Insight Brief";

            var docBuilder = new PdfDocumentBuilder(doc)
                .Title("Aurora Dynamics Q2 Insight Brief")
                .HeaderFooter(hf =>
                {
                    hf.HeaderTemplate = "Aurora Dynamics - Executive Intelligence {date:yyyy}";
                    hf.FooterTemplate = "Confidential - {page}/{pages}";
                    hf.HeaderAlign = TextAlignment.Center;
                    hf.FooterAlign = TextAlignment.Right;
                    hf.FontFamily = "Helvetica";
                    hf.FontSize = 9f;
                    hf.Color = "#1F2937";
                })
                .Master(master =>
                {
                    master.BackgroundColor = "#F7FAFC";
                    master.Watermark = new WatermarkSpec
                    {
                        Text = "Aurora Dynamics",
                        FontFamily = "Helvetica",
                        FontSize = 64f,
                        Color = "#1C64F2",
                        Opacity = 0.035f,
                        RotationDegrees = 45f,
                        Layer = WatermarkLayer.BehindContent
                    };
                });

            BuildOverviewPage(doc, docBuilder);
            BuildAnalyticsPage(doc, docBuilder);
            BuildOutlookPage(doc, docBuilder);

            return doc;
        }

        private static void BuildOverviewPage(PdfDocument doc, PdfDocumentBuilder docBuilder)
        {
            var page = doc.AddPage();
            docBuilder.ApplySectionTo(page);

            float contentWidth = page.Width - (ContentMargin * 2f);

            new PdfPageBuilder(page)
                .Margin(ContentMargin)
                .Background("#FFFFFF")
                .Content(col =>
                {
                    col.Text("Aurora Dynamics Q2 Insight Brief")
                        .FontFamily("Helvetica")
                        .FontSize(26f)
                        .Bold()
                        .Color("#FFFFFF")
                        .BackgroundColor("#1C64F2")
                        .PaddingTop(14f)
                        .PaddingBottom(14f)
                        .PaddingLeft(18f)
                        .PaddingRight(18f)
                        .BackgroundCornerRadius(12f)
                        .MarginBottom(18f)
                        .Add();

                    col.Text("Executive Summary")
                        .FontFamily("Helvetica")
                        .FontSize(16f)
                        .Bold()
                        .Color("#0F172A")
                        .MarginBottom(8f)
                        .Add();

                    col.Text("Aurora Dynamics closes the second quarter with accelerated revenue momentum, renewed customer sentiment, and a tighter alignment between product delivery and go-to-market execution. The business sustained double digit growth while preserving a disciplined cost profile.")
                        .FontFamily("Helvetica")
                        .FontSize(12.5f)
                        .LineHeight(1.35f)
                        .Color("#1F2937")
                        .MarginBottom(10f)
                        .Add();

                    col.Text("This brief distills the leading indicators that leadership teams are monitoring ahead of the second-half planning cycle. Core highlights include demand velocity across enterprise accounts, marketing-influenced pipeline, and customer success interventions to reduce churn.")
                        .FontFamily("Helvetica")
                        .FontSize(12.5f)
                        .LineHeight(1.35f)
                        .Color("#1F2937")
                        .MarginBottom(16f)
                        .Add();

                    new ListBuilder(col, ContentMargin, col.GetCurrentY(), contentWidth)
                        .Marker(ListMarker.Bullet)
                        .Font("Helvetica", 12f)
                        .Item(new RichRun { Text = "Net revenue retention climbed to 114%, driven by strategic platform expansions.", Color = "#1C64F2" })
                        .Item(new RichRun { Text = "Enterprise pipeline accelerated 18% quarter-over-quarter with improved close velocity.", Color = "#0EA472" })
                        .Item(new RichRun { Text = "Customer sentiment reached a two-year high led by proactive support playbooks.", Color = "#F97316" })
                        .Item(new RichRun { Text = "Operational expenditure remained 4% under plan despite continued hiring in R&D.", Color = "#1F2937" })
                        .Add();

                    var banner = GetBannerLogo();
                    if (banner != null)
                    {
                        col.Image(banner, ContentMargin, col.GetCurrentY() + 18f, 220f, 72f)
                            .CornerRadius(18f)
                            .Shadow("#0B1E3B", 4f, -4f, 8f)
                            .MarginTop(20f)
                            .Add();
                    }

                    col.Text("Prepared by the Strategic Operations Office - July 2025")
                        .FontFamily("Helvetica")
                        .FontSize(10.5f)
                        .Color("#475569")
                        .MarginTop(12f)
                        .Add();
                });
        }

        private static void BuildAnalyticsPage(PdfDocument doc, PdfDocumentBuilder docBuilder)
        {
            var page = doc.AddPage();
            docBuilder.ApplySectionTo(page);

            float contentWidth = page.Width - (ContentMargin * 2f);
            var revenueRows = new List<RevenueRow>
            {
                new("Enterprise Platforms", 32.4f, 29.5f, 93),
                new("Growth Accounts", 18.9f, 17.3f, 89),
                new("Public Sector", 14.1f, 15.0f, 84),
                new("Channel", 8.6f, 7.9f, 88),
                new("International Expansion", 12.3f, 11.0f, 91)
            };

            new PdfPageBuilder(page)
                .Margin(ContentMargin)
                .Background("#FFFFFF")
                .Content(col =>
                {
                    col.Text("Performance Drivers")
                        .FontFamily("Helvetica")
                        .FontSize(18f)
                        .Bold()
                        .Color("#0F172A")
                        .MarginBottom(10f)
                        .Add();

                    col.Text("Quarterly operating metrics quantify how marketing, sales, and customer success collaboration expanded deal velocity while anchoring retention. The table below highlights portfolio-level contributions alongside customer sentiment.")
                        .FontFamily("Helvetica")
                        .FontSize(12.5f)
                        .LineHeight(1.3f)
                        .Color("#1F2937")
                        .MarginBottom(14f)
                        .Add();

                    float planTotal = revenueRows.Sum(r => r.Plan);
                    float actualTotal = revenueRows.Sum(r => r.Actual);
                    float totalDelta = planTotal <= 0f ? 0f : (actualTotal - planTotal) / planTotal * 100f;
                    int averagePulse = (int)Math.Round(revenueRows.Average(r => r.Pulse), MidpointRounding.AwayFromZero);

                    var tableBuilder = col.Table(ContentMargin, col.GetCurrentY(), contentWidth, 0f)
                        .Caption("Q2 Revenue Drivers")
                        .DefaultFont("Helvetica")
                        .DefaultFontSize(10.5f)
                        .CellPadding(6f)
                        .HeaderBackground("#1E3A8A")
                        .Border("#1E3A8A", 0.75f)
                        .AltRowBackground("#EEF2FF")
                        .AltRowEvery(2)
                        .HeaderRow(
                            c => c.Text("Segment").Bold().TextColor("#FFFFFF").AlignLeft(),
                            c => c.Text("Actual (USD M)").Bold().TextColor("#FFFFFF").AlignRight(),
                            c => c.Text("Plan").Bold().TextColor("#FFFFFF").AlignRight(),
                            c => c.Text("Delta vs Plan").Bold().TextColor("#FFFFFF").AlignRight(),
                            c => c.Text("Customer Pulse").Bold().TextColor("#FFFFFF").AlignRight()
                        );

                    foreach (var row in revenueRows)
                    {
                        float delta = row.Plan <= 0f ? 0f : (row.Actual - row.Plan) / row.Plan * 100f;
                        tableBuilder.Row(
                            c => c.Text(row.Segment).AlignLeft().TextColor("#0F172A"),
                            c => c.Text(row.Actual.ToString("0.0", CultureInfo.InvariantCulture)).AlignRight().TextColor("#0F172A"),
                            c => c.Text(row.Plan.ToString("0.0", CultureInfo.InvariantCulture)).AlignRight().TextColor("#0F172A"),
                            c => c.Text(FormatDelta(delta)).AlignRight().TextColor(delta >= 0 ? "#0EA472" : "#BE123C"),
                            c => c.Text($"{row.Pulse}%").AlignRight().TextColor("#0F172A")
                        );
                    }

                    tableBuilder.FooterRow(
                        c => c.Text("Total / Weighted").Bold(),
                        c => c.Text(actualTotal.ToString("0.0", CultureInfo.InvariantCulture)).AlignRight().Bold(),
                        c => c.Text(planTotal.ToString("0.0", CultureInfo.InvariantCulture)).AlignRight().Bold(),
                        c => c.Text(FormatDelta(totalDelta)).AlignRight().Bold().TextColor(totalDelta >= 0 ? "#0EA472" : "#BE123C"),
                        c => c.Text($"{averagePulse}%").AlignRight().Bold()
                    )
                    .Add();

                    col.Text("Enterprise and international cohorts delivered outsized expansion, offsetting procurement delays in the public sector pipeline. Channel productivity rose with new enablement assets, sustaining partner-driven momentum.")
                        .FontFamily("Helvetica")
                        .FontSize(12f)
                        .LineHeight(1.3f)
                        .Color("#1F2937")
                        .MarginTop(14f)
                        .Add();

                    col.Chart(ContentMargin, col.GetCurrentY() + 18f, contentWidth, 240f)
                        .Title("Pipeline Velocity & Conversion")
                        .TitleFont("Helvetica-Bold", 12f)
                        .NumericX(0f, 3f, 4, v => new[] { "Q3'24", "Q4'24", "Q1'25", "Q2'25" }[(int)v])
                        .NumericY(0f, 45f, 7, v => $"{v:0}%")
                        .GridY(true)
                        .AddBars("Conversion Rate", Color.FromArgb(28, 100, 242), Color.FromArgb(20, 83, 220), 0.6f, 26f, 28f, 31f, 35f)
                        .BarCornerRadius(6f)
                        .AddLine("Cycle Time (days)", Color.FromArgb(14, 166, 120), 1.5f, 38f, 36f, 33f, 29f)
                        .Legend(true)
                        .LabelsFont("Helvetica", 9f)
                        .Add();
                });
        }

        private static void BuildOutlookPage(PdfDocument doc, PdfDocumentBuilder docBuilder)
        {
            var page = doc.AddPage();
            docBuilder.ApplySectionTo(page);

            float contentWidth = page.Width - (ContentMargin * 2f);

            new PdfPageBuilder(page)
                .Margin(ContentMargin)
                .Background("#FFFFFF")
                .Content(col =>
                {
                    col.Text("Forward Outlook & Actions")
                        .FontFamily("Helvetica")
                        .FontSize(18f)
                        .Bold()
                        .Color("#0F172A")
                        .MarginBottom(12f)
                        .Add();

                    col.Text("Leadership will anchor second-half resourcing on sustaining enterprise momentum, hardening delivery reliability, and deepening customer partnerships. The initiatives below provide a phased blueprint for execution.")
                        .FontFamily("Helvetica")
                        .FontSize(12.5f)
                        .LineHeight(1.3f)
                        .Color("#1F2937")
                        .MarginBottom(10f)
                        .Add();

                    new RichTextBuilder(col, ContentMargin, col.GetCurrentY() + 6f, contentWidth)
                        .Font("Helvetica", 12.5f)
                        .LineHeight(1.35f)
                        .Span("Confidence Index: ").Bold().Color("#1C64F2").EndSpan()
                        .Span("7.6 / 10").Bold().Color("#0EA472").EndSpan()
                        .Span(" - Weighted by revenue coverage, product roadmap execution, and customer health.")
                            .Color("#1F2937").EndSpan()
                        .Add();

                    new ListBuilder(col, ContentMargin, col.GetCurrentY() + 14f, contentWidth)
                        .Marker(ListMarker.Decimal)
                        .Font("Helvetica", 12f)
                        .Item(new RichRun { Text = "Launch an advisory council with ten strategic customers to co-author FY26 platform themes.", Color = "#0F172A" })
                        .Item(new RichRun { Text = "Accelerate onboarding for 45 new enterprise sellers with scenario-based enablement sprints.", Color = "#0F172A" })
                        .Item(new RichRun { Text = "Deploy predictive churn scores into success playbooks, prioritizing health interventions.", Color = "#0F172A" })
                        .Item(new RichRun { Text = "Expand telemetry coverage across cloud regions to protect uptime commitments.", Color = "#0F172A" })
                        .Add();

                    var seal = GetSealLogo();
                    if (seal != null)
                    {
                        col.Image(seal, ContentMargin, col.GetCurrentY() + 24f, 120f, 120f)
                            .Clip(ImageClipShape.Circle)
                            .Border("#1C64F2", 3f)
                            .Shadow("#1F2937", 4f, -2f, 8f)
                            .MarginTop(24f)
                            .Add();
                    }

                    col.Text("\"We are tracking ahead of plan while reinforcing a resilient customer community.\"")
                        .FontFamily("Helvetica")
                        .FontSize(12f)
                        .Italic()
                        .Color("#475569")
                        .LineHeight(1.3f)
                        .MarginTop(16f)
                        .Add();

                    new RichTextBuilder(col, ContentMargin, col.GetCurrentY() + 12f, contentWidth)
                        .Font("Helvetica", 11.5f)
                        .LineHeight(1.25f)
                        .Span("Chief Executive Officer: ").Bold().Color("#0F172A").EndSpan()
                        .Span("Amelia Reyes").Color("#1F2937").EndSpan()
                        .Span(" - Chief Revenue Officer: ").Bold().Color("#0F172A").EndSpan()
                        .Span("Theo Martin").Color("#1F2937").EndSpan()
                        .Add();

                    col.Text("Next Check-in: September 18 - Dallas HQ - Agenda: FY26 North Star Metrics")
                        .FontFamily("Helvetica")
                        .FontSize(11f)
                        .Color("#F8FAFC")
                        .BackgroundColor("#1C64F2")
                        .PaddingTop(10f)
                        .PaddingBottom(10f)
                        .PaddingLeft(16f)
                        .PaddingRight(16f)
                        .BackgroundCornerRadius(10f)
                        .MarginTop(24f)
                        .Add();
                });
        }

        private static string FormatDelta(float value)
        {
            var formatted = value.ToString("+0.0;-0.0;0.0", CultureInfo.InvariantCulture);
            return $"{formatted}%";
        }

        private static byte[]? GetBannerLogo()
        {
            if (_bannerLogo != null)
                return _bannerLogo;

            _bannerLogo = LoadImageBytes("./samples/aurora-banner.png")
                          ?? LoadImageBytes("./samples/banner.png");
            return _bannerLogo;
        }

        private static byte[]? GetSealLogo()
        {
            if (_sealLogo != null)
                return _sealLogo;

            _sealLogo = LoadImageBytes("./samples/aurora-seal.png")
                        ?? LoadImageBytes("./samples/seal.png");
            return _sealLogo;
        }

        private static byte[]? LoadImageBytes(string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
                return null;

            try
            {
                var full = Path.GetFullPath(relativePath, Directory.GetCurrentDirectory());
                return File.Exists(full) ? File.ReadAllBytes(full) : null;
            }
            catch
            {
                return null;
            }
        }

        private readonly record struct RevenueRow(string Segment, float Actual, float Plan, int Pulse);
    }
}
