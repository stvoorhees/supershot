# Supershot

A modern, local-only **screenshot utility for Windows** — capture a region with a hotkey, then drop it into a polished editor (gradient/wallpaper backgrounds, padding, rounded corners, soft shadow, aspect presets) and copy or save. The Screen Studio look, applied to stills.

Built to be **enterprise-friendly**: everything runs locally, nothing touches the network, no telemetry, and it uses the sanctioned Windows APIs so it doesn't trip EDR/AV. You clone and build it yourself, so there's no unsigned installer to get blocked.

## How it works

- A tray app registers a global hotkey (**Ctrl + Shift + 2**). Double-clicking the tray icon or the tray menu also triggers a capture.
- The screen dims and you drag a region (Esc to cancel).
- The capture opens in a frameless editor. **The entire UI is a local web app** (`editor/index.html`) rendered in WebView2 — so it looks like a modern app, not classic Windows chrome. WPF is only the invisible shell (tray, hotkey, region overlay, capture, clipboard/save).
- Export: **Copy** to the clipboard or **Save** as PNG.

## Architecture

```
src/Supershot/        .NET 10 WPF shell (net10.0-windows)
  App.xaml.cs         tray icon, global hotkey wiring, capture -> editor
  HotKeyWindow.cs     RegisterHotKey (message-only window; not a keyboard hook)
  RegionOverlay.*     full-virtual-screen selection overlay (returns physical px)
  ScreenCapture.cs    GDI capture (Graphics.CopyFromScreen) -> PNG data URL
  EditorWindow.*      frameless WebView2 host + JS<->C# bridge (save/copy/open/window)
editor/index.html     the polished editor UI (offline, no CDNs) — served to WebView2
                      via a virtual https host so clipboard/canvas APIs work
```

The editor and shell talk over a tiny message bridge: the shell posts the captured image in; the page posts `save`/`copy`/`open` and window controls back out, which the shell handles natively.

## Enterprise notes

- **Local only.** No network calls, no telemetry, no cloud upload. Screenshots never leave the machine.
- **Safe APIs.** `RegisterHotKey` (not a low-level keyboard hook, which AV/EDR flag as keylogger behavior). Capture is GDI today; **Windows.Graphics.Capture** is a planned hardening step (it also blacks out DRM/protected windows).
- **No admin, no drivers.** Runs from your user profile once built.
- Check your org's software policy — some environments have DLP that flags any screen-capture tool.

## Build & run

Prerequisites:
- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- WebView2 Runtime — preinstalled on Windows 11 and current Windows 10 (else the [Evergreen runtime](https://developer.microsoft.com/microsoft-edge/webview2/)).

```
git clone https://github.com/stvoorhees/supershot
cd supershot
dotnet run --project src/Supershot
```

It starts in the tray. Press **Ctrl + Shift + 2** to capture.

> Developing on macOS/Linux? The project cross-compiles for review with
> `dotnet build src/Supershot -p:EnableWindowsTargeting=true` (it can't run there, but it
> type-checks). The editor UI runs anywhere: open `editor/index.html` in a browser.

## Roadmap

- Annotations (arrows, boxes, text, highlight, blur/redact)
- Windows.Graphics.Capture backend (protected-content aware)
- Window / full-screen capture modes; configurable hotkey
- Custom background images, more gradient/wallpaper presets

## License

MIT
