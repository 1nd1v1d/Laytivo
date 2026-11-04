; Build with scripts/Build-Installer.ps1 (Inno Setup 7).
#ifndef AppVersion
  #error AppVersion must be supplied by Build-Installer.ps1
#endif

[Setup]
AppId={{B6E7B7F2-9A6B-49AC-A5B9-5040F807D357}
AppName=Laytivo
AppVersion={#AppVersion}
AppPublisher=1nd1v1d
AppPublisherURL=https://github.com/1nd1v1d/Laytivo
AppSupportURL=https://github.com/1nd1v1d/Laytivo/issues
AppUpdatesURL=https://github.com/1nd1v1d/Laytivo/releases
DefaultDirName={localappdata}\Programs\Laytivo
DefaultGroupName=Laytivo
DisableProgramGroupPage=no
AllowNoIcons=yes
PrivilegesRequired=lowest
SetupArchitecture=x64
ArchitecturesAllowed=x64os
ArchitecturesInstallIn64BitMode=x64os
MinVersion=10.0.22000
AppMutex=Local\Laytivo-Running
SetupMutex=Local\Laytivo-Setup
CloseApplications=no
RestartApplications=no
OutputDir=..\artifacts\installer
OutputBaseFilename=Laytivo-Setup-{#AppVersion}-x64
SetupIconFile=..\src\Laytivo.App\Assets\Laytivo.ico
UninstallDisplayIcon={app}\Laytivo.exe
VersionInfoVersion={#AppVersion}.0
Compression=lzma2/normal
SolidCompression=yes
WizardStyle=modern
DisableWelcomePage=no
UninstallDisplayName=Laytivo

[Languages]
Name: "german"; MessagesFile: "compiler:Languages\German.isl"

[Tasks]
Name: "desktopicon"; Description: "Desktop-Verknüpfung erstellen"; Flags: unchecked

[Files]
Source: "..\artifacts\installer-payload\*"; DestDir: "{app}"; Excludes: "*.pdb"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\ANLEITUNG.html"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\Laytivo"; Filename: "{app}\Laytivo.exe"; WorkingDir: "{app}"
Name: "{group}\Laytivo – Anleitung"; Filename: "{app}\ANLEITUNG.html"
Name: "{autodesktop}\Laytivo"; Filename: "{app}\Laytivo.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\Laytivo.exe"; WorkingDir: "{app}"; Description: "Laytivo jetzt starten"; Flags: nowait postinstall skipifsilent

[Code]
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  CurrentCommand: String;
  OwnCommand: String;
begin
  if CurUninstallStep = usPostUninstall then
  begin
    OwnCommand := '"' + ExpandConstant('{app}\Laytivo.exe') + '" --startup';
    if RegQueryStringValue(HKCU64, 'Software\Microsoft\Windows\CurrentVersion\Run', 'Laytivo', CurrentCommand) then
    begin
      if CompareText(CurrentCommand, OwnCommand) = 0 then
      begin
        if RegDeleteValue(HKCU64, 'Software\Microsoft\Windows\CurrentVersion\Run', 'Laytivo') then
          Log('Own autostart entry removed.')
        else
          Log('Own autostart entry could not be removed.');
      end;
    end
    else
      Log('No autostart entry found.');
  end;
end;
