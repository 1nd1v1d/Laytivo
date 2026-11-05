param([string]$InstallerPath)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$version = ([xml](Get-Content -LiteralPath (Join-Path $projectRoot 'Directory.Build.props') -Raw)).Project.PropertyGroup.Version
if (!$InstallerPath) { $InstallerPath = Join-Path $projectRoot "artifacts\installer\Laytivo-Setup-$version-x64.exe" }
$InstallerPath = (Resolve-Path -LiteralPath $InstallerPath).ProviderPath
$uninstallKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\{B6E7B7F2-9A6B-49AC-A5B9-5040F807D357}_is1'
if (Test-Path -LiteralPath $uninstallKey) { throw 'Bereits installierte Laytivo-Version erkannt. Installer-Test nur auf einer Maschine ohne installierte Laytivo-Version ausführen.' }

$token = [Guid]::NewGuid().ToString('N')
$testRoot = Join-Path $projectRoot "artifacts\installer-smoke-$token"
$appDirectory = Join-Path $testRoot 'App'
$dataDirectory = Join-Path $testRoot 'Data'
$groupName = "Laytivo Installer Test $token"
$groupDirectory = Join-Path ([Environment]::GetFolderPath('Programs')) $groupName
New-Item -ItemType Directory -Path $dataDirectory -Force | Out-Null
$profilePath = Join-Path $dataDirectory 'configuration.json'
'{"schemaVersion":1,"settings":{"startWithWindows":false,"restoreAfterLogin":false,"launchMissingApps":false,"startupDelaySeconds":5},"workspace":null}' | Set-Content -LiteralPath $profilePath -Encoding utf8
$profileHash = (Get-FileHash -LiteralPath $profilePath).Hash
$userProfilePath = Join-Path $env:LOCALAPPDATA 'Laytivo\configuration.json'
$userProfileHash = if (Test-Path -LiteralPath $userProfilePath) { (Get-FileHash -LiteralPath $userProfilePath).Hash } else { $null }
$runKey = [Microsoft.Win32.Registry]::CurrentUser.CreateSubKey('Software\Microsoft\Windows\CurrentVersion\Run')
$oldRun = $runKey.GetValue('Laytivo', $null, [Microsoft.Win32.RegistryValueOptions]::DoNotExpandEnvironmentNames)
$oldRunKind = if ($null -ne $oldRun) { $runKey.GetValueKind('Laytivo') } else { $null }
$checks = [Collections.Generic.List[string]]::new()
$originalProcesses = @()
$testProcess = $null

function Check([bool]$Condition, [string]$Description) {
    if (!$Condition) { throw $Description }
    $checks.Add($Description)
    Write-Output "OK: $Description"
}
function Invoke-Setup([string]$FilePath, [string[]]$Arguments) {
    # Inno Uninstall copies itself to a child process. Wait for the entire tree, not just its launcher.
    $process = Start-Process -FilePath $FilePath -ArgumentList $Arguments -WindowStyle Hidden -PassThru -Wait
    try {
        return $process.ExitCode
    }
    finally { $process.Dispose() }
}
function Install-Test([string]$LogName) {
    $arguments = @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', '/SP-', "/DIR=`"$appDirectory`"", "/GROUP=`"$groupName`"", '/TASKS=!desktopicon', "/LOG=`"$(Join-Path $testRoot $LogName)`"")
    return Invoke-Setup -FilePath $InstallerPath -Arguments $arguments
}
function Uninstall-Test([string]$LogName) {
    return Invoke-Setup -FilePath (Join-Path $appDirectory 'unins000.exe') -Arguments @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', "/LOG=`"$(Join-Path $testRoot $LogName)`"")
}

try {
    # Stop only running instances from this workspace, then restore them in finally.
    $workspaceExe = Join-Path $projectRoot 'artifacts\Laytivo\Laytivo.exe'
    $originalProcesses = @(Get-CimInstance Win32_Process -Filter "Name = 'Laytivo.exe'" | Where-Object { $_.ExecutablePath -eq $workspaceExe })
    foreach ($original in $originalProcesses) { Stop-Process -Id $original.ProcessId -ErrorAction Stop }
    Check ((Install-Test 'install.log') -eq 0) 'Installation im eigenen Testordner erfolgreich'
    Check (Test-Path -LiteralPath (Join-Path $appDirectory 'Laytivo.exe')) 'Installierte EXE vorhanden'
    Check (Test-Path -LiteralPath (Join-Path $appDirectory 'coreclr.dll')) '.NET-Laufzeit enthalten'
    Check (Test-Path -LiteralPath (Join-Path $appDirectory 'ANLEITUNG.html')) 'Anleitung mit installiert'
    Check (Test-Path -LiteralPath (Join-Path $groupDirectory 'Laytivo.lnk')) 'Startmenü-Verknüpfung erstellt'
    Check (Test-Path -LiteralPath (Join-Path $groupDirectory 'Laytivo – Anleitung.lnk')) 'Startmenü-Anleitung erstellt'
    Check ((Get-ItemProperty -LiteralPath $uninstallKey).DisplayVersion -eq $version) 'Benutzerbezogener Deinstallationseintrag mit korrekter Version'

    'Eigene Datei: muss erhalten bleiben.' | Set-Content -LiteralPath (Join-Path $appDirectory 'user-note.txt')
    $ownCommand = '"' + (Join-Path $appDirectory 'Laytivo.exe') + '" --startup'
    $runKey.SetValue('Laytivo', $ownCommand, [Microsoft.Win32.RegistryValueKind]::String)
    $testProcess = Start-Process -FilePath (Join-Path $appDirectory 'Laytivo.exe') -ArgumentList @('--startup', '--data-directory', "`"$dataDirectory`"") -WindowStyle Hidden -PassThru
    $ready = $false
    for ($i = 0; $i -lt 50; $i++) {
        if (Test-Path -LiteralPath (Join-Path $dataDirectory 'logs\laytivo.jsonl')) {
            $events = @(Get-Content -LiteralPath (Join-Path $dataDirectory 'logs\laytivo.jsonl') | ForEach-Object { $_ | ConvertFrom-Json })
            if ($events | Where-Object { $_.action -eq 'started' -and $_.details.background }) { $ready = $true; break }
        }
        if ($testProcess.HasExited) { break }
        Start-Sleep -Milliseconds 200
    }
    Check ($ready -and !$testProcess.HasExited) 'Installierte App startet ohne separat installiertes .NET'
    Check $ready 'Autostartmodus erreicht mit leerem Testprofil den bereiten Hintergrundbetrieb'
    $second = Start-Process -FilePath (Join-Path $appDirectory 'Laytivo.exe') -ArgumentList @('--data-directory', "`"$dataDirectory`"") -WindowStyle Hidden -PassThru
    try {
        Check ($second.WaitForExit(5000) -and $second.ExitCode -eq 0) 'Zweiter EXE-Aufruf beendet sich ohne Doppelinstanz'
        $activated = $false
        for ($i = 0; $i -lt 25; $i++) {
            if (Select-String -LiteralPath (Join-Path $dataDirectory 'logs\laytivo.jsonl') -SimpleMatch 'instance-activated' -Quiet) { $activated = $true; break }
            Start-Sleep -Milliseconds 200
        }
        Check $activated 'Zweiter EXE-Aufruf aktiviert die erste Hintergrundinstanz'
    }
    finally { if (!$second.HasExited) { Stop-Process -Id $second.Id }; $second.Dispose() }
    Stop-Process -Id $testProcess.Id
    $testProcess.WaitForExit(); $testProcess.Dispose()
    $logPath = Join-Path $dataDirectory 'logs\laytivo.jsonl'
    $before = @(Select-String -LiteralPath $logPath -SimpleMatch 'startup-restore-requested').Count
    $testProcess = Start-Process -FilePath (Join-Path $appDirectory 'Laytivo.exe') -ArgumentList @('--data-directory', "`"$dataDirectory`"") -WindowStyle Hidden -PassThru
    $manualReady = $false
    for ($i = 0; $i -lt 50; $i++) {
        $started = @(Get-Content -LiteralPath $logPath | ForEach-Object { $_ | ConvertFrom-Json } | Where-Object action -eq 'started')
        if ($started.Count -ge 2) { $manualReady = $true; break }
        Start-Sleep -Milliseconds 200
    }
    Check $manualReady 'Manueller Erststart für verzögerten Windows-Autostart bereit'
    for ($request = 0; $request -lt 2; $request++) {
        $forward = Start-Process -FilePath (Join-Path $appDirectory 'Laytivo.exe') -ArgumentList @('--startup', '--data-directory', "`"$dataDirectory`"") -WindowStyle Hidden -PassThru
        try { if (!$forward.WaitForExit(5000)) { Stop-Process -Id $forward.Id; throw 'Autostart-Zweitinstanz hängt' } }
        finally { $forward.Dispose() }
        for ($i = 0; $i -lt 25; $i++) {
            if (@(Select-String -LiteralPath $logPath -SimpleMatch 'startup-restore-requested').Count -gt $before) { break }
            Start-Sleep -Milliseconds 200
        }
    }
    Start-Sleep -Milliseconds 300
    Check (@(Select-String -LiteralPath $logPath -SimpleMatch 'startup-restore-requested').Count -eq ($before + 1)) 'Verzögerter Autostart wird genau einmal an laufende Instanz weitergereicht'
    Check ((Install-Test 'blocked-update.log') -ne 0) 'Update wird bei laufender App kontrolliert blockiert'
    Check ((Uninstall-Test 'blocked-uninstall.log') -ne 0) 'Deinstallation wird bei laufender App kontrolliert blockiert'
    Stop-Process -Id $testProcess.Id
    $testProcess.WaitForExit()
    $testProcess.Dispose(); $testProcess = $null

    Check ((Install-Test 'upgrade.log') -eq 0) 'Erneute Installation als Update erfolgreich'
    Check ((Get-FileHash -LiteralPath $profilePath).Hash -eq $profileHash) 'Einstellungen bleiben beim Update erhalten'
    Check ((Uninstall-Test 'uninstall.log') -eq 0) 'Deinstallation erfolgreich'
    Check (!(Test-Path -LiteralPath (Join-Path $appDirectory 'Laytivo.exe'))) 'Installierte Programmdateien entfernt'
    Check (!(Test-Path -LiteralPath $uninstallKey)) 'Deinstallationseintrag entfernt'
    Check (!(Test-Path -LiteralPath (Join-Path $groupDirectory 'Laytivo.lnk'))) 'Startmenü-Verknüpfung entfernt'
    Check ($null -eq $runKey.GetValue('Laytivo')) 'Eigener Autostart-Eintrag entfernt'
    Check ((Get-FileHash -LiteralPath $profilePath).Hash -eq $profileHash) 'Nutzerdaten bleiben nach Deinstallation erhalten'
    Check (Test-Path -LiteralPath (Join-Path $appDirectory 'user-note.txt')) 'Nicht installierte Benutzerdateien werden nicht gelöscht'

    Check ((Install-Test 'install-foreign-run.log') -eq 0) 'Erneute Installation für Autostart-Abgrenzung erfolgreich'
    $foreignCommand = if ($null -ne $oldRun) { $oldRun } else { '"C:\OtherPortableLaytivo\Laytivo.exe" --startup' }
    $runKey.SetValue('Laytivo', $foreignCommand, [Microsoft.Win32.RegistryValueKind]::String)
    Check ((Uninstall-Test 'uninstall-foreign-run.log') -eq 0) 'Zweite Deinstallation erfolgreich'
    Check ($runKey.GetValue('Laytivo') -eq $foreignCommand) 'Autostart einer anderen Installation bleibt erhalten'
    $userProfileAfter = if (Test-Path -LiteralPath $userProfilePath) { (Get-FileHash -LiteralPath $userProfilePath).Hash } else { $null }
    Check ($userProfileHash -eq $userProfileAfter) 'Echtes Benutzerprofil während aller Prüfungen unverändert'
    @{ passed = $true; timeUtc = [DateTimeOffset]::UtcNow; checks = $checks; version = $version } | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $testRoot 'result.json')
    Write-Output "Installer-Test erfolgreich: $($checks.Count) Prüfungen. $testRoot"
}
catch {
    @{ passed = $false; checks = $checks; error = $_.Exception.Message } | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $testRoot 'result.json')
    throw
}
finally {
    if ($null -ne $testProcess) { if (!$testProcess.HasExited) { Stop-Process -Id $testProcess.Id }; $testProcess.Dispose() }
    if (Test-Path -LiteralPath (Join-Path $appDirectory 'unins000.exe')) { $null = Uninstall-Test 'cleanup.log' }
    if ($null -eq $oldRun) { $runKey.DeleteValue('Laytivo', $false) } else { $runKey.SetValue('Laytivo', $oldRun, $oldRunKind) }
    $runKey.Dispose()
    foreach ($original in $originalProcesses) {
        $prefix = if ($original.CommandLine.StartsWith('"')) { '"' + $original.ExecutablePath + '"' } else { $original.ExecutablePath }
        $arguments = ($original.CommandLine.Substring($prefix.Length).Trim() -replace '(?i)(?<!\S)--startup(?!\S)', '').Trim()
        $start = @{ FilePath = $original.ExecutablePath; WorkingDirectory = (Split-Path -Parent $original.ExecutablePath); WindowStyle = 'Hidden' }
        if ($arguments) { $start.ArgumentList = $arguments }
        Start-Process @start
    }
}
