; Presso Inno Setup script
#define MyAppName       "Presso"
#define MyAppVersion    "1.2.0"
#define MyAppPublisher  "Presso"
#define MyAppExeName    "Presso.exe"

[Setup]
AppId={{B5F1F7C2-9E2A-4F0C-9E29-7C9C9E0B6A11}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={commonpf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
PrivilegesRequired=admin
ArchitecturesInstallIn64BitMode=x64compatible
ArchitecturesAllowed=x64compatible
OutputDir=output
OutputBaseFilename=PressoSetup-{#MyAppVersion}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
UninstallDisplayIcon={app}\{#MyAppExeName}
UninstallDisplayName={#MyAppName}
SetupIconFile=..\src\Presso\presso_icon.ico
LicenseFile=..\LICENSE
CloseApplications=force

[Languages]
Name: "japanese"; MessagesFile: "compiler:Languages\Japanese.isl"

[Tasks]
Name: "desktopicon"; Description: "デスクトップにショートカットを作成する"; GroupDescription: "追加のショートカット:"; Flags: unchecked

[Files]
Source: "..\src\Presso\bin\Release\net8.0-windows\win-x64\publish\Presso.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\ffmpeg\bin\*"; DestDir: "{app}\ffmpeg\bin"; Excludes: "ffplay.exe"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\LICENSE"; DestDir: "{app}"; DestName: "LICENSE.txt"; Flags: ignoreversion
Source: "..\LICENSES\*"; DestDir: "{app}\licenses"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\README.md"; DestDir: "{app}"; DestName: "README.txt"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{#MyAppName} をアンインストール"; Filename: "{uninstallexe}"
Name: "{commondesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

; -- Right-click "Compress with Presso" entry for major video extensions --
[Registry]
; .mp4
Root: HKLM; Subkey: "Software\Classes\SystemFileAssociations\.mp4\shell\Presso";          ValueType: string; ValueName: "";     ValueData: "Compress with Presso"; Flags: uninsdeletekey
Root: HKLM; Subkey: "Software\Classes\SystemFileAssociations\.mp4\shell\Presso";          ValueType: string; ValueName: "Icon"; ValueData: "{app}\{#MyAppExeName},0"
Root: HKLM; Subkey: "Software\Classes\SystemFileAssociations\.mp4\shell\Presso\command";  ValueType: string; ValueName: "";     ValueData: """{app}\{#MyAppExeName}"" --shell ""%1"""

; .mov
Root: HKLM; Subkey: "Software\Classes\SystemFileAssociations\.mov\shell\Presso";          ValueType: string; ValueName: "";     ValueData: "Compress with Presso"; Flags: uninsdeletekey
Root: HKLM; Subkey: "Software\Classes\SystemFileAssociations\.mov\shell\Presso";          ValueType: string; ValueName: "Icon"; ValueData: "{app}\{#MyAppExeName},0"
Root: HKLM; Subkey: "Software\Classes\SystemFileAssociations\.mov\shell\Presso\command";  ValueType: string; ValueName: "";     ValueData: """{app}\{#MyAppExeName}"" --shell ""%1"""

; .mkv
Root: HKLM; Subkey: "Software\Classes\SystemFileAssociations\.mkv\shell\Presso";          ValueType: string; ValueName: "";     ValueData: "Compress with Presso"; Flags: uninsdeletekey
Root: HKLM; Subkey: "Software\Classes\SystemFileAssociations\.mkv\shell\Presso";          ValueType: string; ValueName: "Icon"; ValueData: "{app}\{#MyAppExeName},0"
Root: HKLM; Subkey: "Software\Classes\SystemFileAssociations\.mkv\shell\Presso\command";  ValueType: string; ValueName: "";     ValueData: """{app}\{#MyAppExeName}"" --shell ""%1"""

; .webm
Root: HKLM; Subkey: "Software\Classes\SystemFileAssociations\.webm\shell\Presso";         ValueType: string; ValueName: "";     ValueData: "Compress with Presso"; Flags: uninsdeletekey
Root: HKLM; Subkey: "Software\Classes\SystemFileAssociations\.webm\shell\Presso";         ValueType: string; ValueName: "Icon"; ValueData: "{app}\{#MyAppExeName},0"
Root: HKLM; Subkey: "Software\Classes\SystemFileAssociations\.webm\shell\Presso\command"; ValueType: string; ValueName: "";     ValueData: """{app}\{#MyAppExeName}"" --shell ""%1"""

; .avi
Root: HKLM; Subkey: "Software\Classes\SystemFileAssociations\.avi\shell\Presso";          ValueType: string; ValueName: "";     ValueData: "Compress with Presso"; Flags: uninsdeletekey
Root: HKLM; Subkey: "Software\Classes\SystemFileAssociations\.avi\shell\Presso";          ValueType: string; ValueName: "Icon"; ValueData: "{app}\{#MyAppExeName},0"
Root: HKLM; Subkey: "Software\Classes\SystemFileAssociations\.avi\shell\Presso\command";  ValueType: string; ValueName: "";     ValueData: """{app}\{#MyAppExeName}"" --shell ""%1"""

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{#MyAppName} を起動"; Flags: nowait postinstall skipifsilent
