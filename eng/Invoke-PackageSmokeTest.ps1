[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateScript({ Test-Path -LiteralPath $_ -PathType Container })]
    [string]$PackageDirectory
)

$ErrorActionPreference = 'Stop'

$package = Get-ChildItem -LiteralPath $PackageDirectory -Filter 'PdfBuilder.*.nupkg' |
    Where-Object { $_.Name -notlike '*.symbols.nupkg' -and $_.Name -notlike '*.snupkg' } |
    Sort-Object LastWriteTimeUtc -Descending |
    Select-Object -First 1

if ($null -eq $package) {
    throw "No PdfBuilder package was found in '$PackageDirectory'."
}

$version = [System.Text.RegularExpressions.Regex]::Match(
    $package.Name,
    '^PdfBuilder\.(?<version>.+)\.nupkg$').Groups['version'].Value

if ([string]::IsNullOrWhiteSpace($version)) {
    throw "Could not determine the package version from '$($package.Name)'."
}

$packageSource = (Resolve-Path -LiteralPath $PackageDirectory).Path
$consumerDirectory = Join-Path ([System.IO.Path]::GetTempPath()) "PdfBuilder-package-smoke-$([Guid]::NewGuid().ToString('N'))"
New-Item -ItemType Directory -Path $consumerDirectory | Out-Null

try {
    Push-Location $consumerDirectory
    dotnet new console --framework net10.0 --no-restore | Out-Host
    dotnet add package PdfBuilder --version $version --source $packageSource | Out-Host

    @'
using PdfBuilder.Document;
using PdfBuilder.Elements;
using PdfBuilder.Writer;

var document = new PdfDocument();
var page = document.AddPage();
page.Elements.Add(new TextElement
{
    X = page.MarginLeft,
    Y = page.Height - page.MarginTop - 24,
    Text = "PdfBuilder package smoke test",
    FontFamily = "Helvetica",
    FontSize = 12
});

var outputPath = Path.Combine(AppContext.BaseDirectory, "package-smoke.pdf");
new PdfWriter().Save(document, outputPath);

if (!File.Exists(outputPath) || new FileInfo(outputPath).Length == 0)
{
    throw new InvalidOperationException("The package smoke test did not create a non-empty PDF.");
}

Console.WriteLine($"Generated {new FileInfo(outputPath).Length} bytes at {outputPath}");
'@ | Set-Content -LiteralPath Program.cs -Encoding utf8

    dotnet run --configuration Release | Out-Host
}
finally {
    Pop-Location
    Remove-Item -LiteralPath $consumerDirectory -Recurse -Force -ErrorAction SilentlyContinue
}
