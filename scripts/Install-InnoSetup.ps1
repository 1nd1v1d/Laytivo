$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$toolDirectory = Join-Path $projectRoot '.tools\inno'
$compilerDirectory = Join-Path $toolDirectory 'compiler'
$compiler = Join-Path $compilerDirectory 'ISCC.exe'
if (Test-Path -LiteralPath $compiler) { Write-Output $compiler; return }

New-Item -ItemType Directory -Path $toolDirectory -Force | Out-Null
$download = Join-Path $toolDirectory 'innosetup-7.1.1-x64.exe'
$expectedHash = '8A5A6711C3766A6E5FB74F280BA0E3551E719CE94538D901FDBD1F8F9920B3F0'
if (!(Test-Path -LiteralPath $download)) {
    Invoke-WebRequest -Uri 'https://github.com/jrsoftware/issrc/releases/download/is-7_1_1/innosetup-7.1.1-x64.exe' -OutFile $download
}
if ((Get-FileHash -LiteralPath $download -Algorithm SHA256).Hash -ne $expectedHash) { throw 'Inno-Setup-Prüfsumme stimmt nicht.' }
$signature = Get-AuthenticodeSignature -LiteralPath $download
if ($signature.Status -ne 'Valid') { throw "Ungültige Compiler-Signatur: $($signature.Status)." }

$log = Join-Path $toolDirectory 'compiler-install.log'
$process = Start-Process -FilePath $download -ArgumentList @('/PORTABLE=1', '/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', '/SP-', "/DIR=`"$compilerDirectory`"", "/LOG=`"$log`"") -WindowStyle Hidden -PassThru -Wait
try {
    $process.WaitForExit()
    if ($process.ExitCode -ne 0 -or !(Test-Path -LiteralPath $compiler)) { throw "Inno Setup konnte nicht eingerichtet werden. Protokoll: $log" }
}
finally { $process.Dispose() }
Write-Output $compiler
