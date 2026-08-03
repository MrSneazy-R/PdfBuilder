////[2025-10-24T15:23:10.619Z] Error: System.InvalidOperationException: Component could not be placed because it repeatedly reported a wrap result after 33 page/column breaks Details: component=DecorationComponent, page=1, columnIndex=1, columns=1, columnWidth=532pt, availableHeight=40pt, cursorY=792pt, attempts=33, limit=32, result=Wrap, usedWidth=532pt. Enable LayoutOptions.Debug.TraceLayout or DrawBoundingBoxes for diagnostics.
////   at PdfBuilder.Document.ColumnBuilder.AddComponent(IMeasurable component) in J:\Jinx\PdfBuilder\Document\ColumnBuilder.cs:line 499
////   at PdfBuilder.Document.ColumnBuilder.ComposeContent(Action`1 configure) in J:\Jinx\PdfBuilder\Document\ColumnBuilder.cs:line 421
////   at PdfBuilder.Document.HeaderFooterLayoutComposer.Render(HeaderFooterLayoutDefinition layout, Boolean isHeader, PdfPage page, HeaderFooterSpec spec) in J:\Jinx\PdfBuilder\Document\HeaderFooterLayoutComposer.cs:line 100
////   at PdfBuilder.Document.HeaderFooterLayoutComposer.Prepare(PdfDocument document, DateTime timestampUtc) in J:\Jinx\PdfBuilder\Document\HeaderFooterLayoutComposer.cs:line 31
////   at PdfBuilder.Writer.PdfWriter.WriteDocument(PdfDocument doc, Stream destination, DateTime nowUtc) in J:\Jinx\PdfBuilder\Writer\PdfWriter.cs:line 61
////   at PdfBuilder.Writer.PdfWriter.GenerateBytes(PdfDocument doc) in J:\Jinx\PdfBuilder\Writer\PdfWriter.cs:line 26
////   at BLSMainSite2025.Components.Pages.TestPage.ShowPdf(PdfDocument doc) in J:\Jinx\BLSMainSite2025\BLSMainSite2025\Components\Pages\TestPage.razor:line 100
////   at BLSMainSite2025.Components.Pages.TestPage.GenerateTextPdf() in J:\Jinx\BLSMainSite2025\BLSMainSite2025\Components\Pages\TestPage.razor:line 70
////   at Microsoft.AspNetCore.Components.ComponentBase.CallStateHasChangedOnAsyncCompletion(Task task)
////   at Microsoft.AspNetCore.Components.RenderTree.Renderer.GetErrorHandledTask(Task taskToHandle, ComponentState owningComponentState)[2025-10-24T15:23:10.619Z] Error: System.InvalidOperationException: Component could not be placed because it repeatedly reported a wrap result after 33 page/column breaks Details: component=DecorationComponent, page=1, columnIndex=1, columns=1, columnWidth=532pt, availableHeight=40pt, cursorY=792pt, attempts=33, limit=32, result=Wrap, usedWidth=532pt. Enable LayoutOptions.Debug.TraceLayout or DrawBoundingBoxes for diagnostics.
////   at PdfBuilder.Document.ColumnBuilder.AddComponent(IMeasurable component) in J:\Jinx\PdfBuilder\Document\ColumnBuilder.cs:line 499
////   at PdfBuilder.Document.ColumnBuilder.ComposeContent(Action`1 configure) in J:\Jinx\PdfBuilder\Document\ColumnBuilder.cs:line 421
////   at PdfBuilder.Document.HeaderFooterLayoutComposer.Render(HeaderFooterLayoutDefinition layout, Boolean isHeader, PdfPage page, HeaderFooterSpec spec) in J:\Jinx\PdfBuilder\Document\HeaderFooterLayoutComposer.cs:line 100
////   at PdfBuilder.Document.HeaderFooterLayoutComposer.Prepare(PdfDocument document, DateTime timestampUtc) in J:\Jinx\PdfBuilder\Document\HeaderFooterLayoutComposer.cs:line 31
////   at PdfBuilder.Writer.PdfWriter.WriteDocument(PdfDocument doc, Stream destination, DateTime nowUtc) in J:\Jinx\PdfBuilder\Writer\PdfWriter.cs:line 61
////   at PdfBuilder.Writer.PdfWriter.GenerateBytes(PdfDocument doc) in J:\Jinx\PdfBuilder\Writer\PdfWriter.cs:line 26
////   at BLSMainSite2025.Components.Pages.TestPage.ShowPdf(PdfDocument doc) in J:\Jinx\BLSMainSite2025\BLSMainSite2025\Components\Pages\TestPage.razor:line 100
////   at BLSMainSite2025.Components.Pages.TestPage.GenerateTextPdf() in J:\Jinx\BLSMainSite2025\BLSMainSite2025\Components\Pages\TestPage.razor:line 70
////   at Microsoft.AspNetCore.Components.ComponentBase.CallStateHasChangedOnAsyncCompletion(Task task)
////   at Microsoft.AspNetCore.Components.RenderTree.Renderer.GetErrorHandledTask(Task taskToHandle, ComponentState owningComponentState)


//public static PdfDocument CreateTextOnly()
//{
//    // ---- Demo data (inline) ----
//    var culture = CultureInfo.GetCultureInfo("en-ZA"); // South Africa format
//    string currencySymbol = "R ";

//    string companyName = "Aurora Dynamics";
//    string fontFamily = "Helvetica";
//    string primaryHex = "#2563EB";
//    string darkHex = "#111827";
//    string? terms = "Payment is due within 15 days of the invoice date. Late payments may be subject to a 5% fee.";
//    string footerNote = "Thank you for choosing Aurora Dynamics.";

//    string invoiceNumber = "INV-1042";
//    DateTime issueDate = new DateTime(2025, 7, 9);
//    DateTime dueDate = new DateTime(2025, 7, 24);

//    string billToName = "Summit Retail Group";
//    string billToAddress = "482 Market Street\nSan Francisco, CA 94105";
//    string billToEmail = "accounts@summitretail.example";

//    var items = new (string Description, decimal UnitPrice, decimal Quantity)[]
//    {
//            ("Discovery & Research",    120m, 10m),
//            ("UX/UI Design",             95m, 18.5m),
//            ("Frontend Implementation", 105m, 24m),
//            ("Backend Integration",     115m, 16m)
//    };

//    decimal taxRate = 0.15m; // 15% VAT example for ZA
//    decimal subtotal = items.Sum(i => i.UnitPrice * i.Quantity);
//    decimal tax = Math.Round(subtotal * taxRate, 2);
//    decimal grandTotal = subtotal + tax;

//    // Try load logo (optional)
//    byte[]? logo = null;
//    try { logo = System.IO.File.ReadAllBytes("wwwroot/bls-logo.png"); } catch { /* ignore */ }

//    // ---- Document ----
//    var doc = new PdfDocument { Title = $"Invoice {invoiceNumber}" };
//    // Uncomment while tuning:
//    doc.LayoutOptions.Debug.TraceLayout = true;
//    doc.LayoutOptions.Debug.DrawBoundingBoxes = true;

//    new PdfDocumentBuilder(doc)
//        .HeaderFooter(hf =>
//        {
//            hf.HeaderHeight = 112f;  // plenty of room for the band + lines
//            hf.FooterHeight = 28f;
//        })
//        .DefaultTextStyle(s =>
//        {
//            s.FontFamily = fontFamily;
//            s.FontSize = 10f;
//            s.Color = "#1F2933";
//            s.LineHeight = 1.35f;
//        })
//        .Compose(d =>
//        {
//            d.Page(p =>
//            {
//                // Keep page margins (body column). Header/footer heights are separate.
//                p.Margin(28);
//                p.DefaultTextStyle(s =>
//                {
//                    s.FontFamily = fontFamily;
//                    s.FontSize = 10f;
//                    s.Color = "#1F2933";
//                    s.LineHeight = 1.35f;
//                });

//                // ===== HEADER (repeat) =====
//                p.Header(flow =>
//                {
//                    // One decoration paints both the dark band and the 4pt accent underline.
//                    flow.Decorate(deco =>
//                    {
//                        deco.Background(ctx =>
//                        {
//                            var r = ctx.Rect;
//                            // Dark band fills child height
//                            ctx.Page.AddElement(new DebugRectangleElement(r.X, r.Bottom, r.Width, r.Height)
//                            {
//                                FillColor = darkHex,
//                                StrokeWidth = 0f
//                            });
//                            // Accent underline at the bottom
//                            ctx.Page.AddElement(new DebugRectangleElement(r.X, r.Bottom, r.Width, 4f)
//                            {
//                                FillColor = primaryHex,
//                                StrokeWidth = 0f
//                            });
//                        });
//                    },
//                    content =>
//                    {
//                        // Fix the child height so measure is stable and < HeaderHeight.
//                        content.Height(72f, h =>
//                        {
//                            h.Padding(16, padded =>
//                            {
//                                padded.Row(row =>
//                                {
//                                    // Left: logo + company + INVOICE
//                                    row.Relative(1f, left =>
//                                    {
//                                        left.Column(col =>
//                                        {
//                                            col.Spacing(6);

//                                            if (logo is { Length: > 0 })
//                                            {
//                                                col.Item(i => i.Image(logo, 96f, 28f, img =>
//                                                {
//                                                    img.MarginBottom = 6;
//                                                    img.CornerRadius = 4;
//                                                }));
//                                            }

//                                            col.Item(i => i.Text(companyName, t =>
//                                            {
//                                                t.Color = "#FFFFFF";
//                                                t.FontSize = 12f;
//                                                t.Bold = true;
//                                            }));

//                                            col.Item(i => i.Text("INVOICE", t =>
//                                            {
//                                                t.Color = "#FFFFFF";
//                                                t.FontSize = 18f;
//                                                t.Bold = true;
//                                            }));
//                                        });
//                                    });

//                                    // Right: meta (right aligned)
//                                    row.Constant(240f, right =>
//                                    {
//                                        right.Column(col =>
//                                        {
//                                            col.Spacing(4);
//                                            col.Item(i => i.Text($"Invoice No: {invoiceNumber}", t => { t.Alignment = TextAlignment.Right; t.Color = "#FFFFFF"; }));
//                                            col.Item(i => i.Text($"Date: {issueDate:dd MMM yyyy}", t => { t.Alignment = TextAlignment.Right; t.Color = "#FFFFFF"; }));
//                                            col.Item(i => i.Text($"Due:  {dueDate:dd MMM yyyy}", t => { t.Alignment = TextAlignment.Right; t.Color = "#FFFFFF"; }));
//                                        });
//                                    });
//                                });
//                            });
//                        });
//                    });
//                });

//                // ===== FOOTER =====
//                p.Footer(content =>
//                    content.Align(LayoutHorizontalAlignment.Center, LayoutVerticalAlignment.Middle, inner =>
//                        inner.Text(footerNote, t =>
//                        {
//                            t.Color = "#6B7280";
//                            t.FontSize = 9f;
//                        })));

//                // ===== BODY =====
//                p.Column(column =>
//                {
//                    // BILL TO
//                    column.Text("Bill To").MarginTop(20f).Bold().Color(primaryHex).Add();

//                    var bill = column.Text(string.Empty);
//                    bill.MarginTop(4f);
//                    bill.LineHeight(1.4f);
//                    bill.Span(billToName, s => s.Bold = true);
//                    bill.Span("\n" + billToAddress);
//                    bill.Span("\n" + billToEmail);
//                    bill.Add();

//                    // Items table
//                    column.Text(" ").FontSize(1f).MarginTop(16f).Add(); // gap

//                    var flow1 = column.GetFlow();
//                    var table = column.Table(flow1.X, flow1.Y, flow1.Width, 0f);
//                    table.TableWidth(flow1.Width);
//                    table.CellPadding(6f);
//                    table.Border("#E5E7EB", 0.75f);
//                    table.ColumnLayout(
//                        TableColumn.Relative(1.2f, minWidth: 32f),
//                        TableColumn.Relative(6f, minWidth: 180f),
//                        TableColumn.Relative(2f, minWidth: 72f),
//                        TableColumn.Relative(2f, minWidth: 58f),
//                        TableColumn.Relative(2f, minWidth: 82f));

//                    string headerBg = "#E5E7EB";
//                    table.HeaderRow(
//                        c => c.Text("NO").Bold().Background(ColorTranslator.FromHtml(headerBg)).Padding(6f),
//                        c => c.Text("PRODUCT DESCRIPTION").Bold().Background(ColorTranslator.FromHtml(headerBg)).Padding(6f),
//                        c => c.Text("UNIT PRICE").Bold().Background(ColorTranslator.FromHtml(headerBg)).Padding(6f).AlignRight(),
//                        c => c.Text("QTY").Bold().Background(ColorTranslator.FromHtml(headerBg)).Padding(6f).AlignRight(),
//                        c => c.Text("TOTAL").Bold().Background(ColorTranslator.FromHtml(headerBg)).Padding(6f).AlignRight());

//                    bool alt = false;
//                    for (int i = 0; i < items.Length; i++)
//                    {
//                        var it = items[i];
//                        var total = it.UnitPrice * it.Quantity;
//                        string up = currencySymbol + it.UnitPrice.ToString("N2", culture);
//                        string qt = it.Quantity.ToString("0.##", culture);
//                        string tt = currencySymbol + total.ToString("N2", culture);

//                        table.Row(r =>
//                        {
//                            if (alt) r.Background(ColorTranslator.FromHtml("#F9FAFB"));
//                            r.Cells(
//                                c => c.Text((i + 1).ToString(culture)).Padding(6f),
//                                c => c.Text(it.Description).Padding(6f),
//                                c => c.Text(up).Padding(6f).AlignRight(),
//                                c => c.Text(qt).Padding(6f).AlignRight(),
//                                c => c.Text(tt).Padding(6f).AlignRight());
//                        });

//                        alt = !alt;
//                    }

//                    table.Add();

//                    // Summary
//                    column.Text(" ").FontSize(1f).MarginTop(12f).Add(); // gap

//                    var flow2 = column.GetFlow();
//                    float summaryWidth = Math.Min(340f, flow2.Width);
//                    float summaryX = flow2.X + flow2.Width - summaryWidth;

//                    var summary = column.Table(summaryX, column.GetCurrentY(), summaryWidth, 0f);
//                    summary.TableWidth(summaryWidth);
//                    summary.CellPadding(6f);
//                    summary.Border("#E5E7EB", 0.75f);

//                    string F(decimal v) => currencySymbol + v.ToString("N2", culture);

//                    summary.Row(cells => cells.Cells(
//                        c => c.Text("Sub Total"),
//                        c => c.Text(F(subtotal)).AlignRight()));

//                    summary.Row(cells => cells.Cells(
//                        c => c.Text("Tax"),
//                        c => c.Text(F(tax)).AlignRight()));

//                    summary.Row(row =>
//                    {
//                        row.Background(ColorTranslator.FromHtml(darkHex));
//                        row.Cells(
//                            c => c.Text("Grand Total").Bold().TextColor(Color.White),
//                            c => c.Text(F(grandTotal)).Bold().TextColor(Color.White).AlignRight());
//                    });

//                    summary.Add();

//                    // Terms
//                    if (!string.IsNullOrWhiteSpace(terms))
//                    {
//                        column.Text(" ").FontSize(1f).MarginTop(18f).Add(); // gap
//                        column.Text("Terms & Condition").Bold().Add();
//                        column.Text(terms!).MarginTop(4f).LineHeight(1.35f).Add();
//                    }
//                });
//            });
//        });

//    return doc;
//}
