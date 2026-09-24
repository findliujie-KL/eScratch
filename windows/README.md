# eScratch for Windows (WPF)

A native C# / WPF rewrite on .NET 10, targeting Windows x64. The Electron app is maintained separately on the main branch.
No Chromium, Node.js, web server, or Electron is used by this application.

## Features

- Global show/hide shortcut (Ctrl+J by default); hiding copies the draft.
- Close button hides to the tray. Double-click the tray icon to restore;
  right-click and choose Quit to exit completely.
- Plain-text editor with Unicode, undo/redo, word wrap, configurable indentation,
  visible whitespace, and a character count.
- New draft (Ctrl+T) saves the current text to history. Copy draft uses Ctrl+Shift+C.
- Configurable shortcuts, always-on-top, and dark/light themes (light by default).
- Frameless title bar, compact icon toolbar, and right-side history/settings
  panels matching the original app. Drag the title bar to move the window;
  resize from its edges. Click outside a panel or press Escape to dismiss it.
- History restore, individual deletion, Clear All, and a configurable 1–1000 entry
  limit (default 10). Reducing the limit prompts before removing older entries.
- Screenshot paste runs native Tesseract OCR in the background and inserts the
  result at the selection. If the draft changes during recognition, the result is
  saved to history instead of overwriting the new text.
- English OCR bundled for offline use; searchable dropdown of 163 official fast
  language/script models, download progress, cancellation, removal, and multiple
  selected recognition languages. Images and draft text never leave the PC.
- Draft autosave, atomic state writes, corrupt-data backup, and single instance.
- Restore defaults with confirmation, resetting preferences while preserving the draft
  and installed OCR models. The newest 10 history entries are retained.

## Build and run

Install the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
and the [Visual C++ x64 Redistributable](https://aka.ms/vs/17/release/vc_redist.x64.exe).
The scripts also recognize a workspace SDK at `.tools/dotnet`.

From the repository root in PowerShell:

```powershell
./windows/run.ps1
```

The first build restores NuGet packages and downloads the English model from the
official `tesseract-ocr/tessdata_fast` repository. Subsequent ordinary development
builds use the local copies. WPF changes need compilation, but do not require
packaging or an installer. Visual Studio's WPF Hot Reload can speed up UI changes.

Close the Electron app before running WPF to free Ctrl+J. A conflicting global
shortcut is reported in the status bar; choose another shortcut in Settings.

## Checks

```powershell
dotnet run --project windows/eScratch.Tests
dotnet run --project windows/eScratch.Tests -- --network
```

The console runner exits nonzero on failure. The default checks use temporary
data, exercise persistence and actual Windows hotkey registration, and recognize
a generated image with native OCR. `--network` additionally downloads Spanish,
recognizes with two languages, removes it, and verifies cancellation cleanup.
The WPF integration checks also exercise image paste, undo, editing during OCR,
theme initialization, image-only and PNG-only routed paste commands, and language search after changing a selection. The full
suite currently contains 39 checks. It does not modify your application data or
clipboard.

## Portable releases

```powershell
./windows/build.ps1 -Publish
./windows/build.ps1 -Publish -SelfContained
```

Outputs are in `windows/artifacts`:

- `compact`: smaller, requires the .NET 10 Desktop Runtime (x64).
- `standalone`: includes the .NET runtime, so a separate .NET installation is not needed.

Both include native OCR and English data and require the Visual C++ x64 runtime.
Extract the entire ZIP and run `eScratch.exe`; the EXE must stay beside its assets
and native libraries. These are unsigned portable packages, not installers.
License texts are included under `Assets/Licenses`.

## Data and migration

WPF data lives in `%LOCALAPPDATA%/eScratch/Wpf` (`state.json` and `tessdata/`).
On first launch, settings and history are copied from the Electron profile under
`%APPDATA%/eScratch`, `escratch`, or `one-time-editor`. Electron data is not modified.
English starts selected; additional languages should be downloaded again because
the native rewrite uses Tesseract fast models rather than the JavaScript models.
Electron's theme was stored separately, so the WPF theme initially defaults to light.

For an isolated testing profile:

```powershell
eScratch.exe --data-directory C:/Temp/eScratch-test
```

## Scope

This is a Windows x64 implementation. macOS/Linux behavior is intentionally absent.
The UI uses native Windows controls rather than reproducing the web UI pixel for
pixel. Existing icon artwork is retained. The language catalog is a checked-in
snapshot of all `.traineddata` files from `tessdata_fast`; model downloads require
internet access, while recognition after download works offline.



