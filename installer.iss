; Script Inno Setup cho Windows Monitor BLE
; Su dung Inno Setup de bien dich thanh file Setup.exe duy nhat

#define MyAppName "Windows Monitor BLE"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "Antigravity Dev"
#define MyAppExeName "WindowsMonitorBLE.exe"

[Setup]
AppId={{D37E5F92-A741-4E68-9A92-6D3E71661A01}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputDir=.\dist_setup
OutputBaseFilename=WindowsMonitorBLE_Setup_v{#MyAppVersion}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
ArchitecturesInstallIn64BitMode=x64

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"
Name: "autostart"; Description: "Tu dong chay cung Windows khi mo may"; GroupDescription: "Tuy chon khoi dong:"

[Files]
Source: ".\dist\app\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion
Source: ".\dist\app\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "WindowsMonitorBLE"; ValueData: """{app}\{#MyAppExeName}"""; Flags: uninsdeletevalue; Tasks: autostart

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent
