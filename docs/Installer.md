# Building the Breeze installer

One script builds Breeze and its Windows installer:

```powershell
powershell -ExecutionPolicy Bypass -File tools\build-installer.ps1
```

Run it from the repository root. It produces `dist\Breeze-v<version>-Setup.exe`, currently about
42 MB.

## Prerequisites

- Windows 10 or later, x64.
- The .NET 10 SDK.
- [Inno Setup 6](https://jrsoftware.org/isinfo.php): `winget install JRSoftware.InnoSetup`. The
  script finds it on `PATH`, under `%LOCALAPPDATA%\Programs`, or in either Program Files.

Nothing else is needed. The script downloads Microsoft's WebView2 bootstrapper on the first run and
caches it in `installer\build`.

## What the script does

1. Reads `<Version>` from `Breeze\Breeze.csproj`, the single place the version is defined.
2. Publishes Breeze as self contained win-x64 into `installer\build\app`, so the installed browser
   needs no .NET runtime of its own. The application's folder layout is left as published.
3. Copies `LICENSE` to `installer\build\License.txt` for the wizard's license page.
4. Downloads the WebView2 Evergreen bootstrapper if it is not already cached.
5. Compiles `installer\Breeze.iss` with Inno Setup, passing the version in.

`installer\build` and `dist` are both ignored by git.

## Preparing a release

1. Change `<Version>` in `Breeze\Breeze.csproj`. Everything else follows: the About page, the
   executable metadata, the installer's version and its file name.
2. Run the build script above.
3. Tag the release and attach `dist\Breeze-v<version>-Setup.exe` to it.

The wizard artwork under `installer\assets` only needs regenerating when the logo itself changes,
with `tools\build-brand.ps1`.

## What the installer does

- Installs into `%ProgramFiles%\Breeze` by default, and asks for the directory. A user without
  administrator rights can choose a per user install instead.
- Asks about a desktop shortcut, and offers the standard Start Menu folder page, where the folder
  can be renamed or declined.
- Shows the MIT license as the agreement page.
- Registers Breeze in Installed Apps with its icon, version and publisher, and writes an
  uninstaller.
- Detects the Microsoft Edge WebView2 Runtime. When it is missing, the wizard offers to install it
  through Microsoft's Evergreen bootstrapper, which fetches the current version; no fixed runtime is
  bundled. A failure is explained and the install still finishes. Success is judged by whether the
  runtime is present afterwards, not by the bootstrapper's exit code, which reports codes of its own
  when it decides no work is needed.
- Offers links to the repository and the release notes on the finish page. Breeze is never started
  automatically.
- Compresses with `lzma2/fast` and no solid block, favouring install speed over size.

## User data

Breeze keeps everything user-owned in `%LOCALAPPDATA%\Breeze`: settings, bookmarks, homepage
shortcuts, the download configuration, partly downloaded files, and the WebView2 profile with its
history, cookies and cache. The installer never writes to or deletes any of it, so installing over
an existing copy or upgrading leaves it untouched.

The uninstaller asks whether to remove that data as well. No is the default. Choosing No leaves the
folder exactly as it was; choosing Yes deletes it.

## Not offered: default browser and file associations

Breeze ignores command-line arguments, so it always opens its startup page and cannot yet open a URL
or a file handed to it by Windows. Registering it as the default browser or as the `.html` handler
would produce a browser that opens the wrong page, so those options are deliberately absent rather
than half implemented. They belong with the work that teaches `Program.cs` to accept a URL.
