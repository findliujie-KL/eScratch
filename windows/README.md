# eScratch for Windows (.NET Framework 4.8)

Compatibility work lives on wpf-net48. The .NET 10 application remains on
wpf-rewrite and Electron remains on main. This build targets Windows 10/11 x64,
uses the Windows .NET Framework CLR, and needs no .NET 10 Desktop Runtime.
.NET Framework 4.8.1 also satisfies the runtime requirement.

## Features retained

- Tray-only UI, Ctrl+J show/hide, copy on hide, and configurable global shortcut.
- Start with Windows (off by default), starting quietly in the tray.
- Restoring from the tray archives the prior nonblank draft and opens a blank one.
- Light/dark themes, toolbar Always on Top pin, word wrap, indentation and visible whitespace.
- Ctrl+H History, Ctrl+T New, Ctrl+Shift+C Copy and configurable Ctrl+Shift+V Markdown paste.
- History limit (10 by default), restore/delete/clear, and restore default settings.
- Offline native Tesseract screenshot OCR, PNG/Bitmap fallbacks and alpha repair.
- Searchable OCR language downloads, multiple recognition languages, cancellation and removal.
- Markdown conversion and best-effort deleted-text recovery from Word clipboard formats.
- Optional live word count, verified against the same 249 Word reference cases.
- Draft autosave, atomic replacement, corrupt-data backup, timed notices and single-instance guard.

## Build and run

Install a .NET SDK that supports C# 12 (8 or later), .NET Framework 4.8 or later,
and the Visual C++ x64 runtime. NuGet supplies Framework 4.8 reference assemblies.
A workspace SDK at .tools/dotnet is supported; its version does not change the
target runtime. Visual Studio 2022 can also build the project.

From the repository root:

```powershell
./windows/run.ps1
```

The development executable is windows/eScratch/bin/Debug/net48/eScratch.exe.
It launches directly; do not pass it to dotnet.exe. Close any other eScratch WPF
instance from its tray before switching builds. Close Electron or change the
global shortcut if it already owns Ctrl+J.

## Tests

```powershell
dotnet build windows/eScratch.Tests/eScratch.Tests.csproj
./windows/eScratch.Tests/bin/Debug/net48/eScratch.Tests.exe
./windows/eScratch.Tests/bin/Debug/net48/eScratch.Tests.exe --network
```

The runner checks the actual Framework CLR and target metadata, .NET 10-format
data compatibility, atomic replacement failure, supplementary Unicode characters,
persistence, Word counting, hotkeys, real native OCR and WPF paste/UI behavior.
The network option downloads a language, performs multilingual OCR, removes it,
and tests cancellation. Tests use isolated temporary profiles, not your clipboard
or application notes. Local validation used Windows 11 with Framework 4.8.1;
the compile target is the Framework 4.8 reference API surface.

## Portable ZIP and compact installer

```powershell
./windows/build.ps1 -Publish
./windows/build-installer.ps1
```

The installer script uses .tools/inno/ISCC.exe by default, or accepts -Compiler
with the path to Inno Setup 7. Outputs are isolated under windows/artifacts/net48:

- portable/: complete application folder, including English OCR and library dependencies.
- eScratch-2.1.0-net48-win-x64-portable.zip: extract all files and run eScratch.exe.
- installer/eScratch-2.1.0-net48-win-x64-setup.exe: compact per-user installer.

Both need Framework 4.8+ and the Visual C++ x64 runtime for native OCR. No separate
self-contained flavor is needed: the goal is to use Windows Framework rather
than bundle a modern .NET runtime. The EXE is not a standalone single-file app;
keep its .config, DLLs, Assets and x64 folders alongside it. Packages are unsigned.

Setup checks the Framework Release registry value in both registry views against
Microsoft's 528040 minimum, accepting 4.8.1 and later. It offers to download/install
missing prerequisites, open Microsoft's download pages, or install the app only.
The Framework web bootstrapper is approximately 1.4 MB and downloads further
components from Microsoft; it can require elevation and a restart. The app never
requires admin rights for ordinary use. Hashes and official URLs are pinned in
installer/prerequisites.json. Framework and VC++ installers are not bundled.

/CHECKONLY="C:/path/report.txt" reports prerequisite detection without installing.
For unattended setup, use /PREREQUISITES=download or /PREREQUISITES=skip with
/VERYSILENT; missing prerequisites without an explicit choice fail safely.

The installer uses the existing WPF app identity and per-user destination,
%LOCALAPPDATA%/Programs/eScratch. Installing this variant replaces that WPF app;
to compare builds, run the extracted portable package after exiting the other one.

## Data compatibility

The profile remains %LOCALAPPDATA%/eScratch/Wpf. Notes, settings, history and native
OCR model files retain the existing schema/paths. The login registry value and
single-instance identity are shared with the .NET 10 build. No data migration or
model re-download is needed. Keep portable builds at their registered location;
after moving the EXE or switching a portable login-startup build, toggle Start
with Windows off/on to register its new path. Uninstall preserves application data.

Framework-specific compatibility code replaces newer file-overwrite, hashing,
Unicode and stream APIs. System.Text.Json and its support libraries are bundled
using their Framework-compatible assets. Their 10.x library package version is
not a dependency on the .NET 10 runtime. Third-party notices ship with both packages.

Portable distribution policy: publish the complete application folder as a ZIP. Keep all DLLs, assets and native OCR libraries beside the executable. Do not use single-file publishing or a self-extracting portable EXE. Settings/history remain in the Windows user profile; this packaging does not change data storage.
