using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Rendering;
using ICSharpCode.AvalonEdit.Document;
using System.Globalization;
using System.Windows.Media;

namespace eScratch;

// Separate controls keep calculated values out of the document, undo and selection.
public sealed class LiveCalculations
{
    private readonly TextEditor editor;
    private readonly Canvas overlay;
    private readonly MarkdownEditor markdown;
    private readonly Action<string> copy;
    private string previous = "";
    public LiveCalculations(TextEditor editor, Canvas overlay, MarkdownEditor markdown, Action<string> copy)
    {
        this.editor = editor; this.overlay = overlay; this.markdown = markdown; this.copy = copy;
        editor.TextArea.TextView.ElementGenerators.Add(new ResultSpace(this));
        editor.LayoutUpdated += (_, _) => Refresh();
    }
    // Reserve space only at the end of an expression, without inserting document text.
    // If it cannot fit, the result gets a continuation row instead of covering text.
    private sealed class ResultSpace : VisualLineElementGenerator
    {
        private readonly LiveCalculations owner;
        public ResultSpace(LiveCalculations owner) { this.owner = owner; }
        public override int GetFirstInterestedOffset(int startOffset)
        {
            var line = CurrentContext.VisualLine.FirstDocumentLine;
            return line.Length > 0 && line.Length <= 512 && startOffset <= line.EndOffset ? line.EndOffset : -1;
        }
        public override VisualLineElement? ConstructElement(int offset)
        {
            var line = CurrentContext.VisualLine.FirstDocumentLine;
            var text = CurrentContext.Document.GetText(line);
            if (owner.markdown.Enabled) text = new InlineMarkdown(text).Visible;
            if (!Calculator.TryLiveResult(text, out var result)) return null;
            var size = new FormattedText("  = " + result + "  ", CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                new Typeface(owner.editor.FontFamily, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal), owner.editor.FontSize, Brushes.Transparent, VisualTreeHelper.GetDpi(owner.editor).PixelsPerDip);
            return new Spacer(new string('\u2003', Math.Max(1, (int)Math.Ceiling(size.WidthIncludingTrailingWhitespace / owner.editor.FontSize))));
        }
    }
    private sealed class Spacer : FormattedTextElement
    {
        public Spacer(string text) : base(text, 0) { }
        public override int GetNextCaretPosition(int visualColumn, System.Windows.Documents.LogicalDirection direction, CaretPositioningMode mode) => -1;
    }
    private void Refresh()
    {
        var view = editor.TextArea.TextView;
        if (!view.VisualLinesValid) return;
        var origin = view.TranslatePoint(new Point(), overlay);
        var entries = new List<(string Result, double Y)>();
        foreach (var line in view.VisualLines)
        {
            var documentLine = line.FirstDocumentLine;
            if (documentLine.Length > 512) continue;
            var text = editor.Document.GetText(documentLine);
            if (markdown.Enabled) text = new InlineMarkdown(text).Visible;
            if (!Calculator.TryLiveResult(text, out var result)) continue;
            var y = origin.Y + line.VisualTop - view.VerticalOffset + line.Height - line.TextLines[line.TextLines.Count - 1].Height;
            if (y < origin.Y || y >= origin.Y + view.ActualHeight) continue;
            entries.Add((result, y));
        }
        var signature = editor.FontFamily.Source + ":" + editor.FontSize + ":" + overlay.ActualWidth + ":" + string.Join(";", entries);
        if (signature == previous) return;
        previous = signature;
        overlay.Children.Clear();
        foreach (var entry in entries)
        {
            var button = new Button { Content = "= " + entry.Result, Tag = entry.Result,
                Padding = new Thickness(3, 0, 3, 0), BorderThickness = new Thickness(0),
                FontSize = editor.FontSize, FontFamily = editor.FontFamily,
                Background = System.Windows.Media.Brushes.Transparent, Cursor = Cursors.Hand,
                Focusable = false, ToolTip = "Click to copy " + entry.Result };
            button.SetResourceReference(Control.ForegroundProperty, "Accent");
            System.Windows.Automation.AutomationProperties.SetName(button, "Copy result " + entry.Result);
            button.Click += (_, _) => copy((string)button.Tag);
            var menu = new ContextMenu();
            foreach (var format in NumericText.CopyFormats(entry.Result))
            {
                var item = new MenuItem { Header = "Copy " + format };
                item.Click += (_, _) => copy(format);
                menu.Items.Add(item);
            }
            button.ContextMenu = menu;
            button.ToolTip = "Click to copy " + entry.Result + ". Right-click for other formats.";
            Canvas.SetRight(button, 22); Canvas.SetTop(button, entry.Y);
            overlay.Children.Add(button);
        }
    }
}
