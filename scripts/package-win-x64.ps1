param(
    [ValidatePattern('^[0-9]+\.[0-9]+\.[0-9]+(?:-[A-Za-z0-9.-]+)?$')]
    [string]$Version = '1.0.0'
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = [System.IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$artifactRoot = [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot 'artifacts'))
$publishDirectory = [System.IO.Path]::GetFullPath((Join-Path $artifactRoot 'publish\win-x64'))
$projectPath = Join-Path $repositoryRoot 'src\MyIPTV.App\MyIPTV.App.csproj'
$archivePath = Join-Path $artifactRoot "MyIPTV-$Version-win-x64.zip"
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

if (Test-Path -LiteralPath $archivePath) {
    Remove-Item -LiteralPath $archivePath -Force
}
Compress-Archive -Path (Join-Path $publishDirectory '*') -DestinationPath $archivePath -CompressionLevel Optimal

$hash = (Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash.ToLowerInvariant()
Set-Content -LiteralPath $checksumPath -Encoding ascii -Value "$hash *$(Split-Path -Leaf $archivePath)"

Write-Host "Package: $archivePath"
Write-Host "SHA-256: $hash"
