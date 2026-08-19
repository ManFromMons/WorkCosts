; Will I DIY? — Inno Setup 6.3+ installer (unpackaged WinUI 3).
; Compile via Pack-Inno.ps1, or from the Inno IDE after a publish folder exists.
;
; Command-line defines (set by Pack-Inno.ps1):
;   AppVersion  e.g. 1.0.0
;   AppArch     x64 | x86 | arm64
;   PublishDir  folder produced by `dotnet publish` (forward slashes preferred)

#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif
#ifndef AppArch
  #define AppArch "x64"
#endif
#ifndef PublishDir
  #define PublishDir "publish\win-x64"
#endif

#define AppName "Will I DIY?"
#define AppPublisher "Will I DIY?"
#define AppExeName "WillIDIY.exe"
#define AppURL "https://github.com/ManFromMons/WorkCosts"

[Setup]
AppId={{B3E7C1A2-8F54-4D9E-A1C6-9E2B47F05D18}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppURL}
AppSupportURL={#AppURL}
AppUpdatesURL={#AppURL}/releases
DefaultDirName={autopf}\WillIDIY
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
; Per-user by default (no UAC). The dialog offers an all-users Program Files install.
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
OutputDir=Output
OutputBaseFilename=WillIDIY-Setup-{#AppVersion}-{#AppArch}
SetupIconFile=..\WorkCosts\Assets\icon.jog-1.ico
UninstallDisplayIcon={app}\{#AppExeName}
UninstallDisplayName={#AppName}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
WizardSizePercent=120
MinVersion=10.0.17763
CloseApplications=yes
RestartApplications=no
CloseApplicationsFilter={#AppExeName}
ChangesAssociations=no
AllowNoIcons=yes
#if AppArch == "arm64"
ArchitecturesAllowed=arm64
ArchitecturesInstallIn64BitMode=arm64
#elif AppArch == "x86"
ArchitecturesAllowed=x86compatible
#else
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
#endif

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; Self-contained publish output. PDBs are build artifacts; leave user data in %LOCALAPPDATA%\WorkCosts.
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "*.pdb,createdump.exe"

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExeName}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(AppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; Only leftover files under the install folder. Jobs/parts stay in %LOCALAPPDATA%\WorkCosts.

[Code]
function WebView2RuntimeInstalled: Boolean;
begin
  Result :=
    RegKeyExists(HKLM, 'SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}') or
    RegKeyExists(HKLM, 'SOFTWARE\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}') or
    RegKeyExists(HKCU, 'SOFTWARE\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}');
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if (CurStep = ssPostInstall) and (not WizardSilent) and (not WebView2RuntimeInstalled) then
    MsgBox('The WebView2 Runtime was not detected. Importing product pages may not work until it is installed:'#13#13
      'https://aka.ms/webview2-bootstrapper', mbInformation, MB_OK);
end;
