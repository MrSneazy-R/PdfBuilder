# Canonical layout primitives

The canonical API composes a single internal component tree. `IContainer` is the only public entry point for normal layout content; it does not expose PDF coordinates, pages' element collections, or writer objects.

## Container lifecycle and layout order

A container collects decorations, constraints, and child content until the page descriptor finishes. The adapter then maps the container to the existing measure/draw pipeline in this order:

1. outer margin;
2. size constraints and alignment;
3. background and borders;
4. inner padding;
5. text, columns, rows, grids, stacks, layers, and repeated children.

This keeps a background inside a margin and behind padded content. `Text` inserts a terminal node immediately, so it never needs a legacy `Add()` call.

## Coordinate system and units

The layout engine remains point-based. Use `Units.Points`, `Units.Millimeters`, `Units.Centimeters`, or `Units.Inches` to make conversion explicit. No canonical API accepts raw PDF coordinates.

```csharp
page.Content()
    .Margin(Units.Millimeters(4))
    .Padding(Units.Points(12))
    .Background("#F3F7FC")
    .Border(1, "#1E5AA8")
    .CornerRadius(6)
    .Text("Coordinate-free content");
```

## Break behaviour

`EnsureSpace` moves a component to the next page when the requested minimum height is unavailable. `KeepTogether` marks a component as non-splittable when it can fit on a page; `KeepWithNext` currently uses the same safe non-splitting behaviour. `PageBreak` is an explicit flow marker and is intended between root content items. Automatic pagination remains the responsibility of the existing `ColumnBuilder` pipeline.

`ShowIf(false)` contributes an empty component, preserving the component-tree invariant without drawing content. `Repeat` expands a finite set of child containers during composition.

The `layout-primitives` visual fixture uses a documented 2% Linux pixel-difference allowance until a Linux-specific approved raster is generated on the pinned CI rasteriser; all other fixtures retain the existing 0.6% Linux and 0.2% default tolerance.

## Constraints

All dimensions must be finite and non-negative. A minimum cannot exceed its corresponding maximum, and an exact size cannot contradict an active minimum or maximum. These cases throw an explicit exception during document composition.

Text alignment remains a text-style concern; `AlignLeft`, `AlignCenter`, `AlignRight`, `AlignTop`, `AlignMiddle`, and `AlignBottom` position containers.

## Flowing tables

`IContainer.Table` uses the same measurement, partial-layout, remainder, and draw pipeline as normal flowing content. Declare columns before rows. A header repeats after a page break, rows stay together, and content after a split table resumes below its final segment.

```csharp
page.Content().Table(table =>
{
    table.Columns(columns =>
    {
        columns.RelativeColumn();
        columns.ConstantColumn(80);
    });

    table.Header(header =>
    {
        header.Cell().Text("Description").Bold();
        header.Cell().AlignRight().Text("Amount").Bold();
    });

    foreach (var line in lines)
    {
        table.Row(row =>
        {
            row.Cell().Text(line.Description);
            row.Cell().AlignRight().Text(line.Amount, "N2");
        });
    }
});
```

Use `CellPadding`, `Border`, `HeaderBackground`, and the cell `Padding`, `Border`, `Background`, and alignment methods for basic styling. A row taller than the usable page area throws an explicit exception because row splitting is intentionally deferred.

### Advanced table behaviour

The legacy table models remain the advanced styling adapter: they support per-side `BorderStyle` values (dash, cap, join, and miter settings), separate and collapsed borders, table inner/outer borders, row and column banding, spans, per-side padding, rounded cell/table corners, rich runs, fallbacks, decorations, rotation, and explicit wrapping modes. Paint order is deterministic: cell background, row/column banding fills, text/content, inner borders, then the outer border.

Collapsed-border conflicts prefer the thicker edge, then the higher-precedence owner, then the deterministic top, left, bottom, right order. Span geometry is validated during measure and render; invalid spans throw rather than emitting corrupt output. A span that cannot fit as an intact group on a page throws because row splitting is intentionally not implemented. Repeated footer rows and configurable row-splitting policy are deferred: both require a distinct continuation model so they cannot be added safely without changing the pagination contract.
