# eScratch for Windows

A lightweight native Windows scratchpad built with C# and WPF on .NET 10.
This branch contains the Windows application only. The Electron implementation
is maintained separately on the `main` branch.

## Develop

Install the .NET 10 SDK and Visual C++ x64 Redistributable, then run:

```powershell
./windows/run.ps1
```

The scripts also support a workspace SDK in `.tools/dotnet`.
Open `windows/eScratch.slnx` in Visual Studio to edit the application.

## Features

- Global show/hide shortcut and copy-on-hide
- System tray support
- Plain-text editor, light/dark themes, and customizable shortcuts
- Configurable history (10 entries by default) and Clear All
- Offline screenshot OCR with English bundled and searchable language downloads
- Restore defaults in Settings, with confirmation; keeps the draft and downloaded
  languages, and retains the newest 10 history entries

## Build and test

```powershell
dotnet run --project windows/eScratch.Tests
./windows/build.ps1 -Publish -SelfContained
```

See [the Windows development guide](windows/README.md) for prerequisites,
compact builds, data migration, and testing details.
