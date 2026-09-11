# Supershot

A local screenshot studio for Windows. Capture a region, window, or display; compose it with a background, frame, annotations, and a styled pointer; copy or save a PNG.

## Install

Download **Supershot-win-Setup.exe** from a published [GitHub release](https://github.com/stvoorhees/supershot/releases). The installer adds Start menu and desktop shortcuts and installs per user. The .NET runtime is bundled; Setup checks for WebView2 and installs it if needed. No SDK or terminal is needed to use the app.

**Release status:** this change prepares the first installable release. The source repository does not automatically provide a download until a maintainer publishes a built release. Unsigned test builds may show a Windows SmartScreen warning; production signing is a separate release requirement.

Open Supershot from its shortcut. It opens the editor and remains available in the tray after you close the window. Double-click the tray icon or launch the shortcut again to reopen the same editor. Choose **Quit Supershot** in the tray to exit.

## Capture and edit

- **Ctrl+Shift+2** captures a region. Choose another shortcut in Settings if it conflicts with another app.
- **Capture** inspector: region, window, or the display under the pointer; immediate capture or a 3, 5, or 10 second timer. Escape cancels region/window selection.
- **Style** inspector: gradient, solid, image, or transparent backgrounds, padding, rounded corners, shadow, and decorative window frames.
- **Pointer** inspector: show/hide the cursor, original Windows shape or a dark/light pointer, size up to 3×, and a soft highlight. Use **Place pointer on image** to position it; Escape cancels placement. Pointer edits and annotations support undo/redo.
- Annotation toolbar: arrows, rectangles, ellipses, lines, text, numbered steps, highlight, blur, and opaque redaction. Drag to draw; select to move or resize. Delete removes a selection. Ctrl+Z undoes; Ctrl+Shift+Z or Ctrl+Y redoes.
- Ctrl+S saves; Ctrl+O opens an image. Paste or drop an image to edit it. Hold Space or use the middle mouse button to pan; use the zoom controls to fit the canvas.
- Light and dark appearances are available from the title bar. The interface is entirely local, including all fonts and assets.

### Pointer timing

Immediate captures preserve the pointer sampled before the selection overlay appears. This avoids capturing the overlay’s crosshair; the pointer is visible only if its hotspot falls inside the captured image. When starting from an editor button or tray menu, that position may be outside your selected region. Use a timer to move the cursor onto your subject after selection, or place a styled pointer in the editor. Cursor styling remains a separate layer until PNG export. An imported image that already includes a cursor cannot have that embedded cursor removed by the pointer toggle.

Blur is a visual effect, **not secure redaction**. Opaque redaction is rendered last into the exported PNG so other annotations cannot reveal the pixels beneath it. Confirm that the entire sensitive area is covered before sharing. The original source image remains in the editor’s memory while the session is open.

## Updates and privacy

Supershot checks GitHub Releases at startup and every six hours while running, and downloads an available stable update through Velopack. Turn off **Check automatically** in Settings to stop automatic checks; **Check for updates** remains available. Switching the preference off does not cancel an already-running download.

An **Update ready** button appears after downloading. **Restart to update** asks you to save/copy your work before handing installation and restart to Velopack. No forced background restart and no automatic apply on startup. A previously downloaded update remains available after reopening the app. Development builds explain that installation is required for updates.

Screenshots are processed locally and are never uploaded. Update checks/downloads contact GitHub and its asset hosts; there is no telemetry. Settings live in `%AppData%\Supershot\settings.json`; the editor’s WebView2 profile lives in `%LocalAppData%\SupershotData\WebView2`, outside the replaceable installation. An editor session is not automatically saved between app exits. Windows/software policy can still restrict capture or installation.

## Build and package

Requires .NET 10 SDK to build. On Windows:

```powershell
dotnet run --project src/Supershot
./scripts/package.ps1 -Version 0.2.3
```

`artifacts/releases/` contains the installer, portable ZIP, full update package, and `releases.win.json`. Keep the app ID **Supershot**, channel **win**, and x64 architecture stable across releases. The packaging script pins Velopack 1.2.0 and downloads and installs the shared .NET 10 Desktop Runtime (x64) and WebView2 when missing. The portable ZIP requires those runtimes to be installed already. Upgrades from bundled-runtime releases may prompt to install the shared runtime before applying the update. These builds target Windows x64 (Windows ARM64 emulation is not part of the verified scope).

On macOS/Linux you can cross-build, but cannot run the Windows shell:

```sh
dotnet publish src/Supershot -c Release -r win-x64 --self-contained false -p:EnableWindowsTargeting=true -o artifacts/publish
dotnet tool restore
dotnet tool run vpk -- '[win]' pack --packId Supershot --packVersion 0.2.3 --packDir artifacts/publish --mainExe Supershot.exe --packTitle Supershot --icon assets/Supershot.ico --framework net10.0-x64-desktop,webview2 --runtime win-x64 --channel win --outputDir artifacts/releases --skip-updates
```

### Release workflow

**Build and verify** packages a test build on Windows and runs the browser checks. **Prepare Windows release** is manually dispatched with a three-part version (for example `0.2.1`), tests the editor, packages the app, and creates a **draft** GitHub release with all update assets. Publishing the reviewed draft makes that version discoverable. A normal source push does not release an update. Do not replace the app identity or edit release indexes by hand.

The workflow currently produces unsigned evaluation packages. Before public distribution, configure a signing identity/service and pass the supported `VPK_SIGN_TEMPLATE` environment variable to the packaging step, following [Velopack signing guidance](https://docs.velopack.io/packaging/signing). Keep signing secrets in GitHub Actions secrets. Signing must cover the app, updater, and installer; signing only the final Setup executable is insufficient. Provisioning a certificate/service is not included in the source change.

### Verification

```sh
npm ci
npx playwright install chromium
npm test
```

The browser checks cover the editor’s empty state, themes, native message bridge, update progress/retry/restart UI, pointer exports, opaque redaction, annotation history, text input, PNG download, and minimum/large window layouts. These tests emulate host messages; they do not execute Windows capture APIs or install an update.

Before publishing, run the [Windows acceptance checklist](WINDOWS-TESTING.md). A successful cross-build or package creation does not substitute for those checks.

## Architecture

- `src/Supershot/Program.cs`: Velopack startup hooks and single-instance entry point.
- `App.xaml.cs`: tray, capture orchestration, editor activation, periodic update checks.
- `CursorSnapshot.cs`: Win32 cursor snapshot, hotspot, and transparency reconstruction.
- `ScreenCapture.cs`, `RegionOverlay.*`: physical-pixel GDI capture and selection overlay.
- `UpdateService.cs`: stable GitHub update source, serialized checks/downloads, explicit install.
- `EditorWindow.*`: local WebView2 host and native file/clipboard/settings bridge.
- `editor/index.html`, `polish.css`: offline canvas editor and visual design.
- `assets/icon.svg`, `tray.svg`: editable icon masters. `npm run icons` regenerates PNG and multi-resolution ICO assets. Tray art uses a contrasting outline for light/dark taskbars.

## License

MIT
