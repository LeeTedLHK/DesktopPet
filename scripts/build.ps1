param([ValidateSet('Debug', 'Release')][string]$Configuration = 'Release')
$ErrorActionPreference = 'Stop'
try {
    . "$PSScriptRoot\use-dotnet.ps1"
    Push-Location (Split-Path -Parent $PSScriptRoot)
    try {
        dotnet restore DesktopPet.sln
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
        dotnet build DesktopPet.sln -c $Configuration --no-restore
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
        dotnet test DesktopPet.sln -c $Configuration --no-build --logger 'console;verbosity=normal'
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    } finally { Pop-Location }
} catch {
    Write-Error $_
    exit 1
}
exit 0
