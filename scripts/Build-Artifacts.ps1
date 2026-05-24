[CmdletBinding()]
param(
    [string]$Runtime = "win-x64",
    [switch]$NoRestore
)

$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$artifactsDir = Join-Path $repoRoot "artifacts"
$appProject = Join-Path $repoRoot "windows-app/src/App/FieldForce.App.csproj"
$modSource = Join-Path $repoRoot "fs25-mod"
$appPublishDir = Join-Path $artifactsDir "FieldForceApp-$Runtime"
$appZip = Join-Path $artifactsDir "FieldForceApp-$Runtime.zip"
$modZip = Join-Path $artifactsDir "FS25_FieldForceTelemetry.zip"
$legacyG27PackageDir = Join-Path $artifactsDir "FS25RealFfbBridge-G27-FS25-test"

function New-ZipFromDirectory {
    param(
        [Parameter(Mandatory = $true)]
        [string]$SourcePath,

        [Parameter(Mandatory = $true)]
        [string]$ZipPath,

        [string]$EntryPrefix = ""
    )

    Add-Type -AssemblyName System.IO.Compression
    Add-Type -AssemblyName System.IO.Compression.FileSystem

    $resolvedSource = (Resolve-Path $SourcePath).Path.TrimEnd("\", "/")
    $zipParent = Split-Path -Parent $ZipPath
    New-Item -ItemType Directory -Force -Path $zipParent | Out-Null
    Remove-Item -Force $ZipPath -ErrorAction SilentlyContinue

    $archive = [System.IO.Compression.ZipArchive]::new(
        [System.IO.File]::Open($ZipPath, [System.IO.FileMode]::CreateNew),
        [System.IO.Compression.ZipArchiveMode]::Create)

    try {
        Get-ChildItem $resolvedSource -Recurse -File |
            Sort-Object FullName |
            ForEach-Object {
                $entry = $_.FullName.Substring($resolvedSource.Length + 1).Replace("\", "/")
                if ($EntryPrefix) {
                    $entry = "$($EntryPrefix.TrimEnd('/'))/$entry"
                }

                [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $_.FullName, $entry) | Out-Null
            }
    }
    finally {
        $archive.Dispose()
    }
}

Set-Location $repoRoot
New-Item -ItemType Directory -Force -Path $artifactsDir | Out-Null

Remove-Item -Recurse -Force $appPublishDir -ErrorAction SilentlyContinue
Remove-Item -Recurse -Force $legacyG27PackageDir -ErrorAction SilentlyContinue

$publishArgs = @(
    "publish",
    $appProject,
    "--configuration", "Release",
    "--runtime", $Runtime,
    "--self-contained", "true",
    "--output", $appPublishDir,
    "-p:DebugType=None",
    "-p:DebugSymbols=false"
)

if ($NoRestore) {
    $publishArgs += "--no-restore"
}

dotnet @publishArgs

New-ZipFromDirectory -SourcePath $appPublishDir -ZipPath $appZip -EntryPrefix "FieldForceApp-$Runtime"
Remove-Item -Recurse -Force $appPublishDir
New-ZipFromDirectory -SourcePath $modSource -ZipPath $modZip

Write-Host "Created:"
Write-Host "  $appZip"
Write-Host "  $modZip"
