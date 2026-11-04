param([switch]$Publish, [switch]$DesktopTests)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
Set-Location -LiteralPath $projectRoot
$localDotnet = Join-Path $projectRoot '.tools\dotnet\dotnet.exe'
$dotnet = if (Test-Path -LiteralPath $localDotnet) { $localDotnet } else { (Get-Command dotnet -ErrorAction Stop).Source }
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_CLI_HOME = Join-Path $projectRoot '.tools\cli'
$env:NUGET_PACKAGES = Join-Path $projectRoot '.tools\nuget'
$env:DOTNET_ROOT = Split-Path -Parent $dotnet

function Invoke-Dotnet([string[]]$Arguments) {
    & $dotnet @Arguments
    if ($LASTEXITCODE -ne 0) { throw "dotnet $($Arguments -join ' ') fehlgeschlagen (Exit $LASTEXITCODE)." }
}

Invoke-Dotnet -Arguments @('restore', 'Laytivo.sln', '--disable-parallel', '-m:1', '-nr:false')
Invoke-Dotnet -Arguments @('format', 'Laytivo.sln', '--verify-no-changes', '--no-restore', '--verbosity', 'minimal')
Invoke-Dotnet -Arguments @('build', 'Laytivo.sln', '-c', 'Release', '--no-restore', '-m:1', '-nr:false', '-p:UseSharedCompilation=false')
Invoke-Dotnet -Arguments @('test', 'Laytivo.sln', '-c', 'Release', '--no-build', '--no-restore', '-m:1', '-nr:false')
Invoke-Dotnet -Arguments @('list', 'Laytivo.sln', 'package', '--vulnerable', '--include-transitive')
if ($DesktopTests) {
    & 'tests/Laytivo.DesktopSmoke/bin/Release/net10.0-windows/Laytivo.DesktopSmoke.exe' --run 'artifacts/desktop-smoke'
    if ($LASTEXITCODE -ne 0) { throw 'Desktop-Smoke fehlgeschlagen.' }
}
if ($Publish) {
    Invoke-Dotnet -Arguments @('publish', 'src/Laytivo.App/Laytivo.App.csproj', '-c', 'Release', '-r', 'win-x64', '--self-contained', 'true', '-o', 'artifacts/Laytivo', '-m:1', '-nr:false', '-p:UseSharedCompilation=false')
    Compress-Archive -Path 'artifacts/Laytivo/*' -DestinationPath 'artifacts/Laytivo-win-x64.zip' -Force
}
