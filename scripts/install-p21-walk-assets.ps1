$ErrorActionPreference = 'Stop'
$taskRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
foreach ($taskDirection in @('Left','Right')) {
    $taskAction = 'Walk' + $taskDirection
    $taskSource = Join-Path $taskRoot ('src/DesktopPet/Assets/Pet/' + $taskAction)
    $taskSaved = Join-Path $taskRoot ('artifacts/imagegen/p21/previous-runtime/' + $taskAction)
    $taskPackaged = Join-Path $taskRoot ('artifacts/imagegen/p21/packaged/' + $taskAction)
    $taskFrames = @(Get-ChildItem -LiteralPath $taskPackaged -File -Filter '*.png')
    if ($taskFrames.Count -ne 8) { throw 'Expected eight packaged walking frames.' }
    New-Item -ItemType Directory -Path $taskSaved -Force | Out-Null
    foreach ($taskFile in Get-ChildItem -LiteralPath $taskSource -File -Filter '*.png') {
        $taskSavedFile = Join-Path $taskSaved $taskFile.Name
        if (-not (Test-Path -LiteralPath $taskSavedFile)) {
            Copy-Item -LiteralPath $taskFile.FullName -Destination $taskSavedFile
        }
    }
    foreach ($taskFile in $taskFrames) {
        $taskTarget = Join-Path $taskSource $taskFile.Name
        Copy-Item -LiteralPath $taskFile.FullName -Destination $taskTarget -Force
        (Get-Item -LiteralPath $taskTarget).LastWriteTime = Get-Date
        if ((Get-FileHash -LiteralPath $taskFile.FullName).Hash -ne (Get-FileHash -LiteralPath $taskTarget).Hash) {
            throw 'Installed frame differs from selected asset.'
        }
    }
    if (@(Get-ChildItem -LiteralPath $taskSource -File -Filter '*.png').Count -ne 8) {
        throw 'Unexpected source frame count.'
    }
}
Write-Output 'PASS: sixteen selected walking frames installed; previous P20 frames preserved.'
