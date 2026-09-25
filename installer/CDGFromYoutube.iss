; Inno Setup script for CDGFromYoutube.
;
; Inno Setup was chosen because it is the simplest way to get a normal Windows setup.exe: one script,
; one free compiler (https://jrsoftware.org/isinfo.php), no separate bootstrapper project. Running the
; compiled installer adds an entry to "Installed apps" / "Programs and Features" automatically, and that
; entry's uninstaller removes everything the installer put down - no extra work is needed for that.
;
; This script does not build the program. Run build.ps1 first, which publishes CdgFromYoutube and then
; compiles this script; see installer\README.md.

#define MyAppName "CDGFromYoutube"
#define MyAppExeName "cdgfromyoutube.exe"
#ifndef MyAppVersion
  #define MyAppVersion "1.0.0"
#endif
#define MyPublishDir "publish"

[Setup]
; A fixed, random GUID that identifies this program across versions, so a later installer upgrades this
; one instead of installing side by side. It is unrelated to any other identifier; it was generated once
; for this script and must never change. The doubled "{{" is Inno's escape for a literal "{" - see
; https://jrsoftware.org/ishelp/index.php?topic=idpsetupappid.
AppId={{A2E1F6B4-8C3D-4F2A-9E7B-1D5C6A9F3B02}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
UninstallDisplayIcon={app}\{#MyAppExeName}
OutputDir=Output
OutputBaseFilename=CDGFromYoutubeSetup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
; Program Files and the machine PATH both need administrator rights to write to.
PrivilegesRequired=admin
; Tells Windows Explorer to notice the PATH change once setup finishes, so a newly opened Command Prompt
; sees it without a reboot.
ChangesEnvironment=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "addtopath"; Description: "Add {#MyAppName} to the PATH, so ""{#MyAppExeName}"" can be run from any Command Prompt"

[Files]
; publish\ is created by build.ps1 (dotnet publish); everything it contains is the program.
Source: "{#MyPublishDir}\*"; DestDir: "{app}"; Flags: recursesubdirs ignoreversion
; Not flagged isreadme: that flag offers to open the file when setup finishes, including in a silent
; install, which is not what a program meant to be run from a Command Prompt should do on its own.
Source: "..\README.md"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
; The program takes a YouTube URL as a command line argument, so a plain shortcut to it would only flash
; a usage message and close. This one opens a prompt in the install folder instead, ready to type into.
Name: "{group}\{#MyAppName} Command Prompt"; Filename: "{cmd}"; \
    Parameters: "/K ""cd /d ""{app}"" && {#MyAppExeName} --help"""; WorkingDir: "{app}"; \
    IconFilename: "{app}\{#MyAppExeName}"
Name: "{group}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"

[Run]
; Fetches the .NET 10 runtime if it is missing, using Microsoft's own install script, which already
; checks the version installed and does nothing if it is new enough. A failure here (no internet, a
; blocked download) is only logged; the program still installs; Windows itself offers the runtime
; download the first time it is run without one, the same as any other .NET program.
Filename: "{sys}\WindowsPowerShell\v1.0\powershell.exe"; \
    Parameters: "-NoProfile -ExecutionPolicy Bypass -File ""{tmp}\install-dotnet-runtime.ps1"""; \
    StatusMsg: "Checking for the .NET 10 runtime..."; \
    Flags: runhidden waituntilterminated

[Code]
{ Environment variable handling is Windows-specific plumbing with no built-in Inno command, so it is
  spelled out here rather than pulled in from a third-party script. }

const
  EnvironmentKey = 'SYSTEM\CurrentControlSet\Control\Session Manager\Environment';

function GetInstallDir(): string;
begin
  Result := ExpandConstant('{app}');
end;

{ Appends the install directory to the machine PATH, unless it is already there. }
procedure AddToPath();
var
  Path: string;
  InstallDir: string;
begin
  InstallDir := GetInstallDir();
  if not RegQueryStringValue(HKEY_LOCAL_MACHINE, EnvironmentKey, 'Path', Path) then
    Path := '';

  if (Pos(';' + Uppercase(InstallDir) + ';', ';' + Uppercase(Path) + ';') > 0) then
    Exit;

  if (Length(Path) > 0) and (Path[Length(Path)] <> ';') then
    Path := Path + ';';
  Path := Path + InstallDir;

  RegWriteExpandStringValue(HKEY_LOCAL_MACHINE, EnvironmentKey, 'Path', Path);
end;

{ Removes the install directory from the machine PATH, leaving everything else in it untouched. }
procedure RemoveFromPath();
var
  Path: string;
  InstallDir: string;
  MarkedPath: string;
  Position: Integer;
begin
  InstallDir := GetInstallDir();
  if not RegQueryStringValue(HKEY_LOCAL_MACHINE, EnvironmentKey, 'Path', Path) then
    Exit;

  MarkedPath := ';' + Path + ';';
  Position := Pos(';' + Uppercase(InstallDir) + ';', Uppercase(MarkedPath));
  if Position = 0 then
    Exit;

  Delete(MarkedPath, Position, Length(InstallDir) + 1);
  { Drop the leading and trailing markers that were added above. They are all that is left when the
    install directory was the only entry in the PATH. }
  if Length(MarkedPath) <= 2 then
    Path := ''
  else
    Path := Copy(MarkedPath, 2, Length(MarkedPath) - 2);

  RegWriteExpandStringValue(HKEY_LOCAL_MACHINE, EnvironmentKey, 'Path', Path);
end;

{ Writes the PowerShell script that [Run] executes, rather than an inline one-liner, so quoting the
  install path and reporting errors does not have to fight Inno's own command line escaping. }
procedure CreateDotNetInstallScript();
var
  Lines: TArrayOfString;
  ScriptPath: string;
begin
  SetArrayLength(Lines, 12);
  Lines[0] := '$ErrorActionPreference = ''Stop''';
  Lines[1] := 'try {';
  Lines[2] := '    if (Get-Command dotnet -ErrorAction SilentlyContinue) {';
  Lines[3] := '        $has10 = & dotnet --list-runtimes 2>$null | Select-String ''Microsoft\.NETCore\.App 10\.''';
  Lines[4] := '        if ($has10) { exit 0 }';
  Lines[5] := '    }';
  Lines[6] := '    $script = Join-Path $env:TEMP ''dotnet-install.ps1''';
  Lines[7] := '    Invoke-WebRequest -Uri ''https://dot.net/v1/dotnet-install.ps1'' -OutFile $script -UseBasicParsing';
  Lines[8] := '    & $script -Channel 10.0 -Runtime dotnet -InstallDir "$env:ProgramFiles\dotnet"';
  Lines[9] := '} catch {';
  Lines[10] := '    Write-Warning "Could not install the .NET 10 runtime automatically: $_"';
  Lines[11] := '}';
  ScriptPath := ExpandConstant('{tmp}\install-dotnet-runtime.ps1');
  SaveStringsToFile(ScriptPath, Lines, False);
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssInstall then
    CreateDotNetInstallScript();

  if (CurStep = ssPostInstall) and WizardIsTaskSelected('addtopath') then
    AddToPath();
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usUninstall then
    RemoveFromPath();
end;
