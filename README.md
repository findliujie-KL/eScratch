# eScratch for Windows — .NET Framework 4.8

A lightweight native Windows scratchpad built with C# and WPF, targeting .NET Framework 4.8.
This compatibility branch does not require .NET 10 to run. The .NET 10 version
remains on `wpf-rewrite`; Electron remains on `main`.
This branch contains the Windows application only.

## Install

Build the portable ZIP and compact installer with the commands below. These net48
packages are separate from the existing .NET 10 GitHub releases.

- **Portable:** extract the entire ZIP and run eScratch.exe. Keep its config, DLLs,
  Assets and x64 folders together. Requires .NET Framework 4.8 or later and the
  Visual C++ x64 runtime for OCR.
- **Compact installer:** checks for Framework 4.8+ and Visual C++; offers downloads
  from Microsoft when components are missing. No .NET 10 Desktop Runtime check.
- Windows 10/11 x64 remain the supported target. No installer or portable package
  embeds a modern .NET runtime. Settings stay in your Windows profile.

## Develop

Install a .NET SDK supporting C# 12 (8 or later), .NET Framework 4.8 or later,
and Visual C++ x64 Redistributable, then run:

```powershell
./windows/run.ps1
```

The scripts also support a workspace SDK in `.tools/dotnet`. The SDK is a build
tool only; the generated EXE runs directly on Windows .NET Framework. The 4.8
reference assemblies are restored through NuGet, so a separate targeting pack
is not necessary.
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
dotnet build windows/eScratch.Tests/eScratch.Tests.csproj
./windows/eScratch.Tests/bin/Debug/net48/eScratch.Tests.exe
./windows/build.ps1 -Publish
./windows/build-installer.ps1
```

See [the Windows development guide](windows/README.md) for prerequisites,
package names, compatibility details, and testing.

Existing .NET 10 WPF drafts, settings, history and downloaded OCR languages use
the same data directory and schema. Exit the other WPF version before switching.

### Word count

The optional live counter follows Microsoft Word desktop counting conventions: East Asian characters (including Japanese kana, Korean syllables, and full-width punctuation) count individually; other text is counted in runs separated by Word-style boundaries. Standalone punctuation and emoji can contribute to the total. The implementation runs offline without Microsoft Word. Regression fixtures were measured using Word 16.0.19127 `Range.ComputeStatistics(wdStatisticWords)`; results in other Word versions or document structures may differ.

Press **Ctrl+H** while using the editor or History panel to show/hide History. This shortcut is local to eScratch.
