using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Rendering;

namespace eScratch;

public sealed class MarkdownEditor
{
    private readonly TextEditor editor;
    private object? version;
    private InlineMarkdown? parsed;
    private InlineStyle? pendingStyle;
    private object? pendingVersion;
    private bool applying;
    public bool Enabled { get; private set; }
    public InlineMarkdown Model
    {
        get
        {
            if (!ReferenceEquals(version, editor.Document.Version))
            { parsed = new InlineMarkdown(editor.Text); version = editor.Document.Version; }
            return parsed!;
        }
    }
    public MarkdownEditor(TextEditor editor)
    {
        this.editor = editor;
        editor.TextArea.Caret.PositionChanged += (_, _) => { if (!applying) pendingStyle = null; };
        editor.TextArea.TextView.ElementGenerators.Add(new MarkerGenerator(this));
        editor.TextArea.TextView.LineTransformers.Add(new StyleTransformer(this));
        editor.TextArea.TextEntering += (_, e) =>
        {
            if (!Enabled || editor.IsReadOnly || e.Handled) return;
            ReplaceSelection(e.Text); e.Handled = true;
        };
        editor.PreviewKeyDown += (_, e) =>
        {
            if (!Enabled || editor.IsReadOnly || e.Handled) return;
            if ((e.Key == Key.Back || e.Key == Key.Delete) && (Keyboard.Modifiers == ModifierKeys.None || Keyboard.Modifiers == ModifierKeys.Control))
            { Delete(e.Key == Key.Back, Keyboard.Modifiers == ModifierKeys.Control); e.Handled = true; }
            else if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.None)
            { ReplaceSelection(Environment.NewLine); e.Handled = true; }
            else if (e.Key == Key.Tab && Keyboard.Modifiers == ModifierKeys.None)
            { ReplaceSelection(editor.Options.ConvertTabsToSpaces ? new string(' ', editor.Options.IndentationSize) : "\t"); e.Handled = true; }
        };
    }
    public void SetEnabled(bool value)
    {
        Enabled = value;
        pendingStyle = null;
        editor.TextArea.TextView.Redraw();
    }
    public string SelectedVisibleText()
    {
        var start = Model.VisibleOffset(editor.SelectionStart);
        var end = Model.VisibleOffset(editor.SelectionStart + editor.SelectionLength);
        return Model.Visible.Substring(start, end - start);
    }
    public bool Toggle(InlineStyle style)
    {
        if (editor.IsReadOnly) return false;
        var model = Model;
        if (editor.SelectionLength == 0)
        {
            var caret = model.VisibleOffset(editor.CaretOffset);
            if (caret == 0 || (model.Styles[caret - 1] & style) == 0) return false;
            var left = caret; var right = caret;
            while (left > 0 && (model.Styles[left - 1] & style) != 0) left--;
            while (right < model.Styles.Length && (model.Styles[right] & style) != 0) right++;
            Apply(model.Toggle(left, right - left, style), caret, 0);
            return true;
        }
        var start = model.VisibleOffset(editor.SelectionStart);
        var end = model.VisibleOffset(editor.SelectionStart + editor.SelectionLength);
        if (end == start) return false;
        Apply(model.Toggle(start, end - start, style), start, end - start);
        return true;
    }
    public void ReplaceSelection(string text)
    {
        var model = Model;
        var start = model.VisibleOffset(editor.SelectionStart);
        var end = model.VisibleOffset(editor.SelectionStart + editor.SelectionLength);
        var inherit = end == start && ReferenceEquals(pendingVersion, editor.Document.Version) && pendingStyle.HasValue
            ? pendingStyle.Value : model.InsertionStyle(start, end - start);
        Apply(model.Replace(start, end - start, text, inherit), start + new InlineMarkdown(text).Visible.Length, 0);
        // Trailing whitespace cannot be wrapped by Markdown emphasis. Remember
        // the typing style until a subsequent character gives it visible content.
        if (text.Length > 0 && char.IsWhiteSpace(text[text.Length - 1]) && text[text.Length - 1] != '\n' && text[text.Length - 1] != '\r')
        { pendingStyle = inherit; pendingVersion = editor.Document.Version; }
    }
    public void ReplaceVisible(int start, int length, string text) =>
        Apply(Model.Replace(start, length, text), start + new InlineMarkdown(text).Visible.Length, 0);
    public void Delete(bool backwards, bool word = false)
    {
        var model = Model;
        var start = model.VisibleOffset(editor.SelectionStart);
        var end = model.VisibleOffset(editor.SelectionStart + editor.SelectionLength);
        if (start == end)
        {
            var boundaries = StringInfo.ParseCombiningCharacters(model.Visible);
            if (backwards)
            {
                start = boundaries.Where(p => p < start).DefaultIfEmpty(0).Last();
                if (word) while (start > 0 && !char.IsWhiteSpace(model.Visible[start - 1])) start--;
            }
            else
            {
                end = boundaries.FirstOrDefault(p => p > end);
                if (end == 0) end = model.Visible.Length;
                if (word) while (end < model.Visible.Length && !char.IsWhiteSpace(model.Visible[end])) end++;
            }
        }
        if (end > start) Apply(model.Replace(start, end - start, ""), start, 0);
    }
    private void Apply(string source, int visibleStart, int visibleLength)
    {
        pendingStyle = null;
        applying = true;
        try
        {
        var old = editor.Text;
        var prefix = 0; while (prefix < old.Length && prefix < source.Length && old[prefix] == source[prefix]) prefix++;
        var suffix = 0; while (suffix < old.Length - prefix && suffix < source.Length - prefix && old[old.Length - 1 - suffix] == source[source.Length - 1 - suffix]) suffix++;
        if (prefix != old.Length || prefix != source.Length)
            editor.Document.Replace(prefix, old.Length - prefix - suffix, source.Substring(prefix, source.Length - prefix - suffix));
        var model = Model;
        var start = model.SourceOffset(visibleStart);
        var end = model.SourceOffset(visibleStart + visibleLength);
        editor.Select(start, Math.Max(0, end - start));
        editor.CaretOffset = end;
        }
        finally { applying = false; }
    }
    private sealed class MarkerGenerator : VisualLineElementGenerator
    {
        private readonly MarkdownEditor owner;
        public MarkerGenerator(MarkdownEditor owner) => this.owner = owner;
        public override int GetFirstInterestedOffset(int startOffset) => !owner.Enabled ? -1 :
            owner.Model.Markers.Where(m => m.Offset >= startOffset).Select(m => m.Offset).DefaultIfEmpty(-1).First();
        public override VisualLineElement? ConstructElement(int offset)
        {
            var marker = owner.Model.Markers.FirstOrDefault(m => m.Offset == offset);
            return marker == null ? null : new HiddenMarker(marker.Length);
        }
    }
    private sealed class HiddenMarker : FormattedTextElement
    {
        public HiddenMarker(int length) : base("\u200b", length) { }
        public override int GetNextCaretPosition(int visualColumn, System.Windows.Documents.LogicalDirection direction, CaretPositioningMode mode) => -1;
    }
    private sealed class StyleTransformer : DocumentColorizingTransformer
    {
        private readonly MarkdownEditor owner;
        public StyleTransformer(MarkdownEditor owner) => this.owner = owner;
        protected override void ColorizeLine(DocumentLine line)
        {
            if (!owner.Enabled) return;
            var model = owner.Model;
            var start = line.Offset;
            while (start < line.EndOffset)
            {
                var pos = model.VisibleOffset(start);
                var style = pos < model.Styles.Length ? model.Styles[pos] : InlineStyle.None;
                var end = start + 1;
                while (end < line.EndOffset)
                {
                    var next = model.VisibleOffset(end);
                    if ((next < model.Styles.Length ? model.Styles[next] : InlineStyle.None) != style) break;
                    end++;
                }
                if (style != InlineStyle.None)
                    ChangeLinePart(start, end, element =>
                    {
                        var face = element.TextRunProperties.Typeface;
                        element.TextRunProperties.SetTypeface(new Typeface(face.FontFamily,
                            (style & InlineStyle.Italic) != 0 ? FontStyles.Italic : face.Style,
                            (style & InlineStyle.Bold) != 0 ? FontWeights.Bold : face.Weight, face.Stretch));
                        if ((style & InlineStyle.Strike) != 0) element.TextRunProperties.SetTextDecorations(TextDecorations.Strikethrough);
                    });
                start = end;
            }
        }
    }
}
