$ErrorActionPreference = 'Stop'
$taskRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$taskPrefix = $taskRoot.TrimEnd('\') + '\'
$taskBackup = Join-Path $taskRoot 'artifacts/imagegen/p19/previous-runtime'
$taskQuarantine = Join-Path $taskRoot 'artifacts/verification/p19-quarantined-frames'
foreach ($taskDirection in @('Left', 'Right')) {
    $taskAction = 'Walk' + $taskDirection
    $taskSource = Join-Path $taskRoot ('src/DesktopPet/Assets/Pet/' + $taskAction)
    $taskSaved = Join-Path $taskBackup $taskAction
    New-Item -ItemType Directory -Path $taskSaved -Force | Out-Null
    foreach ($taskFile in Get-ChildItem -LiteralPath $taskSource -File -Filter '*.png') {
        $taskSavedFile = Join-Path $taskSaved $taskFile.Name
        if (-not (Test-Path -LiteralPath $taskSavedFile)) { Copy-Item -LiteralPath $taskFile.FullName -Destination $taskSavedFile }
    }
    $taskDirectories = @($taskSource)
    foreach ($taskBase in @('src/DesktopPet/bin', 'tests/DesktopPet.WindowsChecks/bin')) {
        $taskDirectories += @(Get-ChildItem -LiteralPath (Join-Path $taskRoot $taskBase) -Directory -Recurse |
            Where-Object { $_.Name -eq $taskAction -and $_.Parent.Name -eq 'Pet' } | ForEach-Object FullName)
    }
    foreach ($taskDirectory in $taskDirectories) {
        $taskResolved = [IO.Path]::GetFullPath($taskDirectory)
        if (-not $taskResolved.StartsWith($taskPrefix, [StringComparison]::OrdinalIgnoreCase)) { throw 'Origin outside workspace.' }
        foreach ($taskIndex in 8..11) {
            $taskName = 'walk_' + $taskDirection.ToLowerInvariant() + '_' + $taskIndex.ToString('000') + '.png'
            $taskExtra = Join-Path $taskResolved $taskName
            if (Test-Path -LiteralPath $taskExtra) {
                $taskRelative = $taskResolved.Substring($taskPrefix.Length)
                $taskDestination = [IO.Path]::GetFullPath((Join-Path $taskQuarantine $taskRelative))
                if (-not $taskDestination.StartsWith($taskPrefix, [StringComparison]::OrdinalIgnoreCase)) { throw 'Quarantine outside workspace.' }
                New-Item -ItemType Directory -Path $taskDestination -Force | Out-Null
                $taskMoveTarget = Join-Path $taskDestination $taskName
                if (Test-Path -LiteralPath $taskMoveTarget) { throw 'Existing quarantine file; refusing to overwrite.' }
                Move-Item -LiteralPath $taskExtra -Destination $taskMoveTarget
            }
        }
    }
    $taskPackaged = Join-Path $taskRoot ('artifacts/imagegen/p19/packaged/' + $taskAction)
    foreach ($taskFile in Get-ChildItem -LiteralPath $taskPackaged -File -Filter '*.png') {
        $taskTarget = Join-Path $taskSource $taskFile.Name
        Copy-Item -LiteralPath $taskFile.FullName -Destination $taskTarget -Force
        (Get-Item -LiteralPath $taskTarget).LastWriteTime = Get-Date
        if ((Get-FileHash -LiteralPath $taskFile.FullName).Hash -ne (Get-FileHash -LiteralPath $taskTarget).Hash) { throw 'Installed asset differs.' }
    }
    if (@(Get-ChildItem -LiteralPath $taskSource -File -Filter '*.png').Count -ne 8) { throw 'Unexpected source frame count.' }
}
Write-Output 'PASS: installed eight video-guided poses per direction, old frames preserved in backup/quarantine.'
