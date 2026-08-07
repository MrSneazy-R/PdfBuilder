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
    $env:NUGET_PACKAGES = Join-Path $consumerDirectory '.nuget-packages'
    $nugetConfig = Join-Path $consumerDirectory 'NuGet.Config'
    @"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="local" value="$packageSource" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
</configuration>
"@ | Set-Content -LiteralPath $nugetConfig -Encoding utf8
    dotnet new console --framework net10.0 --no-restore | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "dotnet new failed with exit code $LASTEXITCODE." }
    dotnet add package PdfBuilder --version $version --source $packageSource --no-restore | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "dotnet add package failed with exit code $LASTEXITCODE." }
    dotnet restore --configfile $nugetConfig | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "dotnet restore failed with exit code $LASTEXITCODE." }

    @'
using PdfBuilder.Document;

var document = PdfDocument.Create(descriptor =>
{
    descriptor.Page(page =>
    {
        page.Size(PageSizes.A4);
        page.Margin(40);
        page.Content().Column(column =>
        {
            column.Item().Text("PdfBuilder package smoke test").FontSize(12);
        });
    });
});

var outputPath = Path.Combine(AppContext.BaseDirectory, "package-smoke.pdf");
document.Save(outputPath);

if (!File.Exists(outputPath) || new FileInfo(outputPath).Length == 0)
{
    throw new InvalidOperationException("The package smoke test did not create a non-empty PDF.");
}

Console.WriteLine($"Generated {new FileInfo(outputPath).Length} bytes at {outputPath}");
'@ | Set-Content -LiteralPath Program.cs -Encoding utf8

    dotnet run --configuration Release | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "dotnet run failed with exit code $LASTEXITCODE." }
}
finally {
    Pop-Location
    Remove-Item -LiteralPath $consumerDirectory -Recurse -Force -ErrorAction SilentlyContinue
}
