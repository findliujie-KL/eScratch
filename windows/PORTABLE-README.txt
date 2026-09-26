eScratch for Windows x64 - .NET Framework 4.8 compatibility build

Extract this entire ZIP to one folder, then run eScratch.exe.
Keep eScratch.exe.config, DLLs, Assets and x64 beside the executable.

Requires .NET Framework 4.8 or later (4.8.1 works) and the Microsoft Visual C++
x64 runtime for OCR. No .NET 10 runtime, Chromium, or Electron is required.
If a prerequisite is missing, use the compact installer or Microsoft's pages:
https://dotnet.microsoft.com/download/dotnet-framework/net48
https://learn.microsoft.com/cpp/windows/latest-supported-vc-redist

History/settings/drafts/OCR downloads use the same Windows profile directory
as the .NET 10 WPF version: %LOCALAPPDATA%\eScratch\Wpf.
Exit another WPF version from its tray before running this build.
Keep this folder in the same location after enabling Start with Windows.

Ctrl+J: show/hide (configurable). Closing hides to tray; right-click tray to Quit.
Restoring starts a blank entry after saving the previous nonblank entry to History.
Ctrl+H: History. Ctrl+T: new draft. Ctrl+Shift+C: copy. Ctrl+Shift+V: Markdown paste.
Paste screenshots for offline OCR. English is bundled; add languages in Settings.
The toolbar pin toggles Always on Top. Word count can be hidden in Settings.

This is an unsigned compatibility build. See THIRD-PARTY-NOTICES.txt and Assets/Licenses.
