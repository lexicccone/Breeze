# Breeze

A lightweight, privacy-focused web browser built with Avalonia and WebView2.

> Breeze currently targets Windows. Linux and macOS are not supported.

<p align="center">
  <img src="docs/images/Homepage.png" alt="Breeze homepage" width="48%">
  <img src="docs/images/Settings.png" alt="Breeze settings" width="48%">
</p>

## Project Status

Breeze is under active development. Expect breaking changes, UI refinements and new features
until the first stable release.

## What works

- Tabs: open, close, switch, and drag to reorder. Closing the last tab closes the window.
- Address bar: URLs navigate, anything else searches with the selected engine.
- Back, forward, reload and stop.
- Favicons on tabs, taken from the page the engine has already loaded.
- Bookmarks: a star in the toolbar adds and removes the current page, and a bookmark bar below
  the toolbar opens them in the current tab, or in a new one with a middle click. The bar appears
  with your first bookmark and hides again when nothing is left on it, and `Ctrl+Shift+B` toggles it.
- Bookmark folders, nested as deeply as you like. Clicking a folder opens a menu beneath it, and
  folders inside it open as submenus. Right-click anything for open, rename, delete, move to
  folder, open all, and new bookmark or folder; right-click the bar itself to add to its top level.
  Drag entries to reorder them, onto a folder to file them inside it, or out of an open folder onto
  the bar to take them back out.
- A bundled homepage served from disk with the Breeze logo, a search box and manually managed
  shortcuts (create, edit, delete, drag to reorder), stored as readable JSON.
- Favicons for shortcuts and bookmarks, discovered from the site and cached on disk.
- Downloads: files are saved to the configured folder under a sanitised name, or wherever you
  choose when "ask where to save each file" is on. A Breeze popup in the top right shows progress,
  speed and status, and offers cancel while running, open and show in folder once finished.
- A settings page hosted in a tab as native UI, covering startup page, theme, the homepage logo,
  bookmarks, search engine, the download folder, keyboard shortcuts, and clearing history,
  cookies and cache.
- Light and dark themes, applied to the Breeze UI, the homepage, and passed to sites through
  the engine's preferred colour scheme.

## Not implemented yet

These are absent, not merely unfinished, and are worth knowing before you rely on Breeze as a
daily browser:

- **A full downloads manager.** The popup covers this session only: there is no download history,
  no pause and resume, no retry, and no handling of dangerous file types.
- **Permission prompts.** Requests for camera, microphone, location, notifications and
  clipboard read are refused outright, with no way to allow them.
- **History.** Browsing history is kept by the engine and can be cleared from settings, but there
  is no history UI.
- **A bookmark manager.** Folders, renaming and moving all live on the bar and in its menus; there
  is no separate manager window, no search, and no import or export.
- **Keyboard shortcut editing.** Settings lists the shortcuts and the file they are stored in
  accepts overrides, but there is no UI to change them.
- **Find in page, PDF controls, zoom controls, incognito mode, extensions, profiles, sync.**
- **UI scale and compact mode** appear in settings but are disabled.

## Privacy

Breeze is built to keep browsing data on the machine. What that means concretely:

- Breeze itself contains no analytics, no crash reporting and no usage statistics. It sends
  nothing to any Breeze-operated service, because none exists.
- All data lives in `%LOCALAPPDATA%\Breeze`: the WebView2 profile, cached favicons, your
  shortcuts, your bookmarks, your settings, files still being downloaded, and a local error log.
  Nothing is uploaded and there is no account.
- Tracking prevention is set to strict, browser extensions are disabled, and password saving
  and autofill are off.
- Edge reputation checking (SmartScreen), which sends visited URLs to a Microsoft service, is
  explicitly disabled.
- Search queries go to the engine you select. Favicons are fetched from the site itself, never
  through a third-party icon service, and never from local or private network addresses.

Things to Know:

- **"Zero telemetry" is not a claim we can currently make.** Breeze adds none, and the known
  URL-reporting channel is switched off, but Breeze embeds the Microsoft Edge WebView2 runtime
  and we have not yet verified with a network capture that the runtime makes no optional
  connections of its own. Until that verification is published, treat this as "no telemetry
  added by Breeze" rather than "no telemetry at all".
- **Local data is not encrypted.** Anything running as your user account can read your
  shortcuts, settings, cache and cookies. Breeze does not defend against that.

## Security posture

Breeze has had one internal security review; findings rated critical and high were fixed, and
the notable ones are worth stating plainly:

- The homepage is the only origin allowed to talk to the host process, matched on parsed
  origin, and host messaging is switched off on every other page.
- Remote pages cannot navigate a tab onto Breeze's internal pages.
- Shortcut and bookmark URLs are restricted to `http` and `https` on save and on load, and the
  homepage carries a restrictive content security policy.
- Downloads are confined to the configured folder with sanitised names.
- Requests to open new windows become tabs, so nothing renders without a visible address bar.
- Favicon discovery rejects non-web schemes and any destination, including redirect targets,
  that resolves to a loopback, private or link-local address.

Breeze is an early-stage project and has not yet undergone an external security audit. See
[SECURITY.md](SECURITY.md) to report an issue.

## Building

Requires the .NET 10 SDK and the Microsoft Edge WebView2 runtime (present on current Windows
installs). Every page is rendered through that runtime, so when it is missing Breeze explains it
in a dialog, offers the download page and exits rather than opening a window that cannot show
anything. The installer will not complete without it either.

```bash
dotnet build
dotnet run --project Breeze
```

To build the Windows installer, `dist\Breeze-v<version>-Setup.exe`:

```powershell
powershell -ExecutionPolicy Bypass -File tools\build-installer.ps1
```

That needs [Inno Setup 6](https://jrsoftware.org/isinfo.php) as well. See
[docs/Installer.md](docs/Installer.md) for what it produces and how to prepare a release.

## Roadmap

### Browsing

- [x] Bookmarks
- [x] Bookmark folders
- [ ] A bookmark manager, with search, import and export
- [ ] History page
- [ ] Downloads manager
- [ ] Find in page
- [ ] Reopen closed tab
- [ ] Restore previous session
- [ ] Pinned tabs
- [ ] Mute tabs
- [ ] Incognito mode

### Homepage

- [ ] Loading animation in the favicon position while a shortcut is being created instead of delaying the shortcut until the favicon finishes downloading.
- [ ] Import/export shortcuts
- [ ] Homepage customization

### User Interface

- [ ] Compact mode
- [ ] UI scaling
- [ ] Keyboard shortcut customization
- [ ] Custom accent colors

### Privacy & Security

- [ ] Permission prompts
- [ ] Complete a network capture of an idle Breeze session to validate privacy claims
- [ ] Cookie and site data controls

### Long-term

- [ ] Multiple user profiles
- [ ] Guest mode
- [ ] Cross-platform support

## License

Breeze is licensed under the MIT License.
See [LICENSE](LICENSE) for details.
