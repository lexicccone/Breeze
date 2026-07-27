# Tech Stack

- **Runtime:** .NET 10, `net10.0-windows`, `WinExe`. Windows-only by design.
- **UI:** Avalonia 11.3.x with `Avalonia.Themes.Fluent`. Compiled bindings are on by default (`AvaloniaUseCompiledBindingsByDefault`).
- **Web engine:** `Microsoft.Web.WebView2` used through the **CoreWebView2 / `CoreWebView2Controller` API only**. The WPF and WinForms wrappers are explicitly removed by the `RemoveWebView2Wrappers` target in the csproj; do not reference them.
- **Build system:** MSBuild via a single `Breeze.slnx` solution containing `Breeze/Breeze.csproj`.
- Project settings to respect: `Nullable=enable`, `ImplicitUsings=enable`, `LangVersion=latest`, `InvariantGlobalization=true`, `BuiltInComInteropSupport=true`, `app.manifest` with PerMonitorV2 DPI awareness.

## Commands

Run from the repository root (`cmd`):

```cmd
dotnet restore Breeze.slnx
dotnet build Breeze.slnx
dotnet run --project Breeze\Breeze.csproj
dotnet publish Breeze\Breeze.csproj -c Release
```

There is no test project yet. If tests are needed, add `Breeze.Tests` with xUnit and register it in `Breeze.slnx`.

## C# conventions

- File-scoped namespaces, one type per file, `sealed` by default for concrete classes.
- Expression-bodied members for one-liners; braces on their own lines otherwise. Always use braces for `if` bodies, even single statements.
- Nullable reference types are enabled; annotate rather than suppress. Avoid `!` unless provably safe.
- Prefer `static` classes with a cached `Task` for process-wide resources (see `WebViewEnvironment`) over DI containers. No DI framework is in use.
- Async: `async Task`, `Async` suffix, fire-and-forget only with an explicit discard (`_ = AttachAsync(...)`) and a guard for teardown races (`_detached` pattern in `WebView`).
- XML doc comments (`<summary>`) on public types. Inline comments only where intent is non-obvious.
- No `System.Windows.Forms` / WPF types. `System.Drawing.Rectangle` is acceptable for WebView2 bounds interop.
- Account for DPI: multiply Avalonia `Bounds` by `VisualRoot.RenderScaling` when passing sizes to WebView2.

## MVVM conventions

- Views are `.axaml` + minimal `.axaml.cs` code-behind (`InitializeComponent` only). No business logic in code-behind.
- ViewModels derive from `ViewModelBase` (hand-rolled `INotifyPropertyChanged` with `SetProperty`). No MVVM toolkit dependency.
- Set `x:DataType` on every view and bind with compiled bindings.
- Native/interop concerns live in `Controls`; browser and platform services live in `Services`.

## Front-end conventions (start page and any bundled HTML)

- Plain HTML/CSS/JS, no frameworks, no build step, no external network requests (no CDN fonts or scripts).
- `"use strict";`, `const`/`let`, DOM built with `document.createElement` and `append`/`replaceChildren` instead of `innerHTML`.
- CSS custom properties on `:root` plus a `prefers-color-scheme: dark` override; `color-scheme: light dark`.
- Assets under `Breeze/Assets/**` are copied to output with `PreserveNewest` and exposed via `StartPage`'s virtual host mapping with `CoreWebView2HostResourceAccessKind.Deny`.
