# Third-party notices

PdfBuilder depends on the following third-party packages at runtime. Versions are pinned in `Directory.Packages.props`; the package-specific licence files and notices distributed by their authors remain authoritative.

`PdfBuilder.Operations` can invoke a separately installed qpdf executable. qpdf is not bundled in either package; deployments must review and comply with the licence of the qpdf build they install.

`PdfBuilder.Compliance` can invoke a separately installed veraPDF executable or CLI JAR.
veraPDF is not bundled in the NuGet packages; Ubuntu CI uses the pinned `verapdf/cli:v1.30.2`
container. Deployments must review the licence of the veraPDF distribution they install.

| Package | Version | Licence | Project |
| --- | --- | --- | --- |
| SkiaSharp | 2.88.9 | MIT | https://github.com/mono/SkiaSharp |
| SkiaSharp.HarfBuzz | 2.88.9 | MIT | https://github.com/mono/SkiaSharp |
| SkiaSharp.Svg | 1.60.0 | MIT (package licence URL) | https://github.com/mono/SkiaSharp.Extended |
| SkiaSharp.NativeAssets.Linux.NoDependencies | 2.88.9 | MIT | https://github.com/mono/SkiaSharp |
| SkiaSharp.NativeAssets.Win32 | 2.88.9 | MIT | https://github.com/mono/SkiaSharp |
| SkiaSharp.NativeAssets.macOS | 2.88.9 | MIT | https://github.com/mono/SkiaSharp |
| HarfBuzzSharp | 7.3.0.3 | MIT | https://github.com/mono/SkiaSharp |
| HarfBuzzSharp.NativeAssets.Linux | 7.3.0.3 | MIT | https://github.com/mono/SkiaSharp |
| HarfBuzzSharp.NativeAssets.Win32 | 7.3.0.3 | MIT | https://github.com/mono/SkiaSharp |
| HarfBuzzSharp.NativeAssets.macOS | 7.3.0.3 | MIT | https://github.com/mono/SkiaSharp |
| ZXing.Net | 0.16.11 | Apache-2.0 | https://github.com/micjahn/ZXing.Net |

Build, analysis, test, and benchmark dependencies are not runtime dependencies of the PdfBuilder package. Their identities and versions are retained in the generated CycloneDX SBOM and NuGet restore graph.

This notice records third-party licensing only. It does not grant a licence for PdfBuilder itself. The repository owner has not yet approved the PdfBuilder project licence, so public package publication remains blocked.
