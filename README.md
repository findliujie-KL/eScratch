# eScratch for Windows

A lightweight native Windows scratchpad built with C# and WPF on .NET 10.
This branch contains the Windows application only. The Electron implementation
is maintained separately on the `main` branch.

## Install

Download the newest stable WPF release from [GitHub Releases](https://github.com/findliujie-KL/eScratch/releases). Choose the compact Windows x64 installer or the portable ZIP. The installer can download missing .NET 10 Desktop and Visual C++ runtimes. The portable ZIP includes .NET; extract all files together and keep the Visual C++ x64 runtime installed for OCR. Settings and history are stored in your Windows profile.

For macOS, Linux, or Electron Windows builds, see the [main branch](https://github.com/findliujie-KL/eScratch/tree/main).

## Develop

Install the .NET 10 SDK and Visual C++ x64 Redistributable, then run:

```powershell
./windows/run.ps1
```

The scripts also support a workspace SDK in `.tools/dotnet`.
Open `windows/eScratch.slnx` in Visual Studio to edit the application.

## Features

- Global show/hide shortcut and copy-on-hide
- Tray-only operation, with no taskbar button
- Optional startup at Windows login (off by default), quietly in the tray
- Restoring the hidden/minimized editor saves the previous nonblank draft to History and opens a blank entry
- For portable builds, keep the executable in the same location after enabling login startup
- Toolbar pin toggles Always on Top; the copy shortcut remains available
- Optional persistent word count in the bottom-right corner (on by default)
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

### Word count

The optional live counter follows Microsoft Word desktop counting conventions: East Asian characters (including Japanese kana, Korean syllables, and full-width punctuation) count individually; other text is counted in runs separated by Word-style boundaries. Standalone punctuation and emoji can contribute to the total. The implementation runs offline without Microsoft Word. Regression fixtures were measured using Word 16.0.19127 `Range.ComputeStatistics(wdStatisticWords)`; results in other Word versions or document structures may differ.

Press **Ctrl+H** while using the editor or History panel to show/hide History. This shortcut is local to eScratch.
