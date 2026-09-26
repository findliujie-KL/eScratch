# eScratch

A lightweight scratchpad that lives one shortcut away. Draft a message, hit the shortcut again, and it's already on your clipboard — ready to paste anywhere.

Built for the workflow of writing chat messages, AI prompts, and quick notes that you type once and send.

[![Demo](https://img.youtube.com/vi/qwj9fr77vQg/maxresdefault.jpg)](https://youtu.be/qwj9fr77vQg)

https://youtu.be/qwj9fr77vQg

## Install

This fork publishes Electron builds for Windows, macOS, and Linux, plus a native WPF version for Windows. Choose a version from the [Releases](https://github.com/findliujie-KL/eScratch/releases) page:

- **Electron portable (main branch):** download the asset ending in `-portable.zip` from an Electron release, extract the entire folder, and run `eScratch.exe`. No installation or separate .NET runtime is required. English OCR is bundled; additional languages can be downloaded in Settings.
- **Electron installer / Mac / Linux:** use the Windows Setup EXE, the DMG matching your Mac (arm64 for Apple silicon or x64 for Intel), or the Linux AppImage from the newest Electron release.
- **WPF for Windows (wpf-rewrite branch):** choose the compact Setup EXE or extract the entire portable ZIP from the newest stable WPF release. The compact installer can download .NET 10 Desktop Runtime (x64) and Visual C++ runtime when needed. The portable ZIP includes .NET; native OCR still needs the Visual C++ x64 runtime.

The Electron portable ZIP contains the executable, runtime DLLs, resources and OCR dependencies. Keep the entire extracted folder together. Settings, draft history, and downloaded OCR languages are stored in your Windows user profile, not beside the executable. Closing the editor hides it to the tray; use the tray menu to quit fully.

These builds are unsigned; Mac builds are not notarized.

## How it works

1. Press `Ctrl+J` to summon the editor
2. Type your text
3. Press the shortcut again — the window disappears and your text is copied to clipboard
4. Paste wherever you need it

That's it. No save dialog, no file management, no friction.

The shortcut is fully customizable — open settings and press your preferred key combination to change it.

The app lives in the system tray (menu bar on macOS), with no taskbar or Dock icon. Restoring a hidden or minimized editor saves the previous nonblank draft to History and starts a blank entry. Bringing an already-visible window into focus keeps its text.

## Features

- **Instant toggle** — Global shortcut brings up the editor from any app, and hides it just as fast
- **Auto-copy on hide** — Text is copied to clipboard when the window is dismissed via shortcut
- **Focus restore** — On macOS, focus returns to the app you were using before
- **History** — Past entries are saved automatically when you start a new draft (10 by default; configurable in Settings)
- **Always on Top** — Toggle the toolbar pin to keep the editor above other windows
- **Live word count** — Word-compatible counting in the bottom-right corner; show/hide it in Settings
- **Dark / Light theme** — Toggle between dark and light mode
- **Configurable shortcut** — Change the global shortcut in settings by pressing your desired key combination
- **Show whitespace** — Optionally reveal spaces, tabs, and full-width spaces as visible markers
- **Screenshot OCR** — Paste an image to insert its recognized text directly into the editor
- **Downloadable OCR languages** — Install and select additional recognition languages from settings for offline use
- **Start at login** — Optional in Settings on Windows/macOS; off by default, launches quietly in the tray. Keep portable executables in the same location after enabling it.
- **Windows tray mode** — Closing the window keeps the app in the system tray; double-click the tray icon to restore it

## Development

```bash
npm install
npm run dev
```

Run the main-process behavior tests with:

```bash
node --test tests/menu-bar.cjs tests/mac-release.cjs tests/resume.cjs tests/word-count.cjs
npm run test:markdown
```

## Tech Stack

Electron + React + TypeScript, bundled with Vite.

## Paste as Markdown

Right-click in the editor for Paste (including screenshot OCR) or Paste as Markdown.
Ctrl+Shift+V invokes Paste as Markdown while the editor has focus. Change it in
Settings; Restore defaults resets it to Ctrl+Shift+V. Ctrl+V remains normal Paste.

Markdown paste converts basic headings, bold/italic/strikethrough, links, and lists
to visible plain-text Markdown. Plain-text-only content is left unchanged; image-only
clipboards use normal Paste instead. Complex document layouts are not reproduced.

When plain text, HTML, and RTF are present together, it attempts deletion recovery
by comparing normalized plain text against the HTML text. A unique deletion-only
alignment produces strikethrough, e.g. pigdog versus dog becomes ~~pig~~dog.
Ambiguous alignments fall back to ordinary Markdown conversion. This is a heuristic,
not Word revision metadata: unrelated differences between clipboard formats can also
look like deletions. Insertions are not inferred. RTF presence enables the heuristic;
HTML supplies the formatting. Clipboard content is processed locally.

### Word count

The optional live counter follows Microsoft Word desktop counting conventions: East Asian characters (including Japanese kana, Korean syllables, and full-width punctuation) count individually; other text is counted in runs separated by Word-style boundaries. Standalone punctuation and emoji can contribute to the total. The implementation runs offline without Microsoft Word. Regression fixtures were measured using Word 16.0.19127 `Range.ComputeStatistics(wdStatisticWords)`; results in other Word versions or document structures may differ.

Press **⌘ Command+Y** on macOS or **Ctrl+H** on Windows/Linux while using the editor or History panel to show/hide History. This shortcut is local to eScratch.
