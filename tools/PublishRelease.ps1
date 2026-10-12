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
$body = @{
    tag_name = $tag; target_commitish = $env:GITHUB_SHA; name = "FocusSearch $tag"; draft = $true; prerelease = $false
    body = "Windows 10/11 便携版。下载 zip、解压并双击 FocusSearch.exe。Alt + Space 唤起搜索，托盘设置可切换应用网格模式。SHA256SUMS.txt 用于校验下载文件。"
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
