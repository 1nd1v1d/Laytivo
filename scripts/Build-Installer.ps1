param([string]$CompilerPath, [switch]$SkipChecks)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
Set-Location -LiteralPath $projectRoot

if (!$SkipChecks) { & (Join-Path $PSScriptRoot 'Build.ps1') }
$localDotnet = Join-Path $projectRoot '.tools\dotnet\dotnet.exe'
$dotnet = if (Test-Path -LiteralPath $localDotnet) { $localDotnet } else { (Get-Command dotnet -ErrorAction Stop).Source }
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_CLI_HOME = Join-Path $projectRoot '.tools\cli'
$env:NUGET_PACKAGES = Join-Path $projectRoot '.tools\nuget'
$env:DOTNET_ROOT = Split-Path -Parent $dotnet

if (!$CompilerPath) { $CompilerPath = Join-Path $projectRoot '.tools\inno\compiler\ISCC.exe' }
if (!(Test-Path -LiteralPath $CompilerPath)) { throw 'Inno Setup 7 fehlt. Zuerst scripts\Install-InnoSetup.ps1 ausführen oder -CompilerPath angeben.' }
$version = ([xml](Get-Content -LiteralPath 'Directory.Build.props' -Raw)).Project.PropertyGroup.Version
if ($version -notmatch '^\d+\.\d+\.\d+$') { throw 'Ungültige Version in Directory.Build.props.' }

& $dotnet publish 'src/Laytivo.App/Laytivo.App.csproj' -c Release -r win-x64 --self-contained true -o 'artifacts/installer-payload' -m:1 -nr:false -p:UseSharedCompilation=false
if ($LASTEXITCODE -ne 0) { throw 'Publish für den Installer fehlgeschlagen.' }
& $CompilerPath /Qp "/DAppVersion=$version" 'installer/Laytivo.iss'
if ($LASTEXITCODE -ne 0) { throw 'Installer-Kompilierung fehlgeschlagen.' }

$releaseDirectory = Join-Path $projectRoot 'artifacts\installer'
Copy-Item -LiteralPath 'ANLEITUNG.html' -Destination $releaseDirectory -Force
Compress-Archive -Path 'artifacts/installer-payload/*', 'ANLEITUNG.html' -DestinationPath (Join-Path $releaseDirectory "Laytivo-$version-win-x64.zip") -Force
$names = @("Laytivo-Setup-$version-x64.exe", "Laytivo-$version-win-x64.zip", 'ANLEITUNG.html')
$hashes = foreach ($name in $names) {
    $hash = (Get-FileHash -LiteralPath (Join-Path $releaseDirectory $name) -Algorithm SHA256).Hash.ToLowerInvariant()
    "$hash  $name"
}
$hashes | Set-Content -LiteralPath (Join-Path $releaseDirectory 'SHA256SUMS.txt') -Encoding ascii
Write-Output "Installer, portable ZIP, Anleitung und Prüfsummen: $releaseDirectory"
