# Optional SDK provisioning: downloads official Microsoft files into this workspace only.
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path -Parent $PSScriptRoot
$toolsDirectory = Join-Path $taskRoot '.tools'
New-Item -ItemType Directory -Path $toolsDirectory -Force | Out-Null
$metadata = Invoke-RestMethod 'https://builds.dotnet.microsoft.com/dotnet/release-metadata/10.0/releases.json'
$release = $metadata.releases | Where-Object { $_.sdk.version -eq $metadata.'latest-sdk' } | Select-Object -First 1
$archive = $release.sdk.files | Where-Object { $_.rid -eq 'win-x64' -and $_.name -like '*.zip' } | Select-Object -First 1
if (-not $archive.url -or -not $archive.hash) { throw '无法解析微软 SDK 下载信息。' }
$zipPath = Join-Path $toolsDirectory 'dotnet-sdk.zip'
Invoke-WebRequest -Uri $archive.url -OutFile $zipPath
if ((Get-FileHash -LiteralPath $zipPath -Algorithm SHA512).Hash -ne $archive.hash) {
    throw 'SDK SHA512 校验失败。'
}
Expand-Archive -LiteralPath $zipPath -DestinationPath (Join-Path $toolsDirectory 'dotnet') -Force
Write-Host "SDK $($release.sdk.version) 已就绪。请运行 . ./scripts/use-dotnet.ps1 或 ./scripts/build.ps1。"
