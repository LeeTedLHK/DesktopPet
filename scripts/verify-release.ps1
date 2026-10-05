param(
    [switch]$StartCheck,
    [string]$OutputDirectory = 'artifacts/publish/p22-win-x64',
    [string]$ArchiveName = 'DesktopPet-P22-win-x64.zip'
)
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path -Parent $PSScriptRoot
$taskSource = Join-Path $taskRoot 'src\DesktopPet\Assets\Pet'
$taskOutput = Join-Path $taskRoot $OutputDirectory
$taskLeaf = Split-Path -Leaf $taskOutput
$taskAssetOutput = Join-Path $taskOutput 'Assets\Pet'
$taskExpected = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
$taskCount = 0
# Match csproj content: ball photograph and walking video are development references only.
foreach ($taskFile in Get-ChildItem -LiteralPath $taskSource -Recurse -File | Where-Object { $_.Extension -in '.png','.json' -or $_.Name -eq 'pet.jpg' }) {
    $taskRelative = $taskFile.FullName.Substring($taskSource.Length).TrimStart('\')
    [void]$taskExpected.Add($taskRelative)
    $taskTarget = Join-Path $taskAssetOutput $taskRelative
    if (-not (Test-Path -LiteralPath $taskTarget)) { throw "Missing published asset: $taskRelative" }
    if ((Get-FileHash -LiteralPath $taskFile.FullName).Hash -ne (Get-FileHash -LiteralPath $taskTarget).Hash) {
        throw "Asset hash differs: $taskRelative"
    }
    $taskCount++
}
foreach ($taskFile in Get-ChildItem -LiteralPath $taskAssetOutput -Recurse -File | Where-Object { $_.Extension -in '.png','.jpg','.json' }) {
    $taskRelative = $taskFile.FullName.Substring($taskAssetOutput.Length).TrimStart('\')
    if (-not $taskExpected.Contains($taskRelative)) { throw "Stale published asset: $taskRelative" }
}
Write-Output "PASS: $taskCount published assets match source hashes, no stale images"
$taskRuntimeOutput = Join-Path $taskOutput 'runtime'
& (Join-Path $taskRuntimeOutput 'dotnet.exe') --list-runtimes
if ($LASTEXITCODE -ne 0) { throw 'Private runtime is unusable' }
Add-Type -AssemblyName System.IO.Compression.FileSystem
$taskArchive = [IO.Compression.ZipFile]::OpenRead((Join-Path (Join-Path $taskRoot 'artifacts') $ArchiveName))
try {
    $taskNames = @($taskArchive.Entries | ForEach-Object { $_.FullName.Replace('\','/') })
    foreach ($taskRequired in @("$taskLeaf/DesktopPet.exe", "$taskLeaf/runtime/dotnet.exe",
            "$taskLeaf/Assets/Pet/Petting/petting_000.png", "$taskLeaf/Assets/Pet/LookRight/lookright_002.png",
            "$taskLeaf/Assets/Pet/RunLeft/run_left_000.png", "$taskLeaf/Assets/Pet/RunRight/run_right_007.png",
            "$taskLeaf/Assets/Pet/CarryRight/carry_right_007.png", "$taskLeaf/Assets/Pet/JumpLeft/jump_left_007.png",
            "$taskLeaf/Assets/Pet/DropLeft/drop_left_003.png", "$taskLeaf/Assets/Pet/mouth-anchors.json")) {
        if ($taskRequired -notin $taskNames) { throw "ZIP missing: $taskRequired" }
    }
    foreach ($taskAction in @('WalkLeft','WalkRight','RunLeft','RunRight','CarryLeft','CarryRight','JumpLeft','JumpRight','Idle','Happy','Sleep')) {
        $taskActionPrefix = "$taskLeaf/Assets/Pet/$taskAction/"
        $taskFrames = @($taskNames | Where-Object { $_.StartsWith($taskActionPrefix) -and $_.EndsWith('.png') })
        $taskExpectedFrames = 8
        if ($taskFrames.Count -ne $taskExpectedFrames) { throw "ZIP has an unexpected frame set for $taskAction" }
    }
    foreach ($taskAction in @('StartLeft','StartRight','StopLeft','StopRight','TurnLeft','TurnRight','PickupLeft','PickupRight','DropLeft','DropRight')) {
        $taskActionPrefix = "$taskLeaf/Assets/Pet/$taskAction/"
        $taskFrames = @($taskNames | Where-Object { $_.StartsWith($taskActionPrefix) -and $_.EndsWith('.png') })
        if ($taskFrames.Count -ne 4) { throw "ZIP has an unexpected frame set for $taskAction" }
    }
    $taskFeedingFrames = @($taskNames | Where-Object { $_.StartsWith("$taskLeaf/Assets/Pet/Feeding/") -and $_.EndsWith('.png') })
    if ($taskFeedingFrames.Count -ne 6) { throw 'ZIP has an unexpected Feeding frame set' }
    if ($taskNames | Where-Object { $_ -match 'stale_check_999|\.p(?:[2-9]|1[0-9]|2[01])-stage-' }) { throw 'ZIP contains staging/stale files' }
    Write-Output "PASS: ZIP includes executable, runtime and new animations ($($taskNames.Count) entries)"
} finally { $taskArchive.Dispose() }
if ($StartCheck) {
    $taskExe = Join-Path $taskOutput 'DesktopPet.exe'
    $taskProcess = Start-Process -FilePath $taskExe -WorkingDirectory $taskOutput -WindowStyle Hidden -PassThru
    if ($taskProcess.WaitForExit(5000)) { throw "EXE exited during startup: $($taskProcess.ExitCode)" }
    $taskProcess.Refresh()
    $taskModuleNames = @('hostfxr.dll','hostpolicy.dll','coreclr.dll','wpfgfx_cor3.dll')
    $taskModules = @($taskProcess.Modules | Where-Object { $_.ModuleName -in $taskModuleNames } |
        Select-Object ModuleName,FileName)
    if ($taskModules.Count -ne 4) { throw 'Startup did not load all expected runtime modules' }
    foreach ($taskModule in $taskModules) {
        if (-not $taskModule.FileName.StartsWith($taskRuntimeOutput + '\', [StringComparison]::OrdinalIgnoreCase)) {
            throw "System runtime unexpectedly loaded: $($taskModule.ModuleName)"
        }
    }
    $taskLogFile = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) ("DesktopPet\logs\" + (Get-Date -Format 'yyyy-MM-dd') + '.log')
    $taskSignalBefore = @(Get-Content -LiteralPath $taskLogFile -ErrorAction SilentlyContinue | Select-String -SimpleMatch 'Existing instance revealed by startup signal').Count
    $taskSecond = Start-Process -FilePath $taskExe -WorkingDirectory $taskOutput -WindowStyle Hidden -PassThru
    if (-not $taskSecond.WaitForExit(5000) -or $taskSecond.ExitCode -ne 0) { throw 'Single-instance startup check failed' }
    $taskSignalReceived = $false
    for ($taskSignalAttempt = 0; $taskSignalAttempt -lt 20; $taskSignalAttempt++) {
        $taskSignalAfter = @(Get-Content -LiteralPath $taskLogFile -ErrorAction SilentlyContinue | Select-String -SimpleMatch 'Existing instance revealed by startup signal').Count
        if ($taskSignalAfter -gt $taskSignalBefore) { $taskSignalReceived = $true; break }
        Start-Sleep -Milliseconds 100
    }
    if (-not $taskSignalReceived) { throw 'Second instance exited without revealing existing instance' }
    $taskProcess.Refresh()
    if ($taskProcess.HasExited -or -not $taskProcess.Responding) { throw 'Final EXE is not responding' }
    $taskReport = @{ ProcessId=$taskProcess.Id; Responding=$taskProcess.Responding; Exe=$taskExe;
        RuntimeModules=$taskModules; DuplicateExitCode=$taskSecond.ExitCode; ExistingInstanceSignal=$taskSignalReceived; AssetCount=$taskCount }
    $taskReport | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $taskRoot "artifacts\verification\$taskLeaf-startup.json") -Encoding UTF8
    Write-Output "PASS: final EXE running (PID $($taskProcess.Id)), private runtime loaded, duplicate startup reveals existing instance and exits 0"
}
