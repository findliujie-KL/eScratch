# eScratch editor dependency

Vendored AvalonEdit v6.3.1, upstream commit 862415d51eddc9eac93f462dbc522ffbf929cd52.
Source: https://github.com/icsharpcode/AvalonEdit/tree/v6.3.1
License: MIT; upstream LICENSE and source notices retained.

Local changes:
- Build only net48; omit SourceLink build dependency.
- VisualLineTextParagraphProperties carries textAlignment instead of hardcoding Left.
- TextView supplies the inherited WPF TextBlock.TextAlignment attached property to paragraph formatting.

The application explicitly redraws the TextView when alignment changes. WPF's
text formatter handles wrapping, selection geometry, and caret positioning.
