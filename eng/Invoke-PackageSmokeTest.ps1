[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateScript({ Test-Path -LiteralPath $_ -PathType Container })]
    [string]$PackageDirectory,

    [ValidateSet('net8.0', 'net10.0')]
    [string[]]$Frameworks = @('net8.0', 'net10.0')
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
$consumerRoot = Join-Path ([System.IO.Path]::GetTempPath()) "PdfBuilder-package-smoke-$([Guid]::NewGuid().ToString('N'))"
$previousNugetPackages = $env:NUGET_PACKAGES
New-Item -ItemType Directory -Path $consumerRoot | Out-Null

try {
    $env:NUGET_PACKAGES = Join-Path $consumerRoot '.nuget-packages'
    $nugetConfig = Join-Path $consumerRoot 'NuGet.Config'
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

    foreach ($framework in $Frameworks) {
        $consumerDirectory = Join-Path $consumerRoot $framework
        dotnet new console --no-restore --output $consumerDirectory | Out-Host
        if ($LASTEXITCODE -ne 0) { throw "dotnet new for $framework failed with exit code $LASTEXITCODE." }

        $project = Join-Path $consumerDirectory "$framework.csproj"
        [xml]$projectXml = Get-Content -LiteralPath $project -Raw
        $projectXml.Project.PropertyGroup.TargetFramework = $framework
        $projectXml.Save($project)
        dotnet add $project package PdfBuilder --version $version --source $packageSource --no-restore | Out-Host
        if ($LASTEXITCODE -ne 0) { throw "dotnet add package for $framework failed with exit code $LASTEXITCODE." }

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
'@ | Set-Content -LiteralPath (Join-Path $consumerDirectory 'Program.cs') -Encoding utf8

        dotnet restore $project --configfile $nugetConfig | Out-Host
        if ($LASTEXITCODE -ne 0) { throw "dotnet restore for $framework failed with exit code $LASTEXITCODE." }
        dotnet run --project $project --configuration Release --no-restore | Out-Host
        if ($LASTEXITCODE -ne 0) { throw "dotnet run for $framework failed with exit code $LASTEXITCODE." }
    }
}
finally {
    $env:NUGET_PACKAGES = $previousNugetPackages
    Remove-Item -LiteralPath $consumerRoot -Recurse -Force -ErrorAction SilentlyContinue
}
