# Changelog — WPF / .NET Framework 4.8

User-facing changes for this branch. “Unreleased” means changes after the last tagged baseline; development milestone dates are commit dates, not release dates. Installer files may have been updated separately from tags.

## Unreleased

### Added
- Inline calculator: Ctrl+Enter evaluates the current arithmetic line and reveals up to two more useful decimal places on repeat, without trailing zeros.
- Percentages and scale units (K, M, B, T and names such as hundred, thousand and million).
- Ctrl+Shift+Enter cycles equivalent result units, skipping units whose absolute value would be below 0.01. Percentages initially use up to two decimal places; scaled units use up to four.
- Number-selection context actions to add thousand separators or convert a number to English words.
- Editor zoom from 75% to 250% using Ctrl+wheel, with Ctrl+0 to reset.
- Persistent history pins with compact pin/unpin icons. Pinned entries appear first and do not count toward the unpinned history limit.
- Optional copying of the draft when hiding the window, disabled by default.
- App version and edition information at the bottom of Settings.

### Changed
- The global toggle brings a background window to the front and hides it when it is already active.
- Temporary notices disappear after one second.
- Clear History explicitly includes pinned entries; restoring defaults preserves pins and the newest ten unpinned entries.

### Removed
- The manual copy-all action, its Ctrl+Shift+C shortcut and its Settings field. Normal selection copy and optional copy-on-hide remain available.

### Fixed
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

