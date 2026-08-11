[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateScript({ Test-Path -LiteralPath $_ -PathType Container })]
    [string]$PackageDirectory
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

$package = Get-ChildItem -LiteralPath $PackageDirectory -Filter 'PdfBuilder.*.nupkg' |
    Where-Object { $_.Name -notlike '*.symbols.nupkg' -and $_.Name -notlike '*.snupkg' } |
    Sort-Object LastWriteTimeUtc -Descending |
    Select-Object -First 1
$symbols = Get-ChildItem -LiteralPath $PackageDirectory -Filter 'PdfBuilder.*.snupkg' |
    Sort-Object LastWriteTimeUtc -Descending |
    Select-Object -First 1

if ($null -eq $package) { throw "No PdfBuilder .nupkg was found in '$PackageDirectory'." }
if ($null -eq $symbols) { throw "No PdfBuilder .snupkg was found in '$PackageDirectory'." }

function Open-Package([System.IO.FileInfo]$file) {
    return [System.IO.Compression.ZipFile]::OpenRead($file.FullName)
}

function Assert-Entry([System.IO.Compression.ZipArchive]$archive, [string]$path) {
    $entry = $archive.Entries | Where-Object { $_.FullName -ieq $path } | Select-Object -First 1
    if ($null -eq $entry -or $entry.Length -eq 0) {
        throw "Package '$($archive.ToString())' is missing non-empty entry '$path'."
    }
    return $entry
}

$packageArchive = Open-Package $package
$symbolsArchive = Open-Package $symbols
try {
    foreach ($framework in @('net8.0', 'net10.0')) {
        Assert-Entry $packageArchive "lib/$framework/PdfBuilder.dll" | Out-Null
        Assert-Entry $packageArchive "lib/$framework/PdfBuilder.xml" | Out-Null
        $pdbEntry = Assert-Entry $symbolsArchive "lib/$framework/PdfBuilder.pdb"

        $stream = $pdbEntry.Open()
        try {
            $memory = [System.IO.MemoryStream]::new()
            $stream.CopyTo($memory)
            $bytes = $memory.ToArray()
            if ($bytes.Length -lt 4 -or [System.Text.Encoding]::ASCII.GetString($bytes, 0, 4) -ne 'BSJB') {
                throw "Symbol entry for $framework is not a portable PDB."
            }
            $pdbText = [System.Text.Encoding]::UTF8.GetString($bytes)
            if ($pdbText -notmatch 'raw\.githubusercontent\.com|github\.com/MrSneazy-R/PdfBuilder') {
                throw "Symbol entry for $framework does not contain PdfBuilder SourceLink repository metadata."
            }
        }
        finally {
            $stream.Dispose()
        }
    }

    $nuspec = $packageArchive.Entries | Where-Object { $_.FullName -like '*.nuspec' } | Select-Object -First 1
    if ($null -eq $nuspec) { throw 'Package does not contain a nuspec.' }
    $reader = [System.IO.StreamReader]::new($nuspec.Open())
    try { [xml]$nuspecXml = $reader.ReadToEnd() }
    finally { $reader.Dispose() }

    $repository = $nuspecXml.package.metadata.repository
    if ($null -eq $repository -or $repository.type -ne 'git' -or $repository.url -ne 'https://github.com/MrSneazy-R/PdfBuilder') {
        throw 'Package repository metadata is missing or incorrect.'
    }
}
finally {
    $packageArchive.Dispose()
    $symbolsArchive.Dispose()
}

Write-Host "Verified .NET 8/.NET 10 assemblies, XML docs, portable SourceLink symbols, and repository metadata."
