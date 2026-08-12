[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateScript({ Test-Path -LiteralPath $_ -PathType Container })]
    [string]$PackageDirectory,

    [Parameter(Mandatory = $true)]
    [string]$Version,

    [string]$ProjectFile = (Join-Path $PSScriptRoot '..\PdfBuilder.csproj'),

    [string]$AssetsFile = (Join-Path $PSScriptRoot '..\obj\project.assets.json')
)

$ErrorActionPreference = 'Stop'

[xml]$project = Get-Content -LiteralPath $ProjectFile -Raw
$projectVersion = [string]$project.Project.PropertyGroup.Version
if ($Version -ne $projectVersion) {
    throw "Requested version '$Version' does not match PdfBuilder.csproj version '$projectVersion'."
}
if ($Version -notmatch '-') {
    throw "Release preparation is limited to pre-release versions while stable-release gates remain unresolved."
}
if ($Version -match '^1\.0\.0-rc(?:\.|$)') {
    throw "1.0.0 release-candidate labels are blocked until the approved-licence and exact-commit evidence gates pass."
}
if (-not (Test-Path -LiteralPath $AssetsFile -PathType Leaf)) {
    throw "NuGet assets file '$AssetsFile' was not found. Run dotnet restore first."
}

$assets = Get-Content -LiteralPath $AssetsFile -Raw | ConvertFrom-Json
$packageFolders = @($assets.packageFolders.PSObject.Properties.Name | Sort-Object)
if ($packageFolders.Count -eq 0) {
    throw 'The NuGet assets file does not declare a global package folder.'
}

$components = foreach ($entry in $assets.libraries.PSObject.Properties | Sort-Object Name) {
    if ($entry.Value.type -ne 'package') { continue }
    $separator = $entry.Name.LastIndexOf('/')
    if ($separator -le 0) { continue }
    $name = $entry.Name.Substring(0, $separator)
    $packageVersion = $entry.Name.Substring($separator + 1)
    $relativeNuspecPath = Join-Path $name.ToLowerInvariant() (Join-Path $packageVersion.ToLowerInvariant() ($name.ToLowerInvariant() + '.nuspec'))
    $nuspecPath = $packageFolders |
        ForEach-Object { Join-Path $_ $relativeNuspecPath } |
        Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } |
        Select-Object -First 1
    $licences = @()
    if (-not [string]::IsNullOrWhiteSpace($nuspecPath) -and (Test-Path -LiteralPath $nuspecPath -PathType Leaf)) {
        [xml]$nuspec = Get-Content -LiteralPath $nuspecPath -Raw
        $licence = $nuspec.package.metadata.license
        if ($null -ne $licence -and -not [string]::IsNullOrWhiteSpace([string]$licence.InnerText)) {
            if ([string]$licence.type -eq 'expression') {
                $licences = @(@{ expression = [string]$licence.InnerText })
            }
            else {
                $licences = @(@{ license = @{ name = [string]$licence.InnerText } })
            }
        }
    }

    $component = [ordered]@{
        type = 'library'
        name = $name
        version = $packageVersion
        purl = "pkg:nuget/$([Uri]::EscapeDataString($name))@$([Uri]::EscapeDataString($packageVersion))"
    }
    if ($licences.Count -gt 0) { $component['licenses'] = $licences }
    $component
}

$sbom = [ordered]@{
    bomFormat = 'CycloneDX'
    specVersion = '1.6'
    version = 1
    metadata = [ordered]@{
        timestamp = [DateTimeOffset]::UtcNow.ToString('O')
        component = [ordered]@{
            type = 'library'
            name = 'PdfBuilder'
            version = $Version
            purl = "pkg:nuget/PdfBuilder@$([Uri]::EscapeDataString($Version))"
        }
        tools = @{
            components = @(@{
                type = 'application'
                name = 'PdfBuilder release artifact script'
                version = $projectVersion
            })
        }
    }
    components = @($components)
}

$sbomPath = Join-Path $PackageDirectory "PdfBuilder.$Version.sbom.cdx.json"
$sbomJson = $sbom | ConvertTo-Json -Depth 20
[System.IO.File]::WriteAllText($sbomPath, $sbomJson, [System.Text.UTF8Encoding]::new($false))

$checksumFiles = Get-ChildItem -LiteralPath $PackageDirectory -File |
    Where-Object { $_.Extension -in @('.nupkg', '.snupkg', '.json') } |
    Sort-Object Name
$checksumLines = foreach ($file in $checksumFiles) {
    $hash = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    "$hash *$($file.Name)"
}
$checksumPath = Join-Path $PackageDirectory 'checksums.sha256'
$checksumLines | Set-Content -LiteralPath $checksumPath -Encoding ascii

Write-Host "Generated CycloneDX SBOM '$sbomPath' with $($components.Count) NuGet components."
Write-Host "Generated SHA-256 manifest '$checksumPath' for $($checksumFiles.Count) release artifacts."
