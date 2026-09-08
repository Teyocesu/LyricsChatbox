#ifndef AppVersion
  #error AppVersion is required
#endif
#ifndef PublishDir
  #error PublishDir is required
#endif
#ifndef OutputPath
  #error OutputPath is required
#endif

[Setup]
AppId={{79D9DC26-4B18-4DB6-88C5-CE5EA77B6B4E}
AppName=LyricsChatbox
AppVersion={#AppVersion}
AppPublisher=Teyocesu
AppPublisherURL=https://github.com/Teyocesu/LyricsChatbox
DefaultDirName={localappdata}\Programs\LyricsChatbox
DefaultGroupName=LyricsChatbox
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.19041
OutputDir={#OutputPath}
OutputBaseFilename=LyricsChatbox-Setup-{#AppVersion}
SetupIconFile={#PublishDir}\..\..\..\src\LyricsChatbox\Assets\AppIcon.ico
UninstallDisplayIcon={app}\LyricsChatbox.exe
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
RestartApplications=no

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; Flags: unchecked

[Icons]
Name: "{userprograms}\LyricsChatbox"; Filename: "{app}\LyricsChatbox.exe"; WorkingDir: "{app}"
Name: "{userdesktop}\LyricsChatbox"; Filename: "{app}\LyricsChatbox.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Code]
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var StartupCommand: String;
begin
  if CurUninstallStep = usUninstall then
    if RegQueryStringValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'LyricsChatbox', StartupCommand) then
      if CompareText(StartupCommand, '"' + ExpandConstant('{app}\LyricsChatbox.exe') + '"') = 0 then
        RegDeleteValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'LyricsChatbox');
  { User settings, corrections, imported lyrics and cache are deliberately preserved. }
end;
