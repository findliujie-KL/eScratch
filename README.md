# eScratch

A lightweight scratchpad that lives one shortcut away. Draft a message, hit the shortcut again, and it's already on your clipboard — ready to paste anywhere.

Built for the workflow of writing chat messages, AI prompts, and quick notes that you type once and send.

[![Demo](https://img.youtube.com/vi/qwj9fr77vQg/maxresdefault.jpg)](https://youtu.be/qwj9fr77vQg)

https://youtu.be/qwj9fr77vQg

## Install

This fork publishes Windows x64 builds. Choose a version from the [Releases](https://github.com/findliujie-KL/eScratch/releases) page:

- **Electron portable (main branch):** download the asset ending in `-portable.exe` from an Electron release and run it. No installation or separate .NET runtime is required. English OCR is bundled; additional languages can be downloaded in Settings.
- **WPF preview (wpf-rewrite branch):** download `eScratch-2.0.0-win-x64-setup.exe` from the newest WPF prerelease. Its compact installer can download the .NET 10 Desktop Runtime (x64) and Visual C++ runtime when needed.

The Electron portable executable extracts its application files to a temporary folder when launched. Settings, draft history, and downloaded OCR languages are stored in your Windows user profile, not beside the executable. Closing the editor hides it to the tray; use the tray menu to quit fully.

These builds are unsigned. macOS and Linux downloads are not currently published by this fork.

## How it works

1. Press `Ctrl+J` to summon the editor
2. Type your text
3. Press the shortcut again — the window disappears and your text is copied to clipboard
4. Paste wherever you need it

That's it. No save dialog, no file management, no friction.

The shortcut is fully customizable — open settings and press your preferred key combination to change it.

On macOS, you can choose whether the app lives in the menu bar or in the Dock. Enable "Show in Menu Bar" in settings to hide the Dock icon and control the editor from the menu bar — closing the editor window then hides it and keeps the current draft available. Turn it off to use it as a regular Dock app. The global shortcut works in either mode.

## Features

- **Instant toggle** — Global shortcut brings up the editor from any app, and hides it just as fast
- **Auto-copy on hide** — Text is copied to clipboard when the window is dismissed via shortcut
- **Focus restore** — On macOS, focus returns to the app you were using before
- **History** — Past entries are saved automatically when you start a new draft (10 by default; configurable in Settings)
- **Dark / Light theme** — Toggle between dark and light mode
- **Configurable shortcut** — Change the global shortcut in settings by pressing your desired key combination
- **Show whitespace** — Optionally reveal spaces, tabs, and full-width spaces as visible markers
- **Screenshot OCR** — Paste an image to insert its recognized text directly into the editor
- **Downloadable OCR languages** — Install and select additional recognition languages from settings for offline use
- **Windows tray mode** — Closing the window keeps the app in the system tray; double-click the tray icon to restore it

## Development

```bash
npm install
npm run dev
```

Run the main-process behavior tests with:

```bash
node --test tests/*.cjs
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
