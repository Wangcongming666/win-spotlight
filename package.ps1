param([switch]$SkipBuild)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
if (-not $SkipBuild) {
    & (Join-Path $projectRoot 'build.ps1') -Test
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}
$exe = Join-Path $projectRoot 'build\FocusSearch.exe'
if (-not (Test-Path -LiteralPath $exe)) { throw '请先构建 FocusSearch.exe。' }
$info = (Get-Item -LiteralPath $exe).VersionInfo
$version = "$($info.FileMajorPart).$($info.FileMinorPart).$($info.FileBuildPart)"
$dist = Join-Path $projectRoot 'dist'
$staging = Join-Path $dist "FocusSearch-$version"
New-Item -ItemType Directory -Force -Path $staging | Out-Null
Copy-Item -LiteralPath $exe -Destination (Join-Path $staging 'FocusSearch.exe') -Force
Copy-Item -LiteralPath (Join-Path $projectRoot 'docs\INSTALL.md') -Destination (Join-Path $staging '使用说明.md') -Force
Copy-Item -LiteralPath (Join-Path $projectRoot 'CHANGELOG.md') -Destination (Join-Path $staging 'CHANGELOG.md') -Force
$zip = Join-Path $dist "FocusSearch-$version-windows-portable.zip"
$files = @((Join-Path $staging 'FocusSearch.exe'), (Join-Path $staging '使用说明.md'), (Join-Path $staging 'CHANGELOG.md'))
Compress-Archive -LiteralPath $files -DestinationPath $zip -Force
Copy-Item -LiteralPath $exe -Destination (Join-Path $dist 'FocusSearch.exe') -Force
$checksums = @($zip, (Join-Path $dist 'FocusSearch.exe')) | ForEach-Object {
    $hash = Get-FileHash -LiteralPath $_ -Algorithm SHA256
    "$($hash.Hash.ToLowerInvariant())  $([System.IO.Path]::GetFileName($_))"
}
$checksums | Set-Content -LiteralPath (Join-Path $dist 'SHA256SUMS.txt') -Encoding ascii
Write-Output "PACKAGE=$zip"
Write-Output "VERSION=$version"
