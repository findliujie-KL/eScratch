# eScratch

A lightweight scratchpad that lives one shortcut away. Draft a message, hit the shortcut again, and it's already on your clipboard — ready to paste anywhere.

Built for the workflow of writing chat messages, AI prompts, and quick notes that you type once and send.

[![Demo](https://img.youtube.com/vi/qwj9fr77vQg/maxresdefault.jpg)](https://youtu.be/qwj9fr77vQg)

https://youtu.be/qwj9fr77vQg

## Install

### Manual download

Pre-built binaries for macOS, Windows, and Linux are available on the [Releases](https://github.com/findliujie-KL/eScratch/releases) page.

For macOS releases built with this configuration, choose the `-arm64.dmg` download for Apple Silicon or `-x64.dmg` for an Intel Mac. Intel support through Homebrew also requires the tap to select the matching architecture; see [the release guide](docs/RELEASE.md).

If you download manually on macOS, the app is not signed with an Apple Developer certificate, so macOS may show a warning. To allow it, run:

```bash
xattr -cr "/Applications/eScratch.app"
```

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
- **History** — Past entries are saved automatically when you start a new draft (up to 100)
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
