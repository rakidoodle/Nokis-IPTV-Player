param(
    [ValidatePattern('^[0-9]+\.[0-9]+\.[0-9]+(?:-[A-Za-z0-9.-]+)?$')]
    [string]$Version = '1.2.0-beta.3'
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = [System.IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$artifactRoot = [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot 'artifacts'))
$publishDirectory = [System.IO.Path]::GetFullPath((Join-Path $artifactRoot 'publish\win-x64'))
$projectPath = Join-Path $repositoryRoot 'src\MyIPTV.App\MyIPTV.App.csproj'
$archivePath = Join-Path $artifactRoot "Nokis-IPTV-Player-$Version-win-x64.zip"
$checksumPath = Join-Path $artifactRoot 'SHA256SUMS.txt'

if (-not $artifactRoot.StartsWith($repositoryRoot, [System.StringComparison]::OrdinalIgnoreCase) -or
    -not $publishDirectory.StartsWith($artifactRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw 'Packaging paths resolved outside the repository.'
}

New-Item -ItemType Directory -Force -Path $artifactRoot | Out-Null
if (Test-Path -LiteralPath $publishDirectory) {
    Remove-Item -LiteralPath $publishDirectory -Recurse -Force
}

dotnet publish $projectPath -c Release -p:PublishProfile=win-x64 -p:Version=$Version -o $publishDirectory
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE."
}

# VideoLAN.LibVLC.Windows carries native runtimes for every Windows architecture.
# This artifact is x64-only, so keep only the selected runtime. The LibVLC HTTP
# interface is never enabled by the app; excluding its scripts reduces package
# size and avoids shipping an unused administrative web surface.
$unusedRuntimePaths = @(
    (Join-Path $publishDirectory 'libvlc\win-arm64'),
    (Join-Path $publishDirectory 'libvlc\win-x86'),
    (Join-Path $publishDirectory 'libvlc\win-x64\lua\http')
)
foreach ($unusedRuntimePath in $unusedRuntimePaths) {
    $resolvedParent = [System.IO.Path]::GetFullPath((Split-Path -Parent $unusedRuntimePath))
    if (-not $resolvedParent.StartsWith($publishDirectory, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Runtime pruning path resolved outside the publish directory: $unusedRuntimePath"
    }

    if (Test-Path -LiteralPath $unusedRuntimePath) {
        Remove-Item -LiteralPath $unusedRuntimePath -Recurse -Force
    }
}

foreach ($importLibrary in @('libvlc.lib', 'libvlccore.lib')) {
    $importLibraryPath = Join-Path $publishDirectory "libvlc\win-x64\$importLibrary"
    if (Test-Path -LiteralPath $importLibraryPath) {
        Remove-Item -LiteralPath $importLibraryPath -Force
    }
}

if (Test-Path -LiteralPath $archivePath) {
    Remove-Item -LiteralPath $archivePath -Force
}
Compress-Archive -Path (Join-Path $publishDirectory '*') -DestinationPath $archivePath -CompressionLevel Optimal

$hash = (Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash.ToLowerInvariant()
Set-Content -LiteralPath $checksumPath -Encoding ascii -Value "$hash *$(Split-Path -Leaf $archivePath)"

Write-Host "Package: $archivePath"
Write-Host "SHA-256: $hash"
