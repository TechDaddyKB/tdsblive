; Builds are performed and qualified in GitHub Actions. No user data is installed.
#ifndef AppVersion
  #error AppVersion is required
#endif
#ifndef PublishDirectory
  #error PublishDirectory is required
#endif
#ifndef OutputDirectory
  #error OutputDirectory is required
#endif
#ifndef RepositoryDirectory
  #error RepositoryDirectory is required
#endif

[Setup]
AppId={{7B195AB4-7145-45BC-A2F9-EA8A320EBFB1}
AppName=TDSBLive
AppVersion={#AppVersion}
AppPublisher=TechDaddyKB
AppPublisherURL=https://github.com/TechDaddyKB/tdsblive
AppSupportURL=https://github.com/TechDaddyKB/tdsblive/wiki
DefaultDirName={localappdata}\Programs\TDSBLive
DefaultGroupName=TDSBLive
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir={#OutputDirectory}
OutputBaseFilename=TDSBLive-{#AppVersion}-win-x64-setup
LicenseFile={#RepositoryDirectory}\LICENSE
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
DisableProgramGroupPage=yes
CloseApplications=yes
RestartApplications=no
UninstallDisplayName=TDSBLive

[Tasks]
Name: desktopicon; Description: "Create a desktop shortcut"; Flags: unchecked
Name: startup; Description: "Start TDSBLive when I sign in to Windows"; Flags: unchecked

[Files]
Source: "{#PublishDirectory}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\TDSBLive"; Filename: "{app}\TDSBLive.exe"; Parameters: "--TDSBLive:OpenEditor=true"
Name: "{autoprograms}\TDSBLive User Guide"; Filename: "{app}\guide\Home.html"
Name: "{autodesktop}\TDSBLive"; Filename: "{app}\TDSBLive.exe"; Parameters: "--TDSBLive:OpenEditor=true"; Tasks: desktopicon
Name: "{userstartup}\TDSBLive"; Filename: "{app}\TDSBLive.exe"; Tasks: startup

[Run]
Filename: "{app}\TDSBLive.exe"; Parameters: "--TDSBLive:OpenEditor=true"; Description: "Open TDSBLive"; Flags: nowait postinstall skipifsilent

; Application data lives separately in LocalAppData\TDSBLive and survives uninstall.
