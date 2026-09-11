# Windows release acceptance

Run on Windows 11 x64 before publishing the first release. Record OS/build, package version, signing status, and results. The macOS development environment can cross-build and package but cannot prove native behavior.

## Installation and app lifetime

- On a clean user profile without the .NET SDK, run Setup. Verify WebView2 detection/bootstrap, Start menu/desktop shortcuts, editor launch, and uninstall registration.
- Open the shortcut twice. Confirm only one tray icon/editor remains and the existing editor activates.
- Close the editor: tray remains; double-click tray restores the image. Quit: app and WebView2 processes exit.
- Confirm the icon is legible at 100%, 125%, 150%, and 200% display scaling, on light and dark taskbars.
- Install/uninstall without admin under the normal per-user setup. Verify behavior when corporate policy blocks installation rather than assuming it will be allowed.

## Capture, pointer, and exports

- Capture region/window/display on each monitor; include monitors left/above primary and mixed DPI scaling. Confirm full overlay coverage and pixel alignment. Escape cancels selection and restores the editor.
- Capture with the shortcut and no delay. Confirm the original pre-selection pointer hotspot matches the subject and the overlay/crosshair is absent.
- Capture with each timer; move the pointer before expiry. Confirm the actual pointer position and shape appear. Test arrow, text caret, hand, enlarged accessibility cursor, and a monochrome cursor.
- Hide/show, dark/light style, scale, highlight, and reposition the pointer. Undo/redo those edits; verify both clipboard and saved PNG match the preview.
- Open/import an image without cursor metadata and place a styled pointer. Confirm Original is unavailable.
- Draw/move/resize annotations, edit text, undo/redo, pan/zoom, and export. Confirm no handles/guides appear in export.
- Cover text with opaque redaction, then overlap it with blur: exported pixels must remain opaque. Confirm crop coverage manually before sharing.
- Exercise clipboard contention, unwritable save folders, canceled save dialogs, and an occupied capture hotkey. Errors must be visible without crashing.

## Actual update between two versions

Use a separate test repository and a build configured to use it when testing unpublished versions. Do not expose an untested update to production users. Install the older package; do not run the app from `bin/Debug`.

1. Build/install version A. Set a custom shortcut, save folder, appearance, and frame preferences.
2. Publish version B plus its generated `releases.win.json` and full package to the configured GitHub source. Draft releases must remain invisible to normal checks.
3. Manually check from version A: checking → download progress → ready. Confirm normal capture/editing still works while downloading and repeated checks do not start concurrent downloads.
4. Cancel the restart confirmation. Confirm the app and current image remain. Save/copy, accept restart, and confirm version B starts and the same shortcuts and preferences remain.
5. Download an update, quit without installing, reopen, then explicitly apply it. Confirm it does not restart unexpectedly on startup.
6. Disconnect the network; check again. Confirm retryable feedback and continued offline capture. Disable automatic checks, reopen, and confirm no update requests occur until a manual check.
7. Test a corrupt package/checksum mismatch, no published releases, and a locked application file. Never mark the update successful when download or installation fails.
8. Uninstall. Confirm screenshots and settings/profile outside the installation are not deleted.

Signing identity provisioning, SmartScreen reputation, and real Windows capture/update execution require separate evidence before a public production release.
