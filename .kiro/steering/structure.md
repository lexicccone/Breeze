# Project Structure

```
Breeze.slnx                  Solution (slnx format), single project
README.md
Breeze/
  Breeze.csproj              All build settings, package refs, WebView2 wrapper trimming
  app.manifest               DPI awareness (PerMonitorV2), long paths, supported OS
  Program.cs                 Entry point, [STAThread], AppBuilder
  App.axaml / .cs            Application styles (FluentTheme), main window wiring
  Assets/
    StartPage/               Bundled start page (index.html, start.css, start.js)
  Controls/
    WebView.cs               NativeControlHost wrapping CoreWebView2Controller
  Services/
    WebViewEnvironment.cs    Shared CoreWebView2Environment + local user data folder
    StartPage.cs             Virtual host mapping and start page URL
  ViewModels/
    ViewModelBase.cs         INotifyPropertyChanged + SetProperty
    MainWindowViewModel.cs
  Views/
    MainWindow.axaml / .cs
```

## Where things go

- **Controls/** — Avalonia controls, especially native interop hosts. Anything touching window handles or `CoreWebView2Controller` belongs here.
- **Services/** — process-wide, UI-agnostic concerns: WebView2 environment, profiles, bundled content hosting, settings, history/bookmarks storage. Static classes with cached state are the norm.
- **ViewModels/** — observable state and commands for views. One ViewModel per view, named `<View>ViewModel`.
- **Views/** — `.axaml` markup plus thin partial code-behind. Namespace mirrors the folder (`Breeze.Views`).
- **Assets/** — static files copied to output and served through a WebView2 virtual host. Reference them via a `Services` helper, never a hard-coded `file://` path.

## Naming and layout rules

- Namespaces mirror folders under the `Breeze` root namespace.
- File name matches the single type it contains.
- `bin/`, `obj/`, `.vs/`, `.idea/`, `*.user` are gitignored; never edit or commit anything under `Breeze/obj` or `Breeze/bin`.
- New projects must be added to `Breeze.slnx`.
