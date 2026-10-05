param(
    [switch]$SelfContained,
    [string]$OutputDirectory = 'artifacts/publish/p22-win-x64',
    [string]$ArchiveName = 'DesktopPet-P22-win-x64.zip'
)
$ErrorActionPreference = 'Stop'
function Rename-TaskPublishDirectory {
    param([string]$Source, [string]$NewLeaf)
    # A recently published EXE can temporarily hold a directory handle while
    # Windows indexes/scans it. Retry the same rename; never merge directories.
    for ($taskAttempt = 0; $taskAttempt -lt 24; $taskAttempt++) {
        try {
            Rename-Item -LiteralPath $Source -NewName $NewLeaf -ErrorAction Stop
            return
        } catch {
            if ($taskAttempt -eq 23) { throw }
            Start-Sleep -Milliseconds 250
        }
    }
}
try {
    . "$PSScriptRoot\use-dotnet.ps1"
    Push-Location (Split-Path -Parent $PSScriptRoot)
    try {
        $taskOutput = [IO.Path]::GetFullPath((Join-Path (Get-Location) $OutputDirectory))
        $taskPublishRoot = [IO.Path]::GetFullPath((Join-Path (Get-Location) 'artifacts\publish'))
        $taskPublishPrefix = $taskPublishRoot.TrimEnd('\') + '\'
        if (-not $taskOutput.StartsWith($taskPublishPrefix, [StringComparison]::OrdinalIgnoreCase)) {
            throw 'OutputDirectory must be a child directory of this workspace artifacts/publish/.'
        }
        $taskOutputParent = Split-Path -Parent $taskOutput
        New-Item -ItemType Directory -Path $taskOutputParent -Force | Out-Null
        $taskStage = Join-Path $taskOutputParent ('.p22-stage-' + [Guid]::NewGuid().ToString('N'))
        if ($SelfContained) {
            dotnet publish src/DesktopPet/DesktopPet.csproj -c Release -r win-x64 --self-contained true -p:PublishTrimmed=false -o $taskStage
            if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
        } else {
            # Prefer the official runtime already installed on this build machine.
            $taskDotnetRoot = Split-Path -Parent (Get-Command dotnet).Source
            $taskDesktopRoot = Join-Path $taskDotnetRoot 'shared\Microsoft.WindowsDesktop.App'
            if (-not (Test-Path -LiteralPath $taskDesktopRoot)) {
                throw 'No local Windows Desktop runtime found. Use -SelfContained on an online build machine.'
            }
            $taskRuntimeVersion = Get-ChildItem -LiteralPath $taskDesktopRoot -Directory |
                Where-Object { $_.Name -match '^10\.\d+\.\d+$' } |
                Sort-Object { [version]$_.Name } -Descending |
                Where-Object { (Test-Path -LiteralPath (Join-Path $taskDotnetRoot "shared\Microsoft.NETCore.App\$($_.Name)")) -and
                    (Test-Path -LiteralPath (Join-Path $taskDotnetRoot "host\fxr\$($_.Name)")) } |
                Select-Object -First 1 -ExpandProperty Name
            if (-not $taskRuntimeVersion) { throw 'No matching .NET 10 Core/Desktop/host runtime found.' }
            $taskOfflineFeed = Join-Path (Get-Location) '.tools\offline-feed'
            New-Item -ItemType Directory -Path $taskOfflineFeed -Force | Out-Null
            dotnet publish src/DesktopPet/DesktopPet.csproj -c Release -r win-x64 --self-contained false -p:PublishTrimmed=false -p:AppHostDotNetSearch=AppRelative -p:AppHostRelativeDotNet=runtime -p:NuGetAudit=false --source $taskOfflineFeed -o $taskStage
            if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
            $taskRuntimeOutput = Join-Path $taskStage 'runtime'
            foreach ($taskRelativeRuntime in @("host\fxr\$taskRuntimeVersion", "shared\Microsoft.NETCore.App\$taskRuntimeVersion", "shared\Microsoft.WindowsDesktop.App\$taskRuntimeVersion")) {
                $taskRuntimeParent = Join-Path $taskRuntimeOutput (Split-Path -Parent $taskRelativeRuntime)
                New-Item -ItemType Directory -Path $taskRuntimeParent -Force | Out-Null
                Copy-Item -LiteralPath (Join-Path $taskDotnetRoot $taskRelativeRuntime) -Destination $taskRuntimeParent -Recurse -Force
            }
            foreach ($taskRuntimeFile in @('dotnet.exe', 'LICENSE.txt', 'ThirdPartyNotices.txt')) {
                Copy-Item -LiteralPath (Join-Path $taskDotnetRoot $taskRuntimeFile) -Destination $taskRuntimeOutput -Force
            }
        }
        if (-not (Test-Path -LiteralPath (Join-Path $taskStage 'DesktopPet.exe'))) {
            throw 'Publish finished without DesktopPet.exe.'
        }
        $taskPrevious = $null
        if (Test-Path -LiteralPath $taskOutput) {
            $taskPrevious = Join-Path $taskOutputParent ('.p22-previous-' + [Guid]::NewGuid().ToString('N'))
            Rename-TaskPublishDirectory $taskOutput ([IO.Path]::GetFileName($taskPrevious))
        }
        try { Rename-TaskPublishDirectory $taskStage ([IO.Path]::GetFileName($taskOutput)) }
        catch {
            if ($taskPrevious -and -not (Test-Path -LiteralPath $taskOutput)) {
                Rename-TaskPublishDirectory $taskPrevious ([IO.Path]::GetFileName($taskOutput))
            }
            throw
        }
        if ([IO.Path]::GetFileName($ArchiveName) -ne $ArchiveName -or -not $ArchiveName.EndsWith('.zip')) {
            throw 'ArchiveName must be a ZIP file name without directory components.'
        }
        $taskArchive = Join-Path (Join-Path (Get-Location) 'artifacts') $ArchiveName
        Compress-Archive -LiteralPath $taskOutput -DestinationPath $taskArchive -CompressionLevel Optimal -Force
        if ($taskPrevious) {
            $taskPreviousResolved = [IO.Path]::GetFullPath($taskPrevious)
            if (-not $taskPreviousResolved.StartsWith($taskPublishPrefix, [StringComparison]::OrdinalIgnoreCase) -or
                -not ([IO.Path]::GetFileName($taskPreviousResolved).StartsWith('.p22-previous-'))) {
                throw 'Refusing an unverified previous build path.'
            }
            Write-Host "Previous build retained: $taskPreviousResolved"
        }
        Write-Host "EXE: $(Join-Path $taskOutput 'DesktopPet.exe')"
        Write-Host "ZIP: $taskArchive"
    } finally { Pop-Location }
} catch {
    Write-Host $_.ScriptStackTrace
    Write-Error $_
    exit 1
}
exit 0
