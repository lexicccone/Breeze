# Product

Breeze is a lightweight, privacy-focused Chromium-based browser for Windows desktop. It hosts WebView2 inside an Avalonia shell, so the chrome (tabs, address bar, settings) is native UI while page rendering is delegated to Edge WebView2.

## Priorities

1. **Privacy by default.** Strict tracking prevention, autofill and password autosave off, no browser extensions, no telemetry. Browsing data stays in a local user data folder (`%LOCALAPPDATA%\Breeze\WebView2`). Never add code that sends user data, URLs, or usage stats to a remote endpoint.
2. **Lightweight.** Few dependencies, small startup cost, no dead weight (see the WebView2 wrapper trimming in the csproj). Question every new package.
3. **Native feel.** The shell should look and behave like a first-class Windows app.

Default search engine is DuckDuckGo. The bundled start page is served from a virtual host (`https://breeze.start/index.html`) rather than a `file://` URL.

## UI and design rules

- No emojis anywhere in the UI, in either the Avalonia shell or bundled HTML pages.
- Use clean vector icons from one consistent set (Fluent / Material Symbols style). Inline SVG with `fill: none` + `stroke` in HTML, path geometry or `PathIcon` in XAML. No raster icon fonts, no mixed icon styles.
- Minimal, desktop-native appearance over colorful or playful design. Restrained palette driven by CSS custom properties / theme resources, one accent color, light and dark variants.
- Animations are subtle and purposeful: short transitions (~120ms) on state change like hover and focus. No looping, bouncing, or attention-seeking motion.
- Every element has a functional purpose. No decorative graphics, gradients, shadows, or filler text.
- Interactive elements need accessible names (`aria-label`, `AutomationProperties.Name`) and visible focus states.
