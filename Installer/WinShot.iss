; File: Installer/WinShot.iss
;
; Inno Setup script for WinShot.
;
; Produces WinShotSetup.exe — a classic Windows installer that:
;   - installs WinShot.exe to %ProgramFiles%\WinShot
;   - creates Start Menu shortcuts (Launch + Uninstall)
;   - optionally creates a Desktop shortcut
;   - optionally registers WinShot under HKCU\...\Run so it starts with
;     Windows (this is the typical workflow — WinShot is a tray utility
;     that's useless unless it's running and listening for Ctrl+Shift+S)
;   - provides a proper "Programs and Features" uninstaller
;
; Build with:
;     iscc Installer\WinShot.iss
;
; Or run the PowerShell driver at repo root:
;     .\build-installer.ps1
;
; Requirements on the build machine:
;   - .NET 8 SDK           (dotnet publish)
;   - Inno Setup 6+        (https://jrsoftware.org/isdl.php)
;     Default install path: C:\Program Files (x86)\Inno Setup 6\iscc.exe

#define MyAppName        "WinShot"
#define MyAppVersion     "0.2.0"
#define MyAppPublisher   "WinShot"
#define MyAppExeName     "WinShot.exe"

; AppId must stay stable across releases so Windows recognizes upgrades
; instead of creating side-by-side installations. This GUID is intentionally
; DIFFERENT from the single-instance mutex GUID in App.xaml.cs — they serve
; unrelated purposes and conflating them would be misleading.
#define MyAppId          "{{8B7A2F3C-91E4-4D18-8B02-7E4F9D3A1C22}"

[Setup]
AppId={#MyAppId}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppSupportURL=https://example.invalid/winshot
AppUpdatesURL=https://example.invalid/winshot
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputDir=..\dist
OutputBaseFilename=WinShotSetup
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=admin
UninstallDisplayIcon={app}\{#MyAppExeName}
SetupIconFile=..\WinShot\Assets\WinShot.ico
; Restart the Explorer shell is not necessary; WinShot doesn't register any
; shell extensions. If that ever changes, add ChangesEnvironment=yes plus an
; appropriate [Run]/[UninstallRun] entry.
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon";    Description: "Create a &desktop shortcut"; \
    GroupDescription: "Additional shortcuts:"; Flags: unchecked
Name: "startupentry";   Description: "Start {#MyAppName} automatically when Windows starts"; \
    GroupDescription: "Startup options:"

[Files]
; Layout produced by the SingleFile publish profile. All bundled runtime
; assets live alongside WinShot.exe; a Source:*  wildcard copies everything.
Source: "..\WinShot\bin\Release\net8.0-windows\publish\win-x64\*"; \
    DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}";             Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Uninstall {#MyAppName}";   Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}";       Filename: "{app}\{#MyAppExeName}"; \
    Tasks: desktopicon

[Registry]
; Autostart via HKCU Run — per-user. Do NOT use HKLM here: the user might
; install as admin but actually use a different account, and HKLM\Run would
; launch the tray icon in the wrong session.
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; \
    ValueType: string; ValueName: "{#MyAppName}"; \
    ValueData: """{app}\{#MyAppExeName}"""; \
    Flags: uninsdeletevalue; Tasks: startupentry

[Run]
; Launch WinShot at the end of the installer so the tray icon appears
; immediately without the user having to reopen Start or reboot.
Filename: "{app}\{#MyAppExeName}"; \
    Description: "Launch {#MyAppName} now"; \
    Flags: nowait postinstall skipifsilent

[UninstallRun]
; Best-effort shutdown of any running instance before removing the files.
; /T = tree, /F = force. The || exit 0 isn't necessary in Inno's syntax; we
; accept any exit code by just not failing the uninstall on it.
Filename: "{sys}\taskkill.exe"; Parameters: "/IM {#MyAppExeName} /F /T"; \
    Flags: runhidden; RunOnceId: "KillWinShot"

[Code]
// Prevents installing on top of a running instance, which would fail
// silently for the locked executable and leave the install in a half-state.
function InitializeSetup(): Boolean;
var
  ResultCode: Integer;
begin
  // Ask any existing WinShot to exit cleanly; ignore failure — the file
  // replacement step has its own locking behavior (CloseApplications=yes).
  Exec('taskkill.exe', '/IM {#MyAppExeName} /F /T',
       '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Result := True;
end;
