// Comprehensive sample blueprint that exercises the PdfBuilder APIs introduced for
// watermark opacity, advanced table styling, rich text decorations, imaging effects,
// and chart rendering. Copy this into your application, remove the comment markers,
// and adapt the asset paths to match your environment.
//
// using System;
// using System.Collections.Generic;
// using System.Drawing;
// using PdfBuilder.Document;
// using PdfBuilder.Elements;
// using PdfBuilder.Models;
// using TableModels = PdfBuilder.Elements.Table;
//
// public static class SampleDocumentBlueprint
// {
//     public static PdfDocument Build()
//     {
//         var doc = new PdfDocument
//         {
//             Title = "PdfBuilder Premium Showcase"
//         };
//
//         var docBuilder = new PdfDocumentBuilder(doc)
//             .Title("PdfBuilder Premium Showcase")
//             .HeaderFooter(hf =>
//             {
//                 hf.HeaderTemplate = "{title}";
//                 hf.FooterTemplate = "Generated {date:yyyy-MM-dd HH:mm} — Page {page}/{pages}";
//                 hf.HeaderAlign = TextAlignment.Center;
//                 hf.FooterAlign = TextAlignment.Right;
//                 hf.FontFamily = "Helvetica";
//                 hf.FontSize = 9f;
//                 hf.Color = "#4A4D57";
//             })
//             .Master(master =>
//             {
//                 master.BackgroundColor = "#F4F6FB";
//                 master.Watermark = new WatermarkSpec
//                 {
//                     Text = "INTERNAL USE",
//                     FontFamily = "Helvetica",
//                     FontSize = 72f,
//                     Color = "#1C64F2",
//                     Opacity = 0.08f,
//                     RotationDegrees = 45f,
//                     Layer = WatermarkLayer.BehindContent
//                 };
//             });
//
//         // ----- Cover / Overview ----------------------------------------------------
//         var cover = doc.AddPage();
//         docBuilder.ApplySectionTo(cover);
//         new PdfPageBuilder(cover)
//             .Margin(60f)
//             .Content(col =>
//             {
//                 col.Anchor("overview").Title("Overview").Level(1).Add();
//
//                 col.Text("Welcome to the PdfBuilder premium showcase. This document demonstrates translucent watermarks, rich table styling, inline decorations, imaging effects, and dashboard-ready charts.")
//                    .FontFamily("Helvetica")
//                    .FontSize(14f)
//                    .LineHeight(1.35f)
//                    .MarginBottom(18f)
//                    .Add();
//
//                 new ListBuilder(col, cover.MarginLeft, col.GetCurrentY(), cover.Width - cover.MarginLeft - cover.MarginRight)
//                     .Marker(ListMarker.Decimal)
//                     .Item(new RichRun { Text = "Tables & Typography", LinkAnchor = "tables", Underline = true, Color = "#1C64F2" })
//                     .Item(new RichRun { Text = "Media Effects", LinkAnchor = "media", Underline = true, Color = "#1C64F2" })
//                     .Item(new RichRun { Text = "Analytics Dashboards", LinkAnchor = "charts", Underline = true, Color = "#1C64F2" })
//                     .Add();
//
//                 var hero = SafeReadAllBytes("./tests/PdfBuilder.Tests/bin/Debug/net9.0/logo.webp");
//                 if (hero != null)
//                 {
//                     col.Image(hero, cover.MarginLeft, col.GetCurrentY() + 16f, 220f, 110f)
//                        .CornerRadius(12f)
//                        .Shadow("#0B1E3B", 4f, -4f, 8f)
//                        .Opacity(0.9f)
//                        .Add();
//                 }
//             });
//
//         // ----- Tables & Typography -------------------------------------------------
//         var tables = doc.AddPage();
//         docBuilder.ApplySectionTo(tables);
//         new PdfPageBuilder(tables)
//             .Margin(48f)
//             .Content(col =>
//             {
//                 col.Anchor("tables").Title("Tables & Typography").Level(1).Add();
//
//                 col.Text("Tables now honor per-side borders, collapsed seam resolution, alternating row/column banding, inline runs with decorations, and rotated text.")
//                    .FontFamily("Helvetica")
//                    .FontSize(12f)
//                    .LineHeight(1.25f)
//                    .MarginBottom(14f)
//                    .Add();
//
//                 float tableWidth = tables.Width - tables.MarginLeft - tables.MarginRight;
//                 var tableBuilder = col.Table(tables.MarginLeft, col.GetCurrentY(), tableWidth, 0f)
//                     .Caption("Revenue Breakdown FY25")
//                     .DefaultFont("Helvetica")
//                     .DefaultFontSize(10f)
//                     .CellPadding(6f)
//                     .Border(Color.FromArgb(28, 100, 242), 0.5f)
//                     .EnablePageBreaks(true)
//                     .RepeatHeaders(true);
//
//                 tableBuilder.HeaderRow(
//                     c => c.Text("Region").Bold().BorderBottom(Color.FromArgb(28, 100, 242), 1.25f),
//                     c => c.Text("YoY Growth").Bold().AlignRight().BorderBottom(Color.FromArgb(28, 100, 242), 1.25f),
//                     c => c.Text("FY24 Revenue").Bold().AlignRight().BorderBottom(Color.FromArgb(28, 100, 242), 1.25f),
//                     c => c.Text("Narrative").Bold().BorderBottom(Color.FromArgb(28, 100, 242), 1.25f)
//                 );
//
//                 var rows = new[]
//                 {
//                     new { Region = "North America", Growth = "18%", Fy24 = "$22.4M", Trend = "SaaS expansion and enterprise upsell momentum." },
//                     new { Region = "EMEA",          Growth = "11%", Fy24 = "$17.9M", Trend = "Renewals solid, fintech pipeline accelerating." },
//                     new { Region = "APAC",          Growth = "24%", Fy24 = "$13.2M", Trend = "Net-new logos plus marketplace integrations." },
//                     new { Region = "LATAM",         Growth = "7%",  Fy24 = "$6.8M",  Trend = "Retail demand offsets currency pressure." }
//                 };
//
//                 foreach (var row in rows)
//                 {
//                     tableBuilder.Row(
//                         c => c.Text(row.Region),
//                         c => c.Text(row.Growth).AlignRight(),
//                         c => c.Text(row.Fy24).AlignRight(),
//                         c => c.Text(row.Trend).LineHeight(1.2f)
//                     );
//                 }
//
//                 tableBuilder.FooterRow(
//                     c => c.Text("Global Forecast").Bold().ColSpan(2),
//                     c =>
//                     {
//                         c.Text("$69.1M").Bold().AlignRight();
//                         c.BorderTop(Color.FromArgb(28, 100, 242), 1.25f);
//                     },
//                     c => c.Text("Confidence: High").AlignRight()
//                 );
//
//                 var tableElement = tableBuilder.Build();
//                 tableElement.BorderCollapse = TableModels.BorderCollapseMode.Collapse;
//                 tableElement.ResolveBorderConflicts = true;
//                 tableElement.OuterBorder = new TableModels.BorderStyle
//                 {
//                     Color = Color.FromArgb(28, 100, 242),
//                     Width = 1.25f
//                 };
//                 tableElement.InnerBorder = new TableModels.BorderStyle
//                 {
//                     Color = Color.FromArgb(198, 207, 224),
//                     Width = 0.5f
//                 };
//                 tableElement.RowBanding = new TableModels.RowBandingSpec
//                 {
//                     Step = 2,
//                     Fills = new List<TableModels.BandFill>
//                     {
//                         new TableModels.BandFill { FillColor = Color.FromArgb(244, 249, 255) },
//                         new TableModels.BandFill { FillColor = null }
//                     }
//                 };
//                 tableElement.ColumnBanding = new TableModels.ColumnBandingSpec
//                 {
//                     Step = 2,
//                     Fills = new List<TableModels.BandFill>
//                     {
//                         new TableModels.BandFill { FillColor = null },
//                         new TableModels.BandFill { FillColor = Color.FromArgb(247, 252, 255) }
//                     }
//                 };
//
//                 // Inline rich runs + decorations on first body row
//                 if (tableElement.Rows.Count > 1)
//                 {
//                     var growthCell = tableElement.Rows[1].Cells[1];
//                     growthCell.Text = string.Empty;
//                     growthCell.HorizontalAlign = HorizontalAlign.Right;
//                     growthCell.TextRuns.AddRange(new[]
//                     {
//                         new TableModels.InlineRun
//                         {
//                             Text = "18",
//                             Style = new TableModels.TextStyle
//                             {
//                                 FontFamily = "Helvetica",
//                                 FontSize = 10f,
//                                 Bold = true,
//                                 TextColor = Color.FromArgb(28, 100, 242)
//                             }
//                         },
//                         new TableModels.InlineRun
//                         {
//                             Text = "% YoY",
//                             Style = new TableModels.TextStyle
//                             {
//                                 FontFamily = "Helvetica",
//                                 FontSize = 9f,
//                                 TextColor = Color.FromArgb(88, 101, 242)
//                             }
//                         }
//                     });
//
//                     var narrative = tableElement.Rows[1].Cells[3];
//                     narrative.Text = string.Empty;
//                     narrative.TextRuns.AddRange(new[]
//                     {
//                         new TableModels.InlineRun
//                         {
//                             Text = "SaaS expansion ",
//                             Style = new TableModels.TextStyle
//                             {
//                                 FontFamily = "Helvetica",
//                                 FontSize = 9f,
//                                 LineHeight = 1.2f
//                             }
//                         },
//                         new TableModels.InlineRun
//                         {
//                             Text = "(was 12%)",
//                             Style = new TableModels.TextStyle
//                             {
//                                 FontFamily = "Helvetica",
//                                 FontSize = 9f,
//                                 Strikethrough = true,
//                                 TextColor = Color.FromArgb(190, 18, 60),
//                                 DecorationStyle = TableModels.TextDecorationStyle.Dashed
//                             }
//                         }
//                     });
//                 }
//
//                 // Per-side borders on LATAM row
//                 if (tableElement.Rows.Count > 4)
//                 {
//                     var latam = tableElement.Rows[4].Cells[0];
//                     latam.BorderLeft = true;
//                     latam.BorderColorLeft = Color.FromArgb(12, 166, 120);
//                     latam.BorderWidthLeft = 1.5f;
//                     latam.BorderTop = true;
//                     latam.BorderColorTop = Color.FromArgb(12, 166, 120);
//                     latam.BorderWidthTop = 1.5f;
//                 }
//
//                 tableBuilder.Add();
//
//                 // Rich text callout with strike/underline mix
//                 new RichTextBuilder(col, tables.MarginLeft, col.GetCurrentY() - 10f, tableWidth)
//                     .Font("Helvetica", 11f)
//                     .LineHeight(1.2f)
//                     .Span("Margin trend: ").Bold().Color("#344054").EndSpan()
//                     .Span("18%").Bold().Color("#0EA472").EndSpan()
//                     .Span(" → ").EndSpan()
//                     .Span("22%").Strike().Color("#BE123C").EndSpan()
//                     .Span(" target by Q4").EndSpan()
//                     .Add();
//             });
//
//         // ----- Media effects -------------------------------------------------------
//         var media = doc.AddPage();
//         docBuilder.ApplySectionTo(media);
//         new PdfPageBuilder(media)
//             .Margin(48f)
//             .Content(col =>
//             {
//                 col.Anchor("media").Title("Media Effects").Level(1).Add();
//
//                 col.Text("Image builder supports rounded clipping, ellipse masks, opacity, and drop shadows.")
//                    .FontFamily("Helvetica")
//                    .FontSize(12f)
//                    .LineHeight(1.25f)
//                    .MarginBottom(14f)
//                    .Add();
//
//                 var fish = SafeReadAllBytes("./tests/PdfBuilder.Tests/bin/Debug/net9.0/fish.jpeg");
//                 if (fish != null)
//                 {
//                     col.Image(fish, media.MarginLeft, col.GetCurrentY(), 120f, 120f)
//                        .Clip(ImageClipShape.Circle)
//                        .Border("#0A58CA", 3f)
//                        .Shadow("#1F2937", 3f, -3f, 7f)
//                        .Add();
//
//                     col.Image(fish, media.MarginLeft + 160f, col.GetCurrentY(), 160f, 110f)
//                        .ClipEllipse(EllipseOrientation.Horizontal, 0.5f)
//                        .CornerRadius(8f)
//                        .Opacity(0.85f)
//                        .Shadow("#111827", 6f, -2f, 10f)
//                        .Add();
//                 }
//             });
//
//         // ----- Analytics / Charts --------------------------------------------------
//         var charts = doc.AddPage();
//         docBuilder.ApplySectionTo(charts);
//         new PdfPageBuilder(charts)
//             .Margin(48f)
//             .Content(col =>
//             {
//                 col.Anchor("charts").Title("Analytics Dashboards").Level(1).Add();
//
//                 col.Text("Combine column and line series, gantt schedules, and custom palettes without leaving the fluent API.")
//                    .FontFamily("Helvetica")
//                    .FontSize(12f)
//                    .LineHeight(1.25f)
//                    .MarginBottom(12f)
//                    .Add();
//
//                 float chartWidth = charts.Width - charts.MarginLeft - charts.MarginRight;
//                 col.Chart(charts.MarginLeft, col.GetCurrentY(), chartWidth, 220f)
//                     .Title("Net Revenue vs Target")
//                     .TitleFont("Helvetica-Bold", 12f)
//                     .NumericX(0f, 3f, 4, v => new[] { "Q1", "Q2", "Q3", "Q4" }[(int)v])
//                     .NumericY(0f, 35f, 8, v => $"{v:0}M")
//                     .GridY(true)
//                     .AddBars("Actual", Color.FromArgb(28, 100, 242), Color.FromArgb(16, 92, 200), 0.5f, 18f, 21f, 26f, 29f)
//                     .AddLine("Target", Color.FromArgb(12, 166, 120), 1.5f, 20f, 22f, 28f, 32f)
//                     .Legend(true)
//                     .Add();
//
//                 col.Chart(charts.MarginLeft, col.GetCurrentY() + 16f, chartWidth, 240f)
//                     .Title("Launch Roadmap")
//                     .TitleFont("Helvetica-Bold", 12f)
//                     .CategoryX("Design", "Build", "QA", "Launch")
//                     .NumericX(0f, 12f, 7, v => $"W{v:0}")
//                     .AddGantt("Plan", Color.FromArgb(58, 64, 94), 0.6f, rowGap: 2f, barHeightRatio: 0.6f)
//                     .GanttTask(0, 0f, 2.5f, "Design", Color.FromArgb(226, 232, 240), Color.FromArgb(58, 64, 94))
//                     .GanttTask(1, 2f, 8f, "Build", Color.FromArgb(191, 219, 254), Color.FromArgb(28, 100, 242))
//                     .GanttTask(2, 7.5f, 10.5f, "QA", Color.FromArgb(209, 250, 229), Color.FromArgb(12, 166, 120))
//                     .GanttTask(3, 10f, 12f, "Launch", Color.FromArgb(254, 226, 226), Color.FromArgb(190, 18, 60))
//                     .Legend(false)
//                     .Add();
//             });
//
//         return doc;
//     }
//
//     private static byte[]? SafeReadAllBytes(string path)
//     {
//         try
//         {
//             return System.IO.File.Exists(path) ? System.IO.File.ReadAllBytes(path) : null;
//         }
//         catch
//         {
//             return null;
//         }
//     }
// }
