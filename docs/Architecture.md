# Breeze Architecture

Orientation for contributors. Breeze is a native Avalonia shell around the Microsoft Edge
WebView2 engine: Breeze owns the chrome (title bar, tabs, toolbar, settings), the engine owns
page rendering. Windows only, .NET 10, MVVM, no DI container and no third-party packages beyond
Avalonia and WebView2.

## Application structure

```
Program.cs            entry point: shell identity, crash guards, AppBuilder
App.axaml(.cs)        Fluent theme, merged resource dictionaries, creates the window and model
Assets/               Palette.axaml, Icons.axaml, CaptionButtons.axaml, Brand/ (logo and icon),
                      StartPage/ (bundled HTML)
Controls/             native interop and input: WebView, TabStrip, AddressBox, DragArea, MiddleClick
Models/               data and the interfaces that cross the view/view model boundary
Services/             process wide, UI agnostic concerns; static classes with cached state
Utilities/            RelayCommand, KeyboardState, WindowIcons
ViewModels/           MainWindowViewModel, TabViewModel, SettingsTabViewModel, SettingsViewModel,
                      BookmarkViewModel, ShortcutRowViewModel
Views/                MainWindow, SettingsView
tools/                build-brand.ps1, regenerates the logo bitmap and icon from the vector master
```

**How the layers talk.** Avalonia's visual tree holds the chrome. `Controls/WebView` is a
`NativeControlHost` that creates a `CoreWebView2Controller` on the child window Avalonia gives it,
so the engine renders into a native HWND parented by the Breeze window. Everything else is
bindings.

Two directions to keep straight:

- **View model to control**: ordinary bindings. `WebView.Source` is two-way — assigning it
  navigates, and the control writes the document URL back (guarded by `_syncingSource` so the
  echo does not re-navigate). `DocumentTitle`, `Favicon`, `CanGoBack`, `CanGoForward` and
  `IsLoading` are `DirectProperty` with two-way default binding: the control is the source of
  truth, the view model mirrors them for the toolbar and tab strip.
- **Control to view model**: small interfaces in `Models/`, never concrete view model types.
  `WebView` implements `IWebNavigator` and hands itself to its tab through
  `IWebNavigatorHost.Navigator` in `OnDataContextChanged`. `TabStrip` reorders through
  `ITabReorder` on its `DataContext`.

**Services** are static with lazily cached state, deliberately: there is one browser process, one
settings file, one shortcut file and one bookmark file, so a container would add ceremony without
adding anything.
Every service that touches the disk or the network guards its own failures and reports through
`Services/ErrorLog` (local, size capped). `Utilities/RelayCommand` also catches and logs, because
Avalonia's Win32 dispatcher does not support a main loop exception handler — an exception escaping
a command would terminate the browser and every tab.

## Theme system

Three distinct things, often confused:

| Concept | Scope | Meaning |
|---|---|---|
| `Application.RequestedThemeVariant` | Application | What the user asked for. `Light`, `Dark`, or `Default` when the setting is System. |
| `Application.ActualThemeVariant` | Application | The resolved variant. With `Default` this follows the operating system, so it is always concrete. |
| `WebView.DefaultBackgroundColor` | One controller | The colour the engine paints before a document renders. Prevents a white flash on a new tab. |

`Services/Theming` is the only writer of the engine's colour scheme:

- `Apply(theme)` maps `AppTheme` to a `ThemeVariant`, assigns `RequestedThemeVariant` (which drives
  all Avalonia UI through the theme dictionaries in `Assets/Palette.axaml`), then calls
  `ApplyToEngine`.
- `ApplyToEngine` reads `ActualThemeVariant` — never the requested one, so System resolves to a
  real value — and writes `CoreWebView2Profile.PreferredColorScheme`.
- `Watch` subscribes once to `Application.ActualThemeVariantChanged`, so the OS switching themes
  while the setting is System reaches the engine too.
- `Register(webView)` is called by `WebView.Configure` as each engine is created: it records the
  profile and brings the new view in line with the current theme.

**Why a single writer.** `PreferredColorScheme` lives on the *profile*, and every tab shares one
profile. Previously each `WebView` wrote it from its own `ActualThemeVariantChanged`, so N views
raced to set one shared value; ordering depended on which views happened to be attached. One
writer, driven by the application's resolved variant, makes it deterministic.

**Division of responsibility.** Anything profile wide belongs to the application, via `Theming`.
Anything belonging to one controller stays in the view: `WebView.ApplyTheme` sets only that view's
`DefaultBackgroundColor`, which is why each `WebView` keeps its own theme subscription for that one
line.

**What themes cannot do.** Setting the preference updates `prefers-color-scheme` in every tab
immediately, verified across visible and hidden tabs. Sites that read the preference once at load
and cache their decision (YouTube is the clearest example) stay on the previous theme until
reloaded. That is site behaviour, identical in Chrome and Edge, and no engine setting overrides it.
Breeze deliberately does not force reloads, because that would discard scroll position and form
input.

## Tab architecture

A tab is a `TabViewModel`. `SettingsTabViewModel` derives from it and overrides `Title` and
`IsWebPage`. `MainWindowViewModel` holds **two collections of the same objects**:

- `Tabs` — strip order. Dragging reorders this one.
- `HostedTabs` — creation order, add and remove only. The content host binds here.

This split is load bearing. Reordering the collection the content host is bound to would make
Avalonia rebuild the item containers, which destroys and recreates the `WebView` instances and
loses every tab's page and history. Keeping the host's order stable means dragging only moves
strip items.

**WebView ownership.** The content host is an `ItemsControl` over `HostedTabs` with an overlay
`Panel` and two `DataTemplates` selected by view model type: `SettingsTabViewModel` renders
`SettingsView`, `TabViewModel` renders `WebView`. A settings tab therefore never creates a
WebView2 instance. Each `WebView` owns exactly one `CoreWebView2Controller`; favicon bitmaps are
not owned by the view, see below.

**Lifetime.** Selection drives `IsSelected`, bound to the `WebView`'s `IsVisible`. Avalonia *hides*
the native child window rather than destroying it, so background tabs keep their engine, history
and page state; the controller's own `IsVisible` is gated on `_painted && IsVisible`, which both
suspends rendering for background tabs and keeps a new tab hidden until its first
`DOMContentLoaded` (no white flash). Closing a tab removes it from both collections, which detaches
the container, which calls `DestroyNativeControlCore`: the controller is closed and the view drops
its favicon reference. Closing the last tab raises `CloseRequested`, which `App` wires to
`Window.Close()`.

**Favicons.** A tab shows the icon the engine already fetched for the page, so nothing is
downloaded for it. `CoreWebView2.GetFaviconAsync` hands over a forward only stream, which the
bitmap decoder cannot read from, so `Services/FaviconImages` buffers the bytes before decoding and
keeps the result keyed by favicon URL. The same site in several tabs therefore shares one bitmap
and is never asked of the engine twice; a failure is cached as a failure, and the tab falls back to
the globe glyph. Because those bitmaps are shared, nothing disposes them — dropping the reference
is enough. `FaviconImages` also serves the icons on disk, keyed by file name, for the bookmark bar
and the homepage.

**Navigation routing.** Toolbar buttons bind to commands on `SelectedTab`. `TabViewModel` forwards
them to its attached `IWebNavigator`, which is the `WebView`, which calls the engine. The address
bar is two properties: `AddressText` is what the user types, `Address` is the committed URL bound
two-way to `WebView.Source`; `SubmitCommand` runs `UrlResolver.Resolve` and assigns `Address`.
Middle clicks arrive through the `MiddleClick.Command` attached property (Avalonia buttons do not
report middle clicks) and run `BackInNewTabCommand`, `ForwardInNewTabCommand` or
`ReloadInNewTabCommand`, which read a URL from the navigator and call `RequestTab`, ending at
`MainWindowViewModel.OpenTab`. New tabs open in the foreground, matching the existing convention.

## Settings

`Models/AppSettings` is a plain mutable class; `Services/SettingsStore` owns the single instance
and persists it to `%LOCALAPPDATA%\Breeze\settings.json` as indented JSON with camelCase names and
enums as strings. Writes go through `AppPaths.WriteAtomic` (temp sibling then move) so an
interrupted write cannot leave a truncated file. Loading tolerates everything: missing file,
missing fields, unknown fields, or corruption all fall back to defaults, so new properties can be
added without a migration.

**Propagation.** `SettingsViewModel` property setters mutate the settings object, call
`SettingsStore.Save()` immediately, and raise `PropertyChanged`. There is no change notification
service; consumers read `SettingsStore.Current` when they need a value. The two settings the chrome
must react to at once are pushed instead of observed: the settings page takes an optional callback
from its tab and invokes it after saving, and the bookmark bar shortcut calls back into the open
settings page the same way. A static settings event was avoided deliberately, since it would keep
every settings page that has ever been opened alive.

| Setting | When it takes effect |
|---|---|
| Theme | Immediately, through `Theming.Apply` |
| Show bookmark bar | Immediately: the settings page reports back through a callback given to `SettingsTabViewModel`, and `MainWindowViewModel` raises `IsBookmarkBarVisible` |
| Show Breeze logo on homepage | Immediately: `StartPageBridge.Refresh()` republishes settings to every open homepage |
| Search engine | Immediately: `UrlResolver` reads it per resolution, and the start page receives it in the next bridge publish |
| Download folder | Immediately: read by `Downloads.Resolve` at download time |
| Ask where to save | Persisted only; the control is disabled until a save dialog exists |
| UI scale, compact mode | Persisted only; controls disabled, not yet implemented |
| Startup page and custom URL | **Startup only.** `SettingsStore.StartupAddress()` is read once, for the first tab. Changing it has no effect until the next launch. |

## Homepage

The homepage is plain HTML, CSS and JavaScript in `Assets/StartPage`, copied to the output folder
and served from disk through two WebView2 virtual host mappings registered by `Services/StartPage`:

- `https://breeze.start` → the bundled folder, `HostResourceAccessKind.Deny`.
- `https://breeze.icons` → `%LOCALAPPDATA%\Breeze\Favicons`, `DenyCors`. `Deny` would block the
  page from loading its own cached icons, since the icon host is a different origin.

**Shortcut storage.** `Services/ShortcutStore` keeps `shortcuts.json` (name, url, icon file name).
Mutations run under a `SemaphoreSlim` so two open homepages cannot interleave a read, a favicon
fetch and a write. Each accepted change increments a `Revision`; the page echoes the revision it
last rendered with every change, and a stale revision is refused and the page refreshed, which
prevents an edit or delete landing on the wrong index. Favicons are discovered and cached by
`Services/FaviconCache`, and files no store references are pruned after each change.

**Bridge.** The page calls `window.chrome.webview.postMessage` with `{ type, revision, ... }`;
`Services/StartPageBridge` handles `list`, `save`, `delete` and `move`, then publishes
`{ type: "shortcuts", items, revision, searchUrl, showLogo }` back with `PostWebMessageAsJson`.
Every failure is caught, because the handler is `async void`. The bridge also keeps the engines it
is attached to, so `Refresh()` can republish to the homepages that are open when a setting changes;
an engine whose tab has closed throws on first touch and is dropped, which is the only signal
WebView2 offers.

**Why only the homepage may talk to the host.** The bridge writes files and issues outbound HTTP
requests on the user's behalf, so it is privileged. Two independent gates:

1. `WebView` toggles `Settings.IsWebMessageEnabled` per navigation, on only while the homepage is
   loaded, so remote pages cannot post at all.
2. `StartPageBridge` compares the message source against the homepage origin by parsed scheme,
   host and port — not a string prefix.

## Bookmarks

`Services/BookmarkStore` keeps `bookmarks.json` (title, url, icon file name) with the same shape as
the shortcut store: one cached list, mutations serialized behind a `SemaphoreSlim`, atomic writes,
and a file that tolerates missing or unknown fields. Storage and validation the two stores have in
common live in `Services/WebLinks`: the JSON options, the `http`/`https` URL check, the plain file
name check for icon references, and the read and write helpers.

**Icon pruning.** `FaviconCache` no longer takes the list of icons still in use from its caller.
Every store that references icons registers a source through `Track` at startup, and `Prune`
deletes only what no registered source claims. Both stores prune after each change, so a bookmark
and a shortcut for the same site cannot delete each other's icon.

**View side.** `MainWindowViewModel` rebuilds an `ObservableCollection<BookmarkViewModel>` on the
store's `Changed` event, marshalled to the UI thread. `IsCurrentPageBookmarked` and `CanBookmark`
are recomputed when the selected tab changes, when its `Address` changes, and when the store
changes; `CanBookmark` excludes internal pages, blank tabs and the settings tab. The bar is an
`ItemsControl` over a horizontal `VirtualizingStackPanel` inside a `ScrollViewer`, so a long list
costs only the entries on screen.

**Bar visibility.** Two rules, derived from the bookmark count and the stored setting rather than
extra state: going from no bookmarks to one turns the bar on, and removing the last one turns it
off. In between the setting belongs to the user, so a bar they hid stays hidden.

## Keyboard shortcuts

`Services/KeyboardShortcuts` is the only place a gesture is written down. It holds the catalog of
`ShortcutDefinition` (id, label, default gesture), resolves the gesture in force — a parsed override
from `settings.json`, otherwise the default, otherwise nothing — caches the result so no parsing
happens while keys are pressed, and maps a key press to the registered handler. Adding a shortcut
means adding a definition and registering an action.

Key presses reach Breeze from two places and both resolve through that one lookup:

- **Chrome focused.** `MainWindow.OnKeyDown`.
- **Page focused.** The engine owns the keyboard, so `WebView` handles the controller's
  `AcceleratorKeyPressed`, translates the virtual key and the live modifier state through
  `Utilities/KeyboardState` (the event carries neither in Avalonia's terms), and posts the action to
  the next dispatcher turn. That event is synchronous and blocks the browser process until it
  returns, so the action must not run inside the callback.

## Branding assets

`Assets/Brand/logo.svg` is the master artwork. `logo.png` and `breeze.ico` are generated from it by
`tools/build-brand.ps1`, which renders the vector with headless Edge, so no extra tooling is needed.
The homepage loads the SVG directly; the native views draw the bitmap, since Avalonia has no SVG
support without another package.

The icon file carries nine sizes. 16, 20 and 24 px come from a simplified variant of the artwork,
with sub pixel detail dropped and the shapes stroked, because faithful artwork at that size reads as
a smudge. Everything from 32 px up is faithful. Frames below 256 px are stored as uncompressed DIBs:
Windows reads PNG compressed frames of those sizes unreliably and silently substitutes a rescaled
one. `ApplicationIcon` covers `Breeze.exe`, and `Utilities/WindowIcons` hands the file itself to the
window with `WM_SETICON` once it has a handle, because Avalonia takes a single bitmap and rescales
it for every size, which would discard the small frames.

## Security

The full review lives in the commit history; the current model in short:

- **Trusted internal origin.** `https://breeze.start` is the only origin the host process accepts
  messages from, matched on parsed origin.
- **CSP.** The homepage declares `default-src 'none'` with scripts, styles and images from self,
  icons from the local icon host, and no frame ancestors, form action or base URI.
- **Navigation restrictions.** Navigation to `breeze.start` and `breeze.icons` is cancelled unless
  Breeze initiated it, so a remote page cannot put the privileged page on screen. Back, forward and
  reload of an already vetted entry still work.
- **Host messaging restrictions.** Off by default, enabled only for the homepage (above).
- **Shortcut and bookmark validation.** URLs are restricted to `http` and `https` on save *and* on
  load, so a tampered `shortcuts.json` or `bookmarks.json` cannot place a `javascript:` URL in a
  tile or on the bookmark bar. Icon references must be plain file names with no traversal.
- **Download containment.** The server suggested name is sanitised and the target must resolve
  inside the configured folder, otherwise the download is cancelled.
- **Favicon fetching.** `http`/`https` only; every URL and every redirect hop is validated, host
  names are resolved and all addresses must be public, so loopback, private, link local, CGNAT,
  multicast and IPv6 unique local destinations are refused.
- **Engine posture.** Strict tracking prevention, extensions disabled, autofill and password saving
  off, reputation checking (SmartScreen URL reporting) explicitly disabled, all web permissions
  denied, popups routed into tabs.

Breeze is early stage and has not had an external security audit.

## Known architectural limitations

**Theme synchronization assumes a single WebView2 profile.** `Theming` holds one profile
reference, the last one registered. That is correct today because every tab shares the default
profile, but multiple profiles (on the roadmap) would need `Theming` to track a set, or only the
last registered profile would follow the theme.

**The history mirror exists solely for middle-click Back and Forward.** WebView2 exposes no
navigation history list, so `WebView` mirrors visited URLs (`_entries` plus `_position`) to answer
"what is one step back or forward". It is never used to navigate: the engine still owns all
navigation, and every actual Back, Forward and Reload goes through it. It is also not session
state, and it dies with the control.

**The mirror is approximate and must not be reused without redesign.** It classifies each
`SourceChanged` URL by comparing against neighbouring entries, which is inexact:

- `pushState` and hash navigation track correctly.
- `replaceState` drifts: the engine replaces the current entry while the mirror appends one, so
  `PreviousUrl` can name a URL the engine no longer holds. SPA-heavy sites accumulate these.
- HTTP redirects are believed correct, since `SourceChanged` normally reports the committed URL,
  but this was not tested.
- The initial blank document is missing, so on a new tab's first page Back is enabled (the engine
  has that entry) while middle-click Back does nothing.
- Navigating to the URL already shown is skipped, whereas Chromium generally records an entry.

Every failure mode is benign *because* of the containment above: a middle click opens a slightly
different URL, or nothing happens. Any future feature that needs real history — a history page,
session restore, a back button dropdown — should be built on a purpose-designed structure, not on
this mirror.

**The mirror is capped at 100 entries.** This is a memory guard so a long-lived SPA tab cannot grow
the list without bound. It is not derived from an engine limit. I previously suggested Chromium
keeps roughly 50 entries per tab; **that was unverified recollection, not a documented guarantee.**
A search for a Chromium or WebView2 source or documentation reference did not substantiate it — the
"50" figures that surfaced belong to Firefox's `browser.sessionhistory.max_entries` preference, plus
an [empirical StackOverflow observation](https://stackoverflow.com/questions/16355549/the-maximum-value-of-window-history-length)
that Chrome's `history.length` appeared not to exceed 50. Treat the real WebView2 limit as unknown
until measured. Breeze has not measured it.

**Other tradeoffs worth knowing.** Downloads use the engine's own UI (Breeze only redirects where
files land). Web permissions are refused with no way to allow them. `BrowsingData` clears through
the most recently registered profile, so clearing does nothing if no web tab is open. Local data is
unencrypted, by design: Breeze cannot defend against code already running as the user.
