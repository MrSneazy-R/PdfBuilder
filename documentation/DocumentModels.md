Document Models
===============

This reference covers supporting models frequently configured when building documents.

DocumentMetadata (`Models/DocumentMetadata.cs`)
-----------------------------------------------
- `Author`, `Subject`, `Keywords`, `Creator`, `Producer`: strings written to the PDF info dictionary.
- `CreatedUtc`, `ModifiedUtc`: UTC timestamps.
- Methods: `CopyFrom`, `Clone`.

Usage:
```csharp
builder.Metadata(meta =>
{
    meta.Author = "Jane Doe";
    meta.Subject = "Financial Summary";
    meta.CreatedUtc = DateTime.UtcNow;
});
```
Expected: metadata fields surface in PDF viewers and indexing tools.

PdfOutputOptions (`Models/PdfOutputOptions.cs`)
-----------------------------------------------
- `CompressContentStreams` (bool): enable Flate compression.
- `ContentCompressionLevel`, `ImageCompressionLevel`: choose `CompressionLevel`.
- `UsePngPredictor`: toggles PNG predictor filters for better ratios.
- Methods: `CopyFrom`, `Clone`.

Usage:
```csharp
builder.OutputOptions(opt =>
{
    opt.CompressContentStreams = true;
    opt.ContentCompressionLevel = CompressionLevel.Fastest;
});
```
Expected: smaller file size with fast compression.

HeaderFooterSpec (`Models/HeaderFooterModels.cs`)
-------------------------------------------------
- Template strings: `HeaderTemplate`, `FooterTemplate` (`{page}`, `{pages}`, `{title}`, `{date:format}`, `{time:format}`).
- Layout: `HeaderHeight`, `FooterHeight`.
- Typography: `FontFamily`, `FontSize`, `Color`, `HeaderAlign`, `FooterAlign`.
- Behavior: `FirstPageDifferent`, `FirstPageHeaderTemplate`, `FirstPageFooterTemplate`, `HideOnLastPage`.
- Rich layout: `HeaderLayout`, `FooterLayout` (use `ContentComposer`).

MasterPageSpec & WatermarkSpec (`Models/HeaderFooterModels.cs`)
---------------------------------------------------------------
- `BackgroundColor`.
- Background image: `BackgroundImage` (byte[]), `BackgroundImageMime`, position/size overrides.
- Watermark (`WatermarkSpec`): configure text or image watermarks (font, size, opacity, rotation, placement, layer).

Usage:
```csharp
builder.Master(master =>
{
    master.BackgroundColor = "#F3F4F6";
    master.Watermark = new WatermarkSpec
    {
        Text = "DRAFT",
        FontFamily = "Helvetica",
        FontSize = 80,
        Opacity = 0.05f,
        RotationDegrees = 30,
        Layer = WatermarkLayer.BehindContent
    };
});
```
Expected: all pages receive a light "DRAFT" watermark under the content with a gray background.

TextStyleDefaults (`Models/TextStyleDefaults.cs`)
-------------------------------------------------
Properties influence text, rich text, table cells, and other string-based components:
- Font & formatting: `FontFamily`, `FontSize`, `LineHeight`, `Color`, `Bold`, `Italic`, `Underline`, `Strikethrough`, `SmallCaps`, `Monospace`.
- Layout: `Alignment`, `BaselineOffset`, `FlowDirection`.
- Spacing: `LetterSpacing`, `WordSpacing`.
- Decorations: `DecorationColor`, `DecorationThickness`, `DecorationStyle`, `Overline`.
- `Opacity`, `Transform`, `FallbackFonts` (list).
- Methods: `Clone`, `CopyFrom`, `ApplyTo(TextElement)`, `ApplyTo(RichTextElement)`, `ApplyTo(TableCell)`, etc.

Usage:
```csharp
builder.DefaultTextStyle(defaults =>
{
    defaults.FontFamily = "Inter";
    defaults.FontSize = 11;
    defaults.LineHeight = 1.4f;
    defaults.Color = "#1F2933";
    defaults.FallbackFonts = new List<string> { "Noto Sans", "Segoe UI Emoji" };
});
```
Expected: all subsequent text inherits Inter 11pt styling with emoji support unless overridden locally.

PdfDocument & PdfPage (`Models/PdfPage.cs`, `Document/PdfDocument.cs`)
----------------------------------------------------------------------
- `PdfDocument.Pages`: list managed by the builder/composer.
- `PdfDocument.LayoutOptions`, `OutputOptions`, `Metadata`, `TextDefaults`, `Pagination`, `ProfilerSession`.
- `PdfPage`: holds margin settings, header/footer overrides, background, column specifications (`ColumnLayoutSpec`), and element collections.

Manual Page Usage:
```csharp
var doc = new PdfDocument();
var page = doc.AddPage(width: 792, height: 612); // landscape Letter
page.MarginLeft = 36;
page.MarginRight = 36;
```
Expected: subsequent builders respect the updated margins on the landscape page.

ColumnLayoutSpec (`Models/ColumnLayoutSpec.cs`)
----------------------------------------------
- `Columns`: number of columns (default 1).
- `Gutter`: space between columns.
- `ExplicitWidths`: optional array of column widths; otherwise widths are distributed evenly.

Setting Columns:
```csharp
page.Columns = new ColumnLayoutSpec { Columns = 2, Gutter = 18 };
```
Expected: column flows switch to a two-column layout with 18pt gutter.

TextTransform, TextAlignment, FlowDirection, TextDecorationStyle, ListMarker, TextWrapMode
------------------------------------------------------------------------------------------
Enums used throughout the API. Refer to source for accepted values; they align closely with CSS naming (e.g., `TextAlignment.Left`, `TextWrapMode.EllipsisWhenClipped`).
