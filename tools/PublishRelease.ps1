param()
$ErrorActionPreference = 'Stop'
if (-not $env:GITHUB_TOKEN -or -not $env:GITHUB_REPOSITORY -or -not $env:GITHUB_SHA) { throw '请在 GitHub Actions 发布工作流中运行此脚本。' }
$projectRoot = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$info = (Get-Item -LiteralPath (Join-Path $projectRoot 'dist\FocusSearch.exe')).VersionInfo
$version = "$($info.FileMajorPart).$($info.FileMinorPart).$($info.FileBuildPart)"
$tag = "v$version"
$headers = @{ Authorization = "Bearer $env:GITHUB_TOKEN"; Accept = 'application/vnd.github+json'; 'X-GitHub-Api-Version' = '2026-03-10'; 'User-Agent' = 'FocusSearch-release' }
$api = "https://api.github.com/repos/$env:GITHUB_REPOSITORY"
$release = $null
try { $release = Invoke-RestMethod -Uri "$api/releases/tags/$tag" -Headers $headers }
catch { if ([int]$_.Exception.Response.StatusCode -ne 404) { throw } }
if ($release) {
    if ($release.draft) { throw "Release $tag is an existing draft. Check its assets before manually finishing or retrying publication." }
    Write-Output "Release $tag already exists; no existing assets were modified."; exit 0
}
$changelog = Get-Content -LiteralPath (Join-Path $projectRoot 'CHANGELOG.md') -Raw -Encoding utf8
$sectionPattern = '(?ms)^##\s+' + [regex]::Escape($version) + '(?:\s+[^\r\n]*)?\r?\n(.*?)(?=^##\s|\z)'
$section = [regex]::Match($changelog, $sectionPattern)
if (-not $section.Success) { throw "CHANGELOG.md does not contain release notes for $version." }
$downloadGuide = @'
## 下载与使用

适用于 Windows 10 / Windows 11 x64，需要 .NET Framework 4.8 或 4.8.1。

1. 下载本版本的 `windows-portable.zip` 并解压到可写目录。
2. 更新前先从托盘退出旧程序，再双击新的 `FocusSearch.exe`。
3. `Alt + Space` 打开聚焦搜索，`Alt + A` 打开仅展示软件的应用网格。

`SHA256SUMS.txt` 用于校验 ZIP 与可执行文件。配置和使用历史会保留，无需重新安装开发环境。
'@
$releaseNotes = $section.Groups[1].Value.Trim() + "`n`n" + $downloadGuide
$body = @{
    tag_name = $tag; target_commitish = $env:GITHUB_SHA; name = "FocusSearch $tag"; draft = $true; prerelease = $false
    body = $releaseNotes
} | ConvertTo-Json
$release = Invoke-RestMethod -Method Post -Uri "$api/releases" -Headers $headers -ContentType 'application/json; charset=utf-8' -Body ([System.Text.Encoding]::UTF8.GetBytes($body))
$uploadBase = $release.upload_url -replace '\{.*$', ''
foreach ($file in @("FocusSearch-$version-windows-portable.zip", 'FocusSearch.exe', 'SHA256SUMS.txt')) {
    $path = Join-Path $projectRoot "dist\$file"
    $type = if ($file.EndsWith('.zip')) { 'application/zip' } else { 'application/octet-stream' }
    $null = Invoke-RestMethod -Method Post -Uri ($uploadBase + '?name=' + [Uri]::EscapeDataString($file)) -Headers $headers -ContentType $type -InFile $path
}
$published = Invoke-RestMethod -Method Patch -Uri "$api/releases/$($release.id)" -Headers $headers -ContentType 'application/json' -Body '{"draft":false,"make_latest":"true"}'
Write-Output $published.html_url
