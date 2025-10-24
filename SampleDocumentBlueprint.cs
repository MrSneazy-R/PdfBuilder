using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using PdfBuilder.Document;
using PdfBuilder.Document.Layout;
using PdfBuilder.Document.Layout.Components;
using PdfBuilder.Elements.Table;
using PdfBuilder.Models;

/// <summary>
/// Demonstrates how to build an invoice-style PDF using PdfBuilder.
/// This is a parity port of a QuestPDF sample that renders a dark header invoice.
/// </summary>
public static class SampleDocumentBlueprint
{
    public static PdfDocument CreateInvoiceSample() =>
        CreateInvoiceSample(InvoiceSampleData.CreateDemo());

    public static PdfDocument CreateInvoiceSample(InvoiceSampleData data)
    {
        if (data is null)
            throw new ArgumentNullException(nameof(data));

        var culture = data.Culture ?? CultureInfo.InvariantCulture;
        var doc = new PdfDocument
        {
            Title = string.IsNullOrWhiteSpace(data.InvoiceNumber)
                ? "Invoice"
                : $"Invoice {data.InvoiceNumber}"
        };

        new PdfDocumentBuilder(doc)
            .DefaultContentMargin(28)
            .DefaultTextStyle(style =>
            {
                style.FontFamily = data.Branding.FontFamily ?? "Helvetica";
                style.FontSize = 10f;
                style.Color = "#1F2933";
                style.LineHeight = 1.35f;
            })
            .Compose(document =>
            {
                document.Page(page =>
                {
                    page.Margin(28);
                    page.DefaultTextStyle(style =>
                    {
                        style.FontFamily = data.Branding.FontFamily ?? "Helvetica";
                        style.FontSize = 10f;
                        style.Color = "#1F2933";
                        style.LineHeight = 1.35f;
                    });

                    page.Footer(content =>
                        content.Align(LayoutHorizontalAlignment.Center, LayoutVerticalAlignment.Middle, inner =>
                            inner.Text(data.FooterNote ?? "Thank you for your business", text =>
                            {
                                text.Color = "#6B7280";
                                text.FontSize = 9f;
                            })));

                    page.Column(column =>
                    {
                        RenderHeader(column, data, culture);
                        RenderBillTo(column, data);
                        RenderItemsTable(column, data, culture);
                        RenderSummary(column, data, culture);
                        RenderTerms(column, data);
                    });
                });
            });

        return doc;
    }

    private static void RenderHeader(ColumnBuilder column, InvoiceSampleData data, CultureInfo culture)
    {
        string darkHex = string.IsNullOrWhiteSpace(data.Branding.DarkColor) ? "#1F2933" : data.Branding.DarkColor!;
        string accentHex = string.IsNullOrWhiteSpace(data.Branding.PrimaryColor) ? "#2563EB" : data.Branding.PrimaryColor!;
        var issueDate = data.IssueDate.HasValue
            ? data.IssueDate.Value.ToString("dd MMM yyyy", culture)
            : "(unspecified)";
        var dueDate = data.DueDate.HasValue
            ? data.DueDate.Value.ToString("dd MMM yyyy", culture)
            : null;
        var invoiceNo = string.IsNullOrWhiteSpace(data.InvoiceNumber) ? "(Draft)" : data.InvoiceNumber;

        column.ComposeContent(flow =>
        {
            flow.Background(darkHex, header =>
            {
                header.Padding(16, padded =>
                {
                    padded.Row(row =>
                    {
                        row.Relative(1f, left =>
                        {
                            left.Column(col =>
                            {
                                col.Spacing(4);
                                if (!string.IsNullOrWhiteSpace(data.Branding.CompanyName))
                                {
                                    col.Item(item => item.Text(data.Branding.CompanyName!, text =>
                                    {
                                        text.Color = "#FFFFFF";
                                        text.FontSize = 12f;
                                        text.Bold = true;
                                    }));
                                }

                                col.Item(item => item.Text("INVOICE", text =>
                                {
                                    text.Color = "#FFFFFF";
                                    text.FontSize = 18f;
                                    text.Bold = true;
                                }));
                            });
                        });

                        row.Constant(240f, right =>
                        {
                            right.Column(col =>
                            {
                                col.Spacing(4);
                                AddHeaderLine(col, "Invoice No:", invoiceNo);
                                AddHeaderLine(col, "Date:", issueDate);
                                if (!string.IsNullOrWhiteSpace(dueDate))
                                    AddHeaderLine(col, "Due:", dueDate!);
                            });
                        });
                    });
                });
            });

            // Primary accent underline
            flow.Padding(0, 0, 0, 0, inner =>
            {
                inner.MinHeight(4f, underline =>
                    underline.Background(accentHex, block => { }));
            });
        });
    }

    private static void AddHeaderLine(LayoutComponentCollection.ColumnComponentBuilder col, string label, string value)
    {
        col.Item(item =>
        {
            item.Text(string.Empty, text =>
            {
                text.Alignment = TextAlignment.Right;
                text.Color = "#FFFFFF";
                text.Spans.Add(new TextSpan
                {
                    Text = $"{label} ",
                    Bold = true
                });
                text.Spans.Add(new TextSpan
                {
                    Text = value
                });
            });
        });
    }

    private static void RenderBillTo(ColumnBuilder column, InvoiceSampleData data)
    {
        string accentHex = string.IsNullOrWhiteSpace(data.Branding.PrimaryColor) ? "#2563EB" : data.Branding.PrimaryColor!;

        column.Text("Bill To")
            .MarginTop(20f)
            .Bold()
            .Color(accentHex)
            .Add();

        var block = column.Text(string.Empty);
        block.MarginTop(4f);
        block.LineHeight(1.4f);
        if (!string.IsNullOrWhiteSpace(data.Customer.DisplayName))
            block.Span(data.Customer.DisplayName!, span => span.Bold = true);
        if (!string.IsNullOrWhiteSpace(data.Customer.BillingAddress))
            block.Span("\n" + data.Customer.BillingAddress);
        if (!string.IsNullOrWhiteSpace(data.Customer.Email))
            block.Span("\n" + data.Customer.Email);
        block.Add();
    }

    private static void RenderItemsTable(ColumnBuilder column, InvoiceSampleData data, CultureInfo culture)
    {
        AddGap(column, 16f);

        var flow = column.GetFlow();
        var table = column.Table(flow.X, flow.Y, flow.Width, 0f);
        table.TableWidth(flow.Width);
        table.CellPadding(6f);
        table.Border("#E5E7EB", 0.75f);
        table.ColumnLayout(
            TableColumn.Relative(1.2f, minWidth: 32f),
            TableColumn.Relative(6f, minWidth: 180f),
            TableColumn.Relative(2f, minWidth: 72f),
            TableColumn.Relative(2f, minWidth: 58f),
            TableColumn.Relative(2f, minWidth: 82f));

        string headerBg = "#E5E7EB";
        table.HeaderRow(
            cell => HeaderCell(cell, "NO", headerBg),
            cell => HeaderCell(cell, "PRODUCT DESCRIPTION", headerBg),
            cell => HeaderCell(cell, "UNIT PRICE", headerBg).AlignRight(),
            cell => HeaderCell(cell, "QTY", headerBg).AlignRight(),
            cell => HeaderCell(cell, "TOTAL", headerBg).AlignRight());

        bool useAlternate = false;
        if (data.Items.Count == 0)
        {
            table.Row(
                c => c.Text("1").Padding(6f),
                c => c.Text("Sample service").Padding(6f),
                c => c.Text(FormatMoney(199m, data, culture)).Padding(6f).AlignRight(),
                c => c.Text("1").Padding(6f).AlignRight(),
                c => c.Text(FormatMoney(199m, data, culture)).Padding(6f).AlignRight());
        }
        else
        {
            int index = 1;
            foreach (var item in data.Items)
            {
                var total = item.Total ?? item.UnitPrice * item.Quantity;
                table.Row(row =>
                {
                    if (useAlternate)
                        row.Background(ParseColor("#F9FAFB"));
                    row.Cells(
                        c => c.Text(index.ToString(culture)).Padding(6f),
                        c => c.Text(item.Description ?? string.Empty).Padding(6f),
                        c => c.Text(FormatMoney(item.UnitPrice, data, culture)).Padding(6f).AlignRight(),
                        c => c.Text(item.Quantity.ToString("0.##", culture)).Padding(6f).AlignRight(),
                        c => c.Text(FormatMoney(total, data, culture)).Padding(6f).AlignRight());
                });
                index++;
                useAlternate = !useAlternate;
            }
        }

        table.Add();
    }

    private static void RenderSummary(ColumnBuilder column, InvoiceSampleData data, CultureInfo culture)
    {
        decimal subtotal = data.Subtotal ?? data.Items.Sum(i => (i.Total ?? (i.UnitPrice * i.Quantity)));
        decimal tax = data.TaxTotal ?? (data.TaxRate.HasValue ? Math.Round(subtotal * data.TaxRate.Value, 2) : 0m);
        decimal total = data.Total ?? (subtotal + tax);

        AddGap(column, 12f);

        var flow = column.GetFlow();
        float summaryWidth = Math.Min(340f, flow.Width);
        float summaryX = flow.X + flow.Width - summaryWidth;

        var summary = column.Table(summaryX, column.GetCurrentY(), summaryWidth, 0f);
        summary.TableWidth(summaryWidth);
        summary.CellPadding(6f);
        summary.Border("#E5E7EB", 0.75f);

        summary.Row(
            cells => cells
                .Cells(
                    c => c.Text("Sub Total"),
                    c => c.Text(FormatMoney(subtotal, data, culture)).AlignRight()));

        summary.Row(
            cells => cells
                .Cells(
                    c => c.Text("Tax"),
                    c => c.Text(FormatMoney(tax, data, culture)).AlignRight()));

        summary.Row(row =>
        {
            row.Background(ParseColor(string.IsNullOrWhiteSpace(data.Branding.DarkColor) ? "#1F2933" : data.Branding.DarkColor!));
            row.Cells(
                c => c.Text("Grand Total").Bold().TextColor(Color.White),
                c => c.Text(FormatMoney(total, data, culture)).Bold().TextColor(Color.White).AlignRight());
        });

        summary.Add();
    }

    private static void RenderTerms(ColumnBuilder column, InvoiceSampleData data)
    {
        if (string.IsNullOrWhiteSpace(data.Branding.Terms))
            return;

        AddGap(column, 18f);

        column.Text("Terms & Condition")
            .Bold()
            .Add();

        column.Text(data.Branding.Terms!)
            .MarginTop(4f)
            .LineHeight(1.35f)
            .Add();
    }

    private static void AddGap(ColumnBuilder column, float points)
    {
        column.Text(" ")
            .FontSize(1f)
            .MarginTop(points)
            .Add();
    }

    private static TableBuilder.TableCellBuilder HeaderCell(TableBuilder.TableCellBuilder cell, string text, string backgroundHex)
    {
        cell.Text(text)
            .Bold()
            .Background(ParseColor(backgroundHex))
            .Padding(6f);
        return cell;
    }

    private static string FormatMoney(decimal value, InvoiceSampleData data, CultureInfo culture)
    {
        string formatted = value.ToString("N2", culture);
        return string.IsNullOrEmpty(data.CurrencySymbol)
            ? formatted
            : $"{data.CurrencySymbol}{formatted}";
    }

    private static Color ParseColor(string hex) =>
        ColorTranslator.FromHtml(string.IsNullOrWhiteSpace(hex) ? "#000000" : hex);

    #region Sample Data Models

    public sealed record InvoiceSampleData(
        BrandingInfo Branding,
        CustomerInfo Customer,
        IReadOnlyList<InvoiceItem> Items)
    {
        public string? InvoiceNumber { get; init; }
        public DateTime? IssueDate { get; init; }
        public DateTime? DueDate { get; init; }
        public decimal? Subtotal { get; init; }
        public decimal? TaxTotal { get; init; }
        public decimal? Total { get; init; }
        public decimal? TaxRate { get; init; }
        public string CurrencySymbol { get; init; } = "$";
        public CultureInfo? Culture { get; init; }
        public string? FooterNote { get; init; }

        public static InvoiceSampleData CreateDemo()
        {
            var items = new[]
            {
                new InvoiceItem("Discovery & Research", 120m, 10m),
                new InvoiceItem("UX/UI Design", 95m, 18.5m),
                new InvoiceItem("Frontend Implementation", 105m, 24m),
                new InvoiceItem("Backend Integration", 115m, 16m)
            };

            return new InvoiceSampleData(
                new BrandingInfo
                {
                    CompanyName = "Aurora Dynamics",
                    PrimaryColor = "#2563EB",
                    DarkColor = "#111827",
                    Terms = "Payment is due within 15 days of the invoice date. Late payments may be subject to a 5% fee.",
                    FontFamily = "Helvetica"
                },
                new CustomerInfo
                {
                    DisplayName = "Summit Retail Group",
                    BillingAddress = "482 Market Street\nSan Francisco, CA 94105",
                    Email = "accounts@summitretail.example"
                },
                items)
            {
                InvoiceNumber = "INV-1042",
                IssueDate = new DateTime(2025, 7, 9),
                DueDate = new DateTime(2025, 7, 24),
                TaxRate = 0.0825m,
                Culture = CultureInfo.GetCultureInfo("en-US"),
                FooterNote = "Thank you for choosing Aurora Dynamics."
            };
        }
    }

    public sealed record BrandingInfo
    {
        public string? CompanyName { get; init; }
        public string? PrimaryColor { get; init; }
        public string? DarkColor { get; init; }
        public string? Terms { get; init; }
        public string? FontFamily { get; init; }
    }

    public sealed record CustomerInfo
    {
        public string? DisplayName { get; init; }
        public string? BillingAddress { get; init; }
        public string? Email { get; init; }
    }

    public sealed record InvoiceItem(string Description, decimal UnitPrice, decimal Quantity)
    {
        public decimal? Total { get; init; }
    }

    #endregion
}
