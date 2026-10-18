#define MyAppName "Audio Visualizer for OBS (Native)"
#define MyAppVersion "2.0.0"
#define MyAppPublisher "Audio Visualizer"
#define MyAppId "{{9D6C5A13-1A17-4F03-9D7F-6D0A89DCE6F9}}"

#ifndef PluginBinDir
#define PluginBinDir "..\\obs-plugin\\build-local\\bin\\64bit"
#endif

[Setup]
AppId={#MyAppId}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={code:ResolveObsPath}
DisableDirPage=no
DisableProgramGroupPage=yes
OutputDir={#SourcePath}\dist
OutputBaseFilename=Audio-Visualizer-OBS-Native-Setup
Compression=lzma
SolidCompression=yes
ArchitecturesInstallIn64BitMode=x64
PrivilegesRequired=admin
WizardStyle=modern

[Files]
Source: "{#PluginBinDir}\audio-visualizer-plugin.dll"; DestDir: "{app}\obs-plugins\64bit"; Flags: ignoreversion
Source: "..\audio-visualizer-plugin\*.html"; DestDir: "{app}\data\obs-plugins\audio-visualizer-plugin"; Flags: ignoreversion
Source: "..\audio-visualizer-plugin\*.md"; DestDir: "{app}\data\obs-plugins\audio-visualizer-plugin"; Flags: ignoreversion
Source: "..\audio-visualizer-plugin\*.js"; DestDir: "{app}\data\obs-plugins\audio-visualizer-plugin"; Flags: ignoreversion
Source: "..\audio-visualizer-plugin\icons\*"; DestDir: "{app}\data\obs-plugins\audio-visualizer-plugin\icons"; Flags: ignoreversion recursesubdirs createallsubdirs

Source: "..\audio-visualizer-plugin\*.css"; DestDir: "{app}\data\obs-plugins\audio-visualizer-plugin"; Flags: ignoreversion
Source: "..\..\..\02_Стримербот_и_CSharp\01_Персоны_и_донаты\Assets\intro_*"; DestDir: "{app}\data\obs-plugins\audio-visualizer-plugin\assets"; Flags: ignoreversion

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
begin
  if CurStep = ssInstall then
  begin
    Obs64 := ExpandConstant('{app}\bin\64bit\obs64.exe');
    if not FileExists(Obs64) then
    begin
      MsgBox('OBS executable not detected in the selected folder. If you use a portable build, select its root folder.', mbInformation, MB_OK);
    end;
  end;
end;
