$ErrorActionPreference = 'Stop'
$taskWorkspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$taskSourceLeft = Join-Path $taskWorkspace 'src/DesktopPet/Assets/Pet/WalkLeft'
$taskAcceptedLeft = Join-Path $taskWorkspace 'artifacts/imagegen/p17/previous-runtime/WalkLeft'
$taskQuarantine = Join-Path $taskWorkspace 'artifacts/verification/p18-quarantined-frames'
$taskAllowed = @(
    @{Path=$taskSourceLeft;Label='source'},
    @{Path=(Join-Path $taskWorkspace 'src/DesktopPet/bin/Release/net10.0-windows/win-x64/Assets/Pet/WalkLeft');Label='app-output'},
    @{Path=(Join-Path $taskWorkspace 'tests/DesktopPet.WindowsChecks/bin/Release/net10.0-windows/win-x64/Assets/Pet/WalkLeft');Label='windows-output'}
)
foreach ($taskEntry in $taskAllowed) {
    $taskDirectory = [IO.Path]::GetFullPath($taskEntry.Path)
    $taskBackup = [IO.Path]::GetFullPath((Join-Path $taskQuarantine $taskEntry.Label))
    if (-not $taskDirectory.StartsWith($taskWorkspace + '\', [StringComparison]::OrdinalIgnoreCase) -or
        -not $taskBackup.StartsWith($taskQuarantine + '\', [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Unverified frame quarantine path.'
    }
    New-Item -ItemType Directory -Path $taskBackup -Force | Out-Null
    foreach ($taskName in @('walk_left_008.png','walk_left_009.png','walk_left_010.png','walk_left_011.png')) {
        $taskOld = [IO.Path]::GetFullPath((Join-Path $taskDirectory $taskName))
        $taskRetained = [IO.Path]::GetFullPath((Join-Path $taskBackup $taskName))
        if ((Split-Path -Parent $taskOld) -ne $taskDirectory -or (Split-Path -Parent $taskRetained) -ne $taskBackup) {
            throw 'Unverified frame file path.'
        }
        if (Test-Path -LiteralPath $taskOld) {
            if (Test-Path -LiteralPath $taskRetained) { throw 'Existing quarantine file would be overwritten.' }
            Move-Item -LiteralPath $taskOld -Destination $taskRetained
        }
    }
}
$taskFiles = @(Get-ChildItem -LiteralPath $taskAcceptedLeft -Filter '*.png' -File)
if ($taskFiles.Count -ne 8) { throw 'Accepted left source count changed.' }
foreach ($taskFile in $taskFiles) {
    $taskDestination = Join-Path $taskSourceLeft $taskFile.Name
    Copy-Item -LiteralPath $taskFile.FullName -Destination $taskDestination -Force
    (Get-Item -LiteralPath $taskDestination).LastWriteTime = Get-Date
    if ((Get-FileHash -LiteralPath $taskDestination).Hash -ne (Get-FileHash -LiteralPath $taskFile.FullName).Hash) {
        throw 'Restored frame differs from accepted source.'
    }
}
Write-Output 'Restored eight accepted left frames; four rejected extra poses preserved in quarantine, including stale derived copies.'
