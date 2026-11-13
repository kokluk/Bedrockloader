#ifndef PayloadDir
  #define PayloadDir "..\installer-payload-v0.17.9"
#endif
#ifndef ProjectDir
  #define ProjectDir "..\BedrockLoader"
#endif

[Setup]
AppId={{7B8B8A23-6749-4C7D-9D23-420A29B0F11B}
AppName=BedrockLoader
AppVersion=0.17.9
AppVerName=BedrockLoader v0.17.9
AppPublisher=Kokluk
AppPublisherURL=https://github.com/kokluk/Bedrockloader
AppSupportURL=https://github.com/kokluk/Bedrockloader/issues
DefaultDirName={localappdata}\Programs\BedrockLoader
DefaultGroupName=BedrockLoader
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
LicenseFile={#ProjectDir}\LICENSE
OutputDir=..\installer-output
OutputBaseFilename=BedrockLoader-Setup-v0.17.9-x64
VersionInfoVersion=0.17.9.0
SetupIconFile={#ProjectDir}\Assets\BedrockLoader.ico
UninstallDisplayIcon={app}\BedrockLoader.exe
WizardStyle=modern dynamic windows11 hidebevels includetitlebar
WizardBackColor=#f2f4fa
WizardBackColorDynamicDark=#202020
Compression=lzma2/ultra64
SolidCompression=yes
CloseApplications=yes
RestartApplications=no
SetupLogging=yes

[Languages]
Name: "turkish"; MessagesFile: "compiler:Languages\Turkish.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#PayloadDir}\*"; DestDir: "{app}"; Excludes: "*.pdb"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#ProjectDir}\LICENSE"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\BedrockLoader"; Filename: "{app}\BedrockLoader.exe"; WorkingDir: "{app}"
Name: "{autodesktop}\BedrockLoader"; Filename: "{app}\BedrockLoader.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\BedrockLoader.exe"; Description: "{cm:LaunchProgram,BedrockLoader}"; Flags: nowait postinstall skipifsilent

; AppData settings, Minecraft mods and the separately installed modloader are
; user data. Uninstall removes only files installed by this setup.

[Code]
var
  PreviousVersion: String;

procedure CurStepChanged(CurStep: TSetupStep);
var
  DataFolder: String;
begin
  if CurStep = ssInstall then
    GetVersionNumbersString(ExpandConstant('{app}\BedrockLoader.exe'), PreviousVersion);
  if (CurStep = ssPostInstall) and (PreviousVersion <> '') and
     (PreviousVersion <> '{#SetupSetting("VersionInfoVersion")}') then
  begin
    DataFolder := ExpandConstant('{userappdata}\BedrockLoader');
    if ForceDirectories(DataFolder) then
      if not SaveStringToFile(DataFolder + '\PreviousInstalledVersion.txt', PreviousVersion, False) then
        Log('Could not save the previous BedrockLoader version.');
  end;
end;