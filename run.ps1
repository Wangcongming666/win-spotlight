param()
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
& (Join-Path $projectRoot 'build.ps1')
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
$focusStart = New-Object System.Diagnostics.ProcessStartInfo
$focusStart.FileName = Join-Path $projectRoot 'build\FocusSearch.exe'
$focusStart.UseShellExecute = $false
[System.Diagnostics.Process]::Start($focusStart) | Out-Null
