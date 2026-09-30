$ErrorActionPreference = 'Stop'
try {
    . "$PSScriptRoot\use-dotnet.ps1"
    Push-Location (Split-Path -Parent $PSScriptRoot)
    try {
        dotnet publish src/DesktopPet/DesktopPet.csproj -c Release -r win-x64 --self-contained true -p:PublishTrimmed=false -o artifacts/publish/win-x64
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
        if (-not (Test-Path -LiteralPath 'artifacts\publish\win-x64\DesktopPet.exe')) {
            throw '发布完成但未找到 DesktopPet.exe。'
        }
        Write-Host "EXE: $(Join-Path (Get-Location) 'artifacts\publish\win-x64\DesktopPet.exe')"
    } finally { Pop-Location }
} catch {
    Write-Error $_
    exit 1
}
exit 0
