BarcodeElement
==============

Overview
--------
`BarcodeElement` renders QR codes and linear barcodes as vector paths, leveraging ZXing. You can add one through `ContentComposer.Barcode` or `ColumnBuilder.Barcode`.

Supported symbologies (`BarcodeKind`)
- `QrCode`
- `Code128`
- `Code39`
- `Ean13`

Key Properties
--------------
- `Value`: data encoded in the barcode. Changing the value regenerates path commands.
- `Kind`: choose symbology.
- `ModuleSize`: size of each module (dot/bar width) in points; minimum 0.25.
- `QuietZone`: margin (in modules) around the barcode.
- `ForegroundColor` / `BackgroundColor`: hex colors (e.g., `#000000`).
- Inherits from `CanvasElement`, so you can set margins, `AvoidBreakInside`, or wrap it with backgrounds/borders via `ContentComposer`.

Example
-------
```csharp
page.Compose(flow =>
{
    flow.Barcode("https://contoso.example/signup",
                 kind: BarcodeKind.QrCode,
                 moduleSize: 3f,
                 quietZone: 6,
                 configure: barcode =>
                 {
                     barcode.ForegroundColor = "#1F2933";
                     barcode.BackgroundColor = "#F9FAFB";
                 });
});
```

Expected Outcome
----------------
- A QR code sized automatically based on module count and module size, surrounded by a light background square.
- Scanning the code opens the sign-up URL.
- Because the barcode is vector-based, it scales cleanly at any zoom level.
