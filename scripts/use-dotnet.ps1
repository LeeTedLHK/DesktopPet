# Dot-source this file to use a workspace-local SDK without changing the system PATH.
$taskRoot = Split-Path -Parent $PSScriptRoot
$taskSdk = Join-Path $taskRoot '.tools\dotnet'
if (Test-Path -LiteralPath (Join-Path $taskSdk 'dotnet.exe')) {
    $env:DOTNET_ROOT = $taskSdk
    $env:PATH = "$taskSdk;$env:PATH"
}
$env:DOTNET_CLI_HOME = Join-Path $taskRoot '.tools\cli-home'
$env:NUGET_PACKAGES = Join-Path $taskRoot '.tools\nuget-packages'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_NOLOGO = '1'
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw '未找到 .NET 10 SDK。请先安装 SDK，或运行 scripts/bootstrap-dotnet.ps1。'
}
