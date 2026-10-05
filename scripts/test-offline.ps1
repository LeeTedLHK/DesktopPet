param([switch]$WindowsIntegration)
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot\use-dotnet.ps1"
$taskProjectRoot = Split-Path -Parent $PSScriptRoot
$taskFeed = Join-Path $taskProjectRoot '.tools\offline-feed'
New-Item -ItemType Directory -Path $taskFeed -Force | Out-Null
$taskProjects = @('tests\DesktopPet.OfflineChecks\DesktopPet.OfflineChecks.csproj')
$taskProjects += 'tests\DesktopPet.SkinChecks\DesktopPet.SkinChecks.csproj'
$taskProjects += 'tests\DesktopPet.JournalChecks\DesktopPet.JournalChecks.csproj'
if ($WindowsIntegration) { $taskProjects += 'tests\DesktopPet.WindowsChecks\DesktopPet.WindowsChecks.csproj' }
Push-Location $taskProjectRoot
try {
    foreach ($taskProject in $taskProjects) {
        dotnet restore $taskProject --source $taskFeed -p:NuGetAudit=false
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
        dotnet run --project $taskProject -c Release --no-restore
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    }
} finally { Pop-Location }
