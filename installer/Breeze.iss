; Breeze installer. Built through tools\build-installer.ps1, which publishes the application and
; passes the version in, so preparing a release means changing <Version> in Breeze.csproj only.
;
; Do not compile this file on its own: it expects the published application and the license and
; WebView2 bootstrapper copies that the build script places in installer\build.

#ifndef AppVersion
  #error Compile through tools\build-installer.ps1, which supplies AppVersion.
#endif

#define AppName "Breeze"
#define AppPublisher "Breeze"
#define AppExeName "Breeze.exe"
#define RepositoryUrl "https://github.com/lexicccone/Breeze"
#define ReleasesUrl "https://github.com/lexicccone/Breeze/releases"

[Setup]
; Never change AppId: it is how Windows recognises an existing install as the same product.
AppId={{B7E0F1C4-9A3D-4C8E-9E2C-4F3A1D6B5E90}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} v{#AppVersion}
VersionInfoVersion={#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#RepositoryUrl}
AppSupportURL={#RepositoryUrl}/issues
AppUpdatesURL={#ReleasesUrl}

DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
UninstallDisplayName={#AppName} v{#AppVersion}
UninstallDisplayIcon={app}\{#AppExeName}

; Program Files needs elevation, but the user may choose to install into their own profile instead.
PrivilegesRequired=admin
PrivilegesRequiredOverridesAllowed=commandline dialog

ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0

; Speed over size, as asked: lzma2/fast with no solid block keeps compression quick.
Compression=lzma2/fast
SolidCompression=no
LZMANumBlockThreads=4

OutputDir=..\dist
OutputBaseFilename={#AppName}-v{#AppVersion}-Setup
SetupIconFile=..\Breeze\Assets\Brand\breeze.ico
LicenseFile=build\License.txt

WizardStyle=modern
WizardImageFile=assets\wizard-large.bmp,assets\wizard-large@125.bmp,assets\wizard-large@150.bmp,assets\wizard-large@200.bmp
WizardSmallImageFile=assets\wizard-small.bmp,assets\wizard-small@125.bmp,assets\wizard-small@150.bmp,assets\wizard-small@200.bmp
DisableWelcomePage=no
DisableDirPage=no
DisableProgramGroupPage=no
AllowNoIcons=yes
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Shortcuts:"
; Breeze cannot render a page without this, so declining it cancels the installation rather than
; leaving a browser that cannot browse.
Name: "webview2"; Description: "Install the Microsoft Edge WebView2 Runtime (required by Breeze)"; GroupDescription: "Required component:"; Check: not WebView2Installed

[Files]
; The published application, runtime included, so no separate .NET install is needed.
Source: "build\app\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

; Microsoft's Evergreen bootstrapper. It is a downloader, not a fixed runtime: it fetches the
; current version at install time and updates itself thereafter. Extracted by code before anything
; is installed, which is why it is not copied as part of the file list.
Source: "build\MicrosoftEdgeWebview2Setup.exe"; Flags: dontcopy

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Comment: "Browse the web with Breeze"
Name: "{group}\Uninstall {#AppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Comment: "Browse the web with Breeze"; Tasks: desktopicon

[Run]
; Breeze is deliberately not started here. These are the finish page's optional links.
Filename: "{#RepositoryUrl}"; Description: "Open the Breeze repository"; Flags: postinstall shellexec nowait skipifsilent unchecked
Filename: "{#ReleasesUrl}"; Description: "View the release notes"; Flags: postinstall shellexec nowait skipifsilent unchecked

[Code]
// Breeze keeps its data in the user's local application data, outside the install directory, so an
// install or an upgrade never writes or deletes any of it. Only the uninstaller offers to remove
// it, and only when the user says yes.

function WebView2Installed: Boolean;
var
  Version: String;
begin
  Result :=
    (RegQueryStringValue(HKLM, 'SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}', 'pv', Version) or
     RegQueryStringValue(HKLM, 'SOFTWARE\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}', 'pv', Version) or
     RegQueryStringValue(HKCU, 'SOFTWARE\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}', 'pv', Version)) and
    (Version <> '') and (Version <> '0.0.0.0');
end;

// Provides the runtime before a single file of Breeze is installed. Returning a message from here
// stops Setup with that message and installs nothing, which is how a run can never finish while the
// runtime is absent. Doing this afterwards, as a post-install step, could only warn.
//
// The bootstrapper runs with its own window rather than /silent: it downloads the runtime, which on
// a clean machine takes a while, and Setup cannot repaint while it waits for a child process. With
// no visible progress that reads as a hung installer.
function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  Code: Integer;
  Started: Boolean;
begin
  Result := '';

  if WebView2Installed then
    Exit;

  if not WizardIsTaskSelected('webview2') then
  begin
    Result :=
      'Breeze needs the Microsoft Edge WebView2 Runtime to display web pages, and it is not ' +
      'installed on this computer.' + #13#10#13#10 +
      'Setup has stopped rather than install a browser that cannot open a page. Run Setup again ' +
      'and leave the runtime option selected, or install it yourself from:' + #13#10 +
      'https://developer.microsoft.com/microsoft-edge/webview2/';
    Exit;
  end;

  WizardForm.StatusLabel.Caption := 'Installing the Microsoft Edge WebView2 Runtime. This may take a few minutes...';
  WizardForm.Refresh;

  ExtractTemporaryFile('MicrosoftEdgeWebview2Setup.exe');

  Started := Exec(ExpandConstant('{tmp}\MicrosoftEdgeWebview2Setup.exe'), '/install', '',
                  SW_SHOWNORMAL, ewWaitUntilTerminated, Code);

  // Judged by whether the runtime is present afterwards, not by the exit code: the bootstrapper
  // reports codes of its own, including when it decides no work is needed.
  if WebView2Installed then
    Exit;

  if not Started then
    Result :=
      'The Microsoft Edge WebView2 Runtime installer could not be started, so Breeze has not been ' +
      'installed.' + #13#10#13#10 +
      'Install the runtime yourself and run Setup again:' + #13#10 +
      'https://developer.microsoft.com/microsoft-edge/webview2/'
  else
    Result :=
      'The Microsoft Edge WebView2 Runtime could not be installed (code ' + IntToStr(Code) + '), so ' +
      'Breeze has not been installed.' + #13#10#13#10 +
      'The runtime is downloaded during setup, so check the computer''s internet connection and try ' +
      'again. The offline installer is available from:' + #13#10 +
      'https://developer.microsoft.com/microsoft-edge/webview2/';
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  Data: String;
begin
  if CurUninstallStep <> usUninstall then
    Exit;

  Data := ExpandConstant('{localappdata}\Breeze');

  if not DirExists(Data) then
    Exit;

  if MsgBox('Also remove your Breeze browsing data?' + #13#10#13#10 +
            'This deletes your bookmarks, homepage shortcuts, settings, cookies, history and cache from:' + #13#10 +
            Data + #13#10#13#10 +
            'Choose No to keep everything for a future install.', mbConfirmation, MB_YESNO or MB_DEFBUTTON2) = IDYES then
    DelTree(Data, True, True, True);
end;
