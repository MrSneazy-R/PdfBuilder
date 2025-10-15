// ComposerUsageGuide.cs
// -----------------------------------------------------------------------------
// Comprehensive, heavily-commented walk-through of the declarative composer
// APIs, layout primitives, diagnostics hooks, customization entry points, and
// the enhanced table component. Everything is left commented so the guide can
// be copied verbatim into tests or samples without impacting the library.
// -----------------------------------------------------------------------------
// STRUCTURE
//   0.  Quick glossary & architectural overview
//   1.  Bootstrapping the document & enabling the composer
//   2.  Flow primitives (builders & components) in detail
//   3.  Customization knobs (margins, columns, layout options, diagnostics)
//   4.  Enhanced TableComponent features (headers, banding, spans, pagination)
//   5.  Writing custom components (Measure/Draw lifecycle)
//   6.  Full end-to-end sample putting everything together
//   7.  Tips, gotchas, and future extension points
// -----------------------------------------------------------------------------

/*
// -----------------------------------------------------------------------------
// 0. QUICK GLOSSARY & ARCHITECTURE
// -----------------------------------------------------------------------------
// - Measure/Draw Pipeline: Every component implements IMeasurable.Measure to
//   produce a LayoutMeasurement (height, remainder, metadata) and Draw to
//   render onto the page using that metadata. ColumnBuilder orchestrates
//   repeated calls until the component is fully drawn or yields a remainder.
// - FlowColumn: Represents a column within a page (X, width, current Y, bottom).
//   ColumnBuilder keeps an array of FlowColumns produced via FlowGrid.Create.
// - LayoutComponentCollection: DSL exposed via ColumnBuilder.Compose where you
//   build nested declarative primitives (Column/Row/Grid/etc.).
// - Component Builders: Thin wrappers on top of ColumnBuilder for specific
//   elements (TextBuilder, ImageBuilder, TableBuilder, ChartBuilder, ListBuilder,
//   RichTextBuilder, AnchorBuilder). These remain available for imperative use.
// - Diagnostics: LayoutDebugOptions toggles (TraceLayout, DrawBoundingBoxes,
//   ShowFlowGuides) plus environment variable `PDFBUILDER_LAYOUT_DEBUG`.
// - Remainders: When a component can't finish in the available height it returns
//   LayoutResultKind.Partial and an IMeasurable remainder. ColumnBuilder will
//   advance to the next column or page and continue measuring/drawing the
//   remainder automatically.

// ----------------------------------------------------------------------------- 
// 1. Bootstrapping the document & enabling the composer
// -----------------------------------------------------------------------------
using PdfBuilder.Document;
using PdfBuilder.Document.Layout;
using PdfBuilder.Elements;
using PdfBuilder.Models;

var document = new PdfDocument();
var builder = new PdfDocumentBuilder(document)
    .DefaultContentMargin(48) // shared margin for all pages unless overridden
    .UseLayout(options =>
    {
        options.Mode = LayoutMode.MeasureDraw;          // activate the two-phase engine
        options.EnableMeasurementCaching = true;        // cache measurements for reused components
        options.Debug.TraceLayout = false;              // emits Trace statements when true
    })
    .LayoutDebug(debug =>
    {
        debug.DrawBoundingBoxes = false; // draw reserved rectangles per component
        debug.ShowFlowGuides = false;    // overlay column guides
        debug.TraceLayout = false;       // runtime tracing (alternative to options.Debug.TraceLayout)
    });

// Compose the document with one or more pages. Each page gets a declarative
// column flow automatically.
builder.Compose(doc =>
{
    doc.Page(page =>
    {
        page
            .Margin(36)                   // page-level override (points)
            .AutoPaginate(document)       // connect auto-pagination; new pages inherit doc defaults
            .Compose(flow =>
            {
                // -----------------------------------------------------------------
// 2. Core layout primitives
// -----------------------------------------------------------------

// TEXT -------------------------------------------------------------
flow.Text("Composer-driven layout guide")
                    .FontSize(24)
                    .Bold()
                    .MarginBottom(8)
                    .Add();

                flow.Text("Leverage Measure/Draw for deterministic pagination.")
                    .FontSize(12)
                    .LineHeight(1.4f)
                    .Add();

                // PADDING + ALIGN -------------------------------------------------
                flow.Padding(16, padded =>
                {
                    padded.Align(
                        LayoutHorizontalAlignment.Center,
                        LayoutVerticalAlignment.Middle,
                        aligned =>
                        {
                            aligned.Text("Centered callout")
                                   .FontSize(14)
                                   .BackgroundColor("#F2F6FF")
                                   .PaddingTop(8)
                                   .PaddingBottom(8)
                                   .MarginBottom(4)
                                   .Add();
                        },
                        minHeight: 48);
                });

                // STACK / COLUMN --------------------------------------------------
                flow.Column(column =>
                {
                    column.Text("Column child 1").Add();
                    column.Text("Column child 2").Add();
                    column.Text("Column child 3").Add();
                }, spacing: 6f);

                // ROW -------------------------------------------------------------
                flow.Row(row =>
                {
                    row.Text("Row | left");
                    row.Text("Row | center");
                    row.Text("Row | right");
                }, gap: 8f);

                // STACK -----------------------------------------------------------
                flow.Stack(stack =>
                {
                    stack.Text("Stack top");
                    stack.Text("Stack middle");
                    stack.Text("Stack bottom");
                });

                // GRID ------------------------------------------------------------
                flow.Grid(columns: 3, grid =>
                {
                    for (int i = 1; i <= 6; i++)
                    {
                        grid.Text($"Grid cell {i}").Add();
                    }
                }, rowGap: 6f, columnGap: 6f);

                // LAYER -----------------------------------------------------------
                flow.Layer(layer =>
                {
                    layer.Background(bg =>
                    {
                        bg.Text("BACKGROUND")
                          .FontSize(60)
                          .Opacity(0.05f)
                          .Add();
                    });

                    layer.Content(content =>
                    {
                        content.Text("Layered content").FontSize(14).Add();
                    });

                    layer.Foreground(fg =>
                    {
                        fg.Text("(Foreground annotation)").FontSize(8).Add();
                    });
                });

                // DECORATE --------------------------------------------------------
                flow.Decorate(deco =>
                {
                    deco.Background(ctx =>
                    {
                        var rect = ctx.Rect;
                        ctx.Page.AddElement(new DebugRectangleElement(rect.X, rect.Top)
                        {
                            Width = rect.Width,
                            Height = rect.Height,
                            StrokeColor = "#4A90E2",
                            StrokeWidth = 0.5f,
                            DashPattern = new[] { 2f, 2f },
                            Opacity = 0.3f
                        });
                    });
                    deco.Foreground(ctx =>
                    {
                        // Nothing additional; placeholder to illustrate both hooks.
                    });
                }, content =>
                {
                    content.Text("Decorated block").BackgroundColor("#FFFCEB").Add();
                });

                // RELATIVE --------------------------------------------------------
                flow.Relative(relative =>
                {
                    relative.Item(1, area =>
                    {
                        area.Text("Top half (weight 1)").Add();
                    });

                    relative.Item(2, area =>
                    {
                        area.Text("Bottom two-thirds (weight 2)").Add();
                    });
                }, spacing: 12f);

// DYNAMIC ---------------------------------------------------------
var chapters = new[]
{
    new { Title = "Intro", Summary = "Overview of the composer." },
    new { Title = "Tables", Summary = "Working with the enhanced TableComponent." },
    new { Title = "Diagnostics", Summary = "Debug overlays and logging." }
};

flow.Dynamic(chapters, (chapter, section) =>
{
    section.Text(chapter.Title).FontSize(16).Bold().Add();
    section.Text(chapter.Summary).MarginBottom(6f).Add();
});

// -----------------------------------------------------------------
// 2.1  Builder Reference (imperative counterparts still available)
// -----------------------------------------------------------------
// ColumnBuilder entry points:
//   Text(string)              -> TextBuilder
//   Image(byte[])             -> ImageBuilder
//   Table(float x, y, width)  -> TableBuilder
//   Chart(...)                -> ChartBuilder
//   List(...)                 -> ListBuilder
//   RichText(...)             -> RichTextBuilder
//   Anchor(string id)         -> AnchorBuilder
//   Row(float gap)            -> RowBuilder (manual cell layout with px/%/fr)
// Each builder exposes fluent setters followed by Add() to place the element.
// The composer APIs internally call these builders with Measure/Draw wrappers,
// so you rarely need the imperative forms unless migrating legacy code.

// -----------------------------------------------------------------
// 3. Enhanced tables (automatic paging, header repeats, banding)
// -----------------------------------------------------------------
var table = new TableElement(0, 0)
{
                    CaptionText = "Annual Sales by Region",
                    RepeatHeaders = true,
                    AutoSizeColumns = true,
                    RowBanding = new Elements.Table.RowBandingSpec
                    {
                        Step = 2,
                        Fills =
                        {
                            new Elements.Table.BandFill { FillColor = "#F7F7F7" }
                        }
                    },
                    ColumnBanding = new Elements.Table.ColumnBandingSpec
                    {
                        Step = 2,
                        Fills =
                        {
                            new Elements.Table.BandFill { FillColor = "#F1FAFF" }
                        }
                    },
                    OuterBorder = new Elements.Table.BorderStyle
                    {
                        Color = "#2C3E50",
                        Width = 1.5f
                    },
                    InnerBorder = new Elements.Table.BorderStyle
                    {
                        Color = "#D0D4D9",
                        Width = 0.5f
                    }
                };

                table.Rows.Add(new TableRow
                {
                    IsHeader = true,
                    Cells =
                    {
                        new TableCell("Region") { Bold = true, HorizontalAlign = HorizontalAlign.Left },
                        new TableCell("Q1") { Bold = true, HorizontalAlign = HorizontalAlign.Right },
                        new TableCell("Q2") { Bold = true, HorizontalAlign = HorizontalAlign.Right },
                        new TableCell("Q3") { Bold = true, HorizontalAlign = HorizontalAlign.Right },
                        new TableCell("Q4") { Bold = true, HorizontalAlign = HorizontalAlign.Right }
                    }
                });

                var random = new Random(42);
                foreach (var region in new[] { "North", "South", "East", "West", "Central", "International" })
                {
                    var row = new TableRow();
                    row.Cells.Add(new TableCell(region));
                    for (int quarter = 1; quarter <= 4; quarter++)
                    {
                        var value = random.Next(25_000, 95_000);
                        row.Cells.Add(new TableCell($"{value:C0}")
                        {
                            HorizontalAlign = HorizontalAlign.Right,
                            TextStyle = new Elements.Table.TextStyle
                            {
                                HorizontalAlign = HorizontalAlign.Right,
                                Wrap = Elements.Table.TextWrapMode.NoWrap
                            }
                        });
                    }
                    table.Rows.Add(row);
                }

flow.Add(table);

// -----------------------------------------------------------------
// 4. Diagnostics (enable when investigating flow issues)
// -----------------------------------------------------------------
                // Environment variable override:
                //   set PDFBUILDER_LAYOUT_DEBUG=boxes,guides,trace
                // or options.Debug.* shown earlier.
            });
    });

    // Add another page with different margin/columns if needed.
    doc.Page(page =>
    {
        page.Margin(24).Compose(flow =>
        {
            flow.Text("Second page content...").Add();
        });
    });
});

// Finally render the document as usual (omitted here).

// ----------------------------------------------------------------------------- 
// 5. WRITING CUSTOM COMPONENTS (IMeasurable)
// -----------------------------------------------------------------------------
// public sealed class CalloutComponent : IMeasurable
// {
//     private readonly string _text;
//     public CalloutComponent(string text) => _text = text;
//
//     public LayoutMeasurement Measure(LayoutMeasureContext context)
//     {
//         float width = context.AvailableWidth;
//         var lines = PdfLayoutUtils.WrapText(_text, "Helvetica", 11f, width - 12f);
//         float lineHeight = 11f * 1.3f;
//         float contentHeight = Math.Max(lineHeight, lines.Count * lineHeight);
//         var metadata = new { Lines = lines, LineHeight = lineHeight };
//         return new LayoutMeasurement(
//             marginTop: 4f,
//             contentHeight: contentHeight + 8f,
//             marginBottom: 4f,
//             usedWidth: width,
//             metadata: metadata,
//             avoidBreakInside: false,
//             result: LayoutResultKind.Full,
//             remainder: null);
//     }
//
//     public void Draw(LayoutDrawContext context, LayoutMeasurement measurement)
//     {
//         dynamic meta = measurement.Metadata!;
//         float y = context.ContentTop - 4f;
//         context.Page.AddElement(new DebugRectangleElement(context.ContentLeft, y)
//         {
//             Width = context.ContentWidth,
//             Height = measurement.ContentHeight,
//             StrokeColor = "#F5A623",
//             Opacity = 0.25f
//         });
//
//         float lineY = y - 4f;
//         foreach (string line in meta.Lines)
//         {
//             var text = new TextElement(line, context.ContentLeft + 6f, lineY) { FontSize = 11f };
//             context.Page.AddElement(text);
//             lineY -= meta.LineHeight;
//         }
//     }
// }
//
// Usage:
//     flow.Add(new CalloutComponent("Custom callout component."));

// ----------------------------------------------------------------------------- 
// 6. Tips, patterns & advanced customization
// -----------------------------------------------------------------------------
// - Each layout primitive returns the collection itself, enabling fluent calls.
// - For custom components, implement IMeasurable (Measure/Draw) and plug them
//   into the flow via flow.Add(IMeasurable instance).
// - TableComponent automatically:
//     * resizes to the flow column width (unless TableWidth is fixed),
//     * repeats headers on continuations (when RepeatHeaders = true),
//     * preserves banding offsets after page breaks,
//     * honours RowSpan/ColSpan when deciding break positions,
//     * emits remainders so ColumnBuilder can continue on the next column/page.
// - To inspect pagination behaviour, enable TraceLayout or DrawBoundingBoxes.
// - Use LayoutOptions.EnableMeasurementCaching for repeated content (charts, tables).
// - KeepWithNext and AvoidBreakInside flags on elements bubble into the new engine.
// - ColumnBuilder.Compose vs ColumnBuilder.AddComponent:
//     Compose(Action<LayoutComponentCollection>) allows declarative primitives.
//     AddComponent(IMeasurable) is available for full control/manual components.
// - FlowGrid.Create respects ColumnLayoutSpec on PdfPage (Columns, Gutter, Widths).
// - AutoPaginate wires ColumnBuilder to inject new pages using PdfDocument.AddPage.
// - Diagnostics quick toggles:
//     Environment variable: PDFBUILDER_LAYOUT_DEBUG=boxes,guides,trace
//     Document builder: builder.LayoutDebug(options => { ... });
// - Custom margins per page: page.Margins(left, top, right, bottom) or Margin(value).
// - Anchors & internal links: use ColumnBuilder.Anchor(id).Add() with RichText
//   linking to anchors (RichRun.LinkAnchor).
*/
