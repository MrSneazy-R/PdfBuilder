# .NET 8 and .NET 10 packaging

`PdfBuilder.csproj` targets `net8.0;net10.0`. The public API and serializer are shared source, while NuGet selects the matching assembly for each consumer. No legacy .NET Framework target is supported.

The package contract requires:

- `lib/net8.0/PdfBuilder.dll` and `PdfBuilder.xml`;
- `lib/net10.0/PdfBuilder.dll` and `PdfBuilder.xml`;
- portable PDBs for both frameworks in the `.snupkg`;
- SourceLink repository data and NuGet repository metadata;
- dependency resolution for SkiaSharp and HarfBuzzSharp native assets on Windows, Ubuntu, and macOS;
- clean net8 and net10 consumers that generate a non-empty PDF.

Run the local package checks with:

```powershell
dotnet pack PdfBuilder.csproj -c Release -o artifacts
./eng/Test-PackageContents.ps1 -PackageDirectory ./artifacts
./eng/Invoke-PackageSmokeTest.ps1 -PackageDirectory ./artifacts
```

The smoke script creates isolated applications and restores only through the packed package plus NuGet.org. It executes both target frameworks by default. A machine must have both runtimes installed; GitHub Actions installs the .NET 8 and .NET 10 SDKs before invoking it.

The main unit project also targets both frameworks. Independent qpdf/Poppler validation remains hosted on Ubuntu and currently runs against the net10 validation project because PDF bytes are an output contract rather than a target-framework-specific API surface.
