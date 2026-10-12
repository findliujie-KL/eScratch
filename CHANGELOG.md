# Changelog — WPF / .NET Framework 4.8

User-facing changes for this branch. “Unreleased” means changes after the last tagged baseline; development milestone dates are commit dates, not release dates. Installer files may have been updated separately from tags.

## 3.0.0 — 2026-10-12

- Book paper is now the default theme for new profiles and restored defaults; existing theme choices are preserved.

- Removed system-wide mouse-button toggling and its settings/help controls. Keyboard window toggling and tray controls remain available; old saved mouse gestures are ignored.

- Removed unconditional paper redraws on every layout update, which could sustain rendering work. Global mouse gestures now run on a dedicated input thread so editor rendering cannot block system mouse processing; repeated pending toggles are coalesced.

- Fixed premature wrapping: ordinary text uses the normal right margin. Only expression lines reserve space for live results; a result wraps to a continuation row when needed.

### Added
- Left, Center, and Justify scratch alignment in All settings → Appearance. Justify is the default; alignment affects presentation only.
- Four paper themes with light/dark pairs: Porcelain (default), Book paper, Sage notebook, and Drafting paper. All settings includes texture intensity; Sage rules follow actual text rows, font size, wrapping, and zoom.
- Compact Quick settings with toggle shortcut and common window switches, grouped full-window All settings, and Help & shortcuts (F1) reflecting current shortcut assignments. Settings apply immediately; numeric fields apply on Enter or leaving the field.
- Right-click live calculation results to copy equivalent grouped, plain, K/M/B/T or percentage formats, preserving the displayed value.
- Live calculation results beside expression lines, outside saved text and selection. Click a result to copy its value; existing explicit calculation shortcuts remain available.
- Optional opening near the mouse pointer and reopening the last scratch when restoring from the tray. Both default off; the existing save-to-history and start-blank behavior remains the default.
- Editable inline Markdown view with a source toggle; selection shortcuts for bold (Ctrl+B), italic (Ctrl+I) and strikethrough (Ctrl+Shift+X). Typing inside a span and replacing its selected text preserve formatting; typing before a span stays plain, while typing immediately after continues its formatting. Headings and list markings remain literal. Saved content stays plain Markdown.
- Selected-font typeface preview in the Settings box, localized Chinese font names where available, and Chinese sample text for fonts with matching glyphs. Font metadata checks are deferred until a dropdown row is displayed.
- Customizable scratch font and base size (8–48), with a virtualized installed-font dropdown that previews each name in its own typeface, and font reset. Zoom scales relative to the saved base size.
- Full-window sticky-note history board with responsive columns, distinct pinned notes, and full-text search (including Chinese).
- Inline calculator: Ctrl+Enter evaluates the current arithmetic line and reveals up to two more useful decimal places on repeat, without trailing zeros.
- Percentages and scale units (K, M, B, T and names such as hundred, thousand and million).
- Ctrl+Shift+Enter cycles equivalent result units, skipping units whose absolute value would be below 0.01. Percentages initially use up to two decimal places; scaled units use up to four.
- Number-selection context actions to add thousand separators or convert a number to English words.
- Editor zoom from 75% to 250% using Ctrl+wheel, with Ctrl+0 to reset.
- Persistent history pins with compact pin/unpin icons. Pinned entries appear first and do not count toward the unpinned history limit.
- Optional copying of the draft when hiding the window, disabled by default.
- App version and edition information at the bottom of Settings.

### Changed
- The mode button identifies the current view with a styled Abc label for formatted text and */~ for Markdown source.
- The global toggle brings a background window to the front and hides it when it is already active.
- Temporary notices disappear after one second.
- Clear History removes only unpinned notes; restoring defaults preserves pins and the newest ten unpinned entries.

### Removed
- The manual copy-all action, its Ctrl+Shift+C shortcut and its Settings field. Normal selection copy and optional copy-on-hide remain available.

### Fixed
- Scan and cache installed fonts in the background after startup, so opening Settings does not block the UI while fonts are inspected.
- Preserve style ranges when applying italic across an existing bold-to-plain boundary, including adjacent closing and opening Markdown markers.
- Hide symbol and icon fonts from the font picker using actual glyph coverage, while retaining text fonts across scripts.
- Omit the extra Chinese font sample when the displayed font name already contains Chinese characters.
- Align the history search hint with the text caret and keep font changes unapplied until Settings is saved.
- Clipboard content containing both text and an image preview (such as copied Excel cells) pastes the text instead of triggering OCR.

### Earlier development milestones

- **2026-09-30** — Configurable system-wide middle-click and modifier + middle-click window toggle, with matching clicks consumed; themed Clear History and Restore Defaults confirmations. [Commit 33afb9b](https://github.com/findliujie-KL/eScratch/commit/33afb9b)
- **2026-09-28** — Reorder selected OCR languages by priority, clean excessive spaces between Chinese characters, and optionally join wrapped OCR lines. [Commit 588d868](https://github.com/findliujie-KL/eScratch/commit/588d868)
- **2026-09-26** — Port the WPF app to .NET Framework 4.8, retaining existing WPF features and shared saved data; provide portable folder ZIP and compact installer build paths without a .NET 10 dependency. [Commit 1db9e65](https://github.com/findliujie-KL/eScratch/commit/1db9e65)

## v2.1.0-wpf — 2026-09-25 (inherited WPF baseline)

- Tray workflow, optional login startup, a new scratch when resuming, and an Always on Top toggle.
- Optional multilingual word count and a history shortcut (Ctrl+H).
- Screenshot OCR, downloadable OCR languages, configurable history and Restore Defaults.
- Paste as Markdown with an editable shortcut and best-effort recovery of Word tracked deletions.
- Clipboard OCR compatibility improvements, busy-clipboard handling, and expiring status messages.

[Tagged source](https://github.com/findliujie-KL/eScratch/tree/v2.1.0-wpf)

## Earlier history

This is a curated changelog, not a record of every source change. For older work and exact diffs, see the [branch commit history](https://github.com/findliujie-KL/eScratch/commits/wpf-net48/).
