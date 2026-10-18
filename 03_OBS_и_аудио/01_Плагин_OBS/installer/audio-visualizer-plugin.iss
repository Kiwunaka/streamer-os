#define MyAppName "Audio Visualizer for OBS"
#define MyAppVersion "2.0.0"
#define MyAppPublisher "Audio Visualizer"
#define MyAppId "{{B6D91C9A-7C3B-4E8D-B0E4-0D0F6A9E8A9A}}"

[Setup]
AppId={#MyAppId}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={code:ResolveObsPath}
DisableDirPage=no
DisableProgramGroupPage=yes
OutputDir={#SourcePath}\dist
OutputBaseFilename=Audio-Visualizer-OBS-Setup
Compression=lzma
SolidCompression=yes
ArchitecturesInstallIn64BitMode=x64
PrivilegesRequired=admin
WizardStyle=modern

[Files]
Source: "..\audio-visualizer-plugin\*"; DestDir: "{app}\data\obs-plugins\audio-visualizer-plugin"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: ".git\*"

[Code]
function ResolveObsPath(Param: string): string;
begin
  if RegQueryStringValue(HKLM, 'SOFTWARE\OBS Studio', 'InstallPath', Result) then
  begin
    Result := AddBackslash(Result);
    exit;
  end;
  if RegQueryStringValue(HKCU, 'SOFTWARE\OBS Studio', 'InstallPath', Result) then
  begin
    Result := AddBackslash(Result);
    exit;
  end;
  Result := ExpandConstant('{pf}\obs-studio');
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  Obs64: string;
  Obs32: string;
begin
  if CurStep = ssInstall then
  begin
    Obs64 := ExpandConstant('{app}\bin\64bit\obs64.exe');
    Obs32 := ExpandConstant('{app}\bin\32bit\obs32.exe');
    if (not FileExists(Obs64)) and (not FileExists(Obs32)) then
    begin
      MsgBox('OBS executable not detected in the selected folder. If you use a portable build, select its root folder.', mbInformation, MB_OK);
    end;
  end;
end;
