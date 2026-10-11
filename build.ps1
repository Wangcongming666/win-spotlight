param(
    [switch]$Test,
    [switch]$Clean
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$buildDirectory = Join-Path $projectRoot 'build'
$compiler = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'

if ($Clean -and (Test-Path -LiteralPath $buildDirectory)) {
    Remove-Item -LiteralPath $buildDirectory -Recurse -Force
}

New-Item -ItemType Directory -Force -Path $buildDirectory | Out-Null

if (-not (Test-Path -LiteralPath $compiler)) {
    throw "未找到 .NET Framework C# 编译器：$compiler"
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

if ($Test) {
    $testOutput = Join-Path $buildDirectory 'FocusSearch.Tests.exe'
    & $compiler /noconfig /nologo /nowarn:1700 /target:exe /platform:anycpu "/out:$testOutput" @references @sourceFiles @testFiles
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    & $testOutput
    exit $LASTEXITCODE
}

$entryPoint = Join-Path $projectRoot 'src\Program.cs'
if (-not (Test-Path -LiteralPath $entryPoint)) {
    throw '主程序入口尚未创建。请使用 -Test 构建当前核心组件。'
}

$appOutput = Join-Path $buildDirectory 'FocusSearch.exe'
& $compiler /noconfig /nologo /nowarn:1700 /target:winexe /platform:anycpu /win32icon:app.ico "/out:$appOutput" @references @sourceFiles
exit $LASTEXITCODE
