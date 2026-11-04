$ErrorActionPreference = 'Stop'
$published = Join-Path $PSScriptRoot 'artifacts\Laytivo\Laytivo.exe'
if (Test-Path -LiteralPath $published) {
    Start-Process -FilePath $published -WorkingDirectory (Split-Path -Parent $published) -WindowStyle Hidden
    exit
}
$localSdk = Join-Path $PSScriptRoot '.tools\dotnet'
if (Test-Path -LiteralPath (Join-Path $localSdk 'dotnet.exe')) { $env:DOTNET_ROOT = $localSdk }
$exe = Join-Path $PSScriptRoot 'src\Laytivo.App\bin\Release\net10.0-windows\Laytivo.exe'
if (!(Test-Path -LiteralPath $exe)) { throw 'Bitte zuerst scripts\Build.ps1 -Publish ausführen.' }
Start-Process -FilePath $exe -WorkingDirectory (Split-Path -Parent $exe) -WindowStyle Hidden
