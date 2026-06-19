; Inno Setup script for CoffeeManiaVPN
; Requires: https://jrsoftware.org/isdl.php

#define AppName "Кофемания ВПН"
#define AppPublisher "КОФЕМАНИЯ ВПН"
#define AppExe "CoffeeManiaVPN.exe"
#define AppVersion "1.0.0"
#define PublishDir "..\dist\app"

[Setup]
AppId={{A4E8B2C1-9F3D-4A6E-B5C7-1D2E3F4A5B6C}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppPublisher} {#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL=https://coffeemaniavpn.ru
AppCopyright=Copyright (C) {#AppPublisher}
DefaultDirName={autopf}\CoffeeManiaVPN
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
OutputDir=..\dist
OutputBaseFilename=CoffeeManiaVPN-Setup-{#AppVersion}
SetupIconFile=..\src\CoffeeManiaVPN\Assets\app.ico
UninstallDisplayIcon={app}\{#AppExe}
UninstallDisplayName={#AppPublisher}
VersionInfoVersion={#AppVersion}
VersionInfoCompany={#AppPublisher}
VersionInfoDescription=Установщик {#AppPublisher}
VersionInfoProductName={#AppPublisher}
VersionInfoProductVersion={#AppVersion}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0

[Languages]
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"

[Tasks]
Name: "desktopicon"; Description: "Создать ярлык на рабочем столе"; GroupDescription: "Дополнительно:"

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExe}"; Description: "Запустить {#AppName}"; Flags: nowait postinstall skipifsilent

[Registry]
Root: HKCR; Subkey: "cfm"; ValueType: string; ValueName: ""; ValueData: "URL:Кофемания ВПН"; Flags: uninsdeletekey
Root: HKCR; Subkey: "cfm"; ValueType: string; ValueName: "URL Protocol"; ValueData: ""; Flags: uninsdeletekey
Root: HKCR; Subkey: "cfm\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\{#AppExe},0"; Flags: uninsdeletekey
Root: HKCR; Subkey: "cfm\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#AppExe}"" ""%1"""; Flags: uninsdeletekey
