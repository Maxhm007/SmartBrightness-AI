#ifndef AppVersion
  #define AppVersion "0.4.0"
#endif

[Setup]
AppId={{64DA6A7A-5659-423E-9927-219DE1B66258}
AppName=SmartBrightness AI
AppVersion={#AppVersion}
AppPublisher=SmartBrightness AI
DefaultDirName={localappdata}\Programs\SmartBrightnessAI
DefaultGroupName=SmartBrightness AI
UninstallDisplayIcon={app}\SmartBrightness.App.exe
OutputDir=..\artifacts\windows\installer
OutputBaseFilename=SmartBrightnessAI-Setup-{#AppVersion}
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=lowest
DisableProgramGroupPage=yes
WizardStyle=modern
Compression=lzma2
SolidCompression=yes
SetupLogging=yes
CloseApplications=yes
RestartApplications=yes

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked

[Files]
Source: "..\artifacts\publish\win-x64\SmartBrightness.App.exe"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\SmartBrightness AI"; Filename: "{app}\SmartBrightness.App.exe"
Name: "{autodesktop}\SmartBrightness AI"; Filename: "{app}\SmartBrightness.App.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\SmartBrightness.App.exe"; Description: "Launch SmartBrightness AI"; Flags: postinstall skipifsilent nowait
