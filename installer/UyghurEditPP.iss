; Inno Setup 6 script for UyghurEdit++ (per-user, no admin rights).
; Build with scripts\build-installer.ps1 (builds Release first, then runs ISCC).

#define AppName "UyghurEdit++"
#define BuildDir "..\bin\UyghurEditPP\"
#define AppExe "UyghurEditPP.exe"
; Version follows AssemblyInformationalVersion of the built exe.
#define AppVersion GetStringFileInfo(AddBackslash(SourcePath) + BuildDir + AppExe, "ProductVersion")
#define ProgId "UyghurEditPP.Document"

[Setup]
; AppId identifies the application for upgrades and uninstall. NEVER CHANGE IT.
AppId={{12763278-CE78-4FFC-AA3F-CF9F63B01417}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher=UyghurEdit++
PrivilegesRequired=lowest
DefaultDirName={autopf}\UyghurEditPP
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
SetupIconFile=..\uyghur.ico
UninstallDisplayIcon={app}\{#AppExe}
LicenseFile=..\LICENSE
OutputDir=Output
OutputBaseFilename=UyghurEditPP-{#AppVersion}-setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ChangesAssociations=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "japanese"; MessagesFile: "compiler:Languages\Japanese.isl"

[CustomMessages]
english.AssocGroup=File associations:
japanese.AssocGroup=ファイルの関連付け:
english.AssocTask=Associate .uut and .txt files with {#AppName}
english.DotNetMissing=.NET Framework 4.8 or later is required but was not found. Please install it and run setup again.
japanese.AssocTask=.uut と .txt ファイルを {#AppName} に関連付ける
japanese.DotNetMissing=.NET Framework 4.8 以降が必要ですが見つかりませんでした。インストールしてからもう一度実行してください。

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "assoc"; Description: "{cm:AssocTask}"; GroupDescription: "{cm:AssocGroup}"; Flags: unchecked

[Files]
Source: "{#BuildDir}*"; DestDir: "{app}"; Excludes: "*.pdb"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\LICENSE"; DestDir: "{app}"; DestName: "LICENSE.txt"; Flags: ignoreversion
Source: "..\README.md"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Registry]
; Per-user file associations (HKCU). Only our own ProgID and values are removed on uninstall.
Root: HKCU; Subkey: "Software\Classes\{#ProgId}"; ValueType: string; ValueData: "{#AppName} document"; Flags: uninsdeletekey; Tasks: assoc
Root: HKCU; Subkey: "Software\Classes\{#ProgId}\DefaultIcon"; ValueType: string; ValueData: "{app}\{#AppExe},0"; Tasks: assoc
Root: HKCU; Subkey: "Software\Classes\{#ProgId}\shell\open\command"; ValueType: string; ValueData: """{app}\{#AppExe}"" ""%1"""; Tasks: assoc
; .uut becomes ours only when no other program owns it (or it is already ours, on an upgrade), so
; uninstalling never removes another program's association.
Root: HKCU; Subkey: "Software\Classes\.uut"; ValueType: string; ValueData: "{#ProgId}"; Flags: uninsdeletevalue uninsdeletekeyifempty; Tasks: assoc; Check: UutIsFreeOrOurs
Root: HKCU; Subkey: "Software\Classes\.uut\OpenWithProgids"; ValueType: string; ValueName: "{#ProgId}"; ValueData: ""; Flags: uninsdeletevalue uninsdeletekeyifempty; Tasks: assoc
Root: HKCU; Subkey: "Software\Classes\.txt\OpenWithProgids"; ValueType: string; ValueName: "{#ProgId}"; ValueData: ""; Flags: uninsdeletevalue uninsdeletekeyifempty; Tasks: assoc

[Run]
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchProgram,{#StringChange(AppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

; User settings in %AppData%\UyghurEditPP are intentionally not listed in [UninstallDelete].

[Code]
function UutIsFreeOrOurs(): Boolean;
var
  Current: String;
begin
  // HKCR merges HKCU and HKLM, so an association made for all users is respected too.
  Result := (not RegQueryStringValue(HKCR, '.uut', '', Current)) or (Current = '') or (Current = '{#ProgId}');
end;

function InitializeSetup(): Boolean;
var
  Release: Cardinal;
begin
  Result := True;
  // .NET Framework 4.8 corresponds to Release >= 528040.
  if not RegQueryDWordValue(HKLM64, 'SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full', 'Release', Release)
     or (Release < 528040) then
  begin
    MsgBox(CustomMessage('DotNetMissing'), mbCriticalError, MB_OK);
    Result := False;
  end;
end;
