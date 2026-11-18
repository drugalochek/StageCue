#define AppName "StageCue"
#define AppVersion "0.1.0"
#define AppPublisher "StageCue"
#define AppExeName "StageCue.exe"

[Setup]
AppId={{A6D7F04B-6D93-4E2C-8A51-2E5E7A9F12C4}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={localappdata}\Programs\StageCue
DefaultGroupName=StageCue
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
OutputDir=..\artifacts\installer
OutputBaseFilename=StageCue-Setup-x64
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
SetupLogging=yes
SetupIconFile=..\\Assets\\StageCue.ico
UninstallDisplayIcon={app}\{#AppExeName}
CloseApplications=yes
RestartApplications=no
SetupMutex=StageCueSetup
Uninstallable=yes

[Languages]
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Создать ярлык на рабочем столе"; GroupDescription: "Ярлыки:"; Flags: unchecked
Name: "startup"; Description: "Запускать StageCue вместе с Windows"; GroupDescription: "Дополнительно:"; Flags: unchecked

[Files]
Source: "..\artifacts\portable\StageCue.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\artifacts\portable\README_RU.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\artifacts\portable\StageCueLogo.svg"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\StageCue"; Filename: "{app}\{#AppExeName}"
Name: "{autodesktop}\StageCue"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueName: "StageCue"; ValueData: """{app}\{#AppExeName}"""; Tasks: startup; Flags: uninsdeletevalue

[Run]
Filename: "{app}\{#AppExeName}"; Description: "Запустить StageCue"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; User data is stored in %LOCALAPPDATA%\StageCue and intentionally preserved.
