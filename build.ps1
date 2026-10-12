param(
    [switch]$Test,
    [switch]$Clean
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$buildDirectory = Join-Path $projectRoot 'build'
$compiler = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'

if ($Clean -and (Test-Path -LiteralPath $buildDirectory)) {
    $resolvedBuild = (Resolve-Path -LiteralPath $buildDirectory).Path
    $expectedBuild = [System.IO.Path]::GetFullPath((Join-Path $projectRoot 'build'))
    if ($resolvedBuild -ne $expectedBuild -or ((Get-Item -LiteralPath $buildDirectory).Attributes -band [System.IO.FileAttributes]::ReparsePoint)) {
        throw 'Unexpected build directory path; refusing to clean.'
    }
    Remove-Item -LiteralPath $buildDirectory -Recurse -Force
}

New-Item -ItemType Directory -Force -Path $buildDirectory | Out-Null

if (-not (Test-Path -LiteralPath $compiler)) {
    throw "The .NET Framework C# compiler was not found: $compiler"
}

$references = @(
    '/reference:System.dll',
    '/reference:System.Core.dll',
    '/reference:System.Data.dll',
    '/reference:System.Drawing.dll',
    '/reference:System.Windows.Forms.dll',
    '/reference:System.Xml.dll'
)

$sourceFiles = @(Get-ChildItem -LiteralPath (Join-Path $projectRoot 'src') -Filter '*.cs' -ErrorAction SilentlyContinue | ForEach-Object FullName)
$testFiles = @(Get-ChildItem -LiteralPath (Join-Path $projectRoot 'tests') -Filter '*.cs' | ForEach-Object FullName)

$iconBuilder = Join-Path $buildDirectory 'IconBuilder.exe'
$iconFile = Join-Path $buildDirectory 'app.ico'
& $compiler /noconfig /nologo /nowarn:1700 /target:exe "/out:$iconBuilder" /reference:System.dll /reference:System.Drawing.dll (Join-Path $projectRoot 'src\ApplicationIcon.cs') (Join-Path $projectRoot 'src\RoundedShapes.cs') (Join-Path $projectRoot 'tools\IconBuilder.cs')
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
& $iconBuilder $iconFile
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$entryPoint = Join-Path $projectRoot 'src\Program.cs'
if (-not (Test-Path -LiteralPath $entryPoint)) { throw 'The application entry point was not found.' }
$appOutput = Join-Path $buildDirectory 'FocusSearch.exe'
& $compiler /noconfig /nologo /nowarn:1700 /optimize+ /target:winexe /platform:anycpu "/win32icon:$iconFile" "/resource:$iconFile,FocusSearch.app.ico" "/out:$appOutput" @references @sourceFiles
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

if ($Test) {
    $testOutput = Join-Path $buildDirectory 'FocusSearch.Tests.exe'
    & $compiler /noconfig /nologo /nowarn:1700 /target:exe /main:FocusSearch.Tests.TestRunner /platform:anycpu "/resource:$iconFile,FocusSearch.app.ico" "/out:$testOutput" @references @sourceFiles @testFiles
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    & $testOutput
    exit $LASTEXITCODE
}

Write-Output "Built $appOutput"
exit 0
