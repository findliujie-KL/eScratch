using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace eScratch;

[Flags]
public enum InlineStyle { None = 0, Bold = 1, Italic = 2, Strike = 4 }

// A deliberately small inline dialect. Headings, lists, links and code stay text.
public sealed class InlineMarkdown
{
    public sealed record Marker(int Offset, int Length);
    public string Source { get; }
    public string Visible { get; }
    public List<Marker> Markers { get; } = new();
    public InlineStyle[] Styles { get; }
    private readonly int[] sourcePositions;

    public InlineMarkdown(string source)
    {
        Source = source;
        var hidden = new bool[source.Length];
        var sourceStyles = new InlineStyle[source.Length];
        var lineStart = 0;
        var fenced = false;
        while (lineStart < source.Length)
        {
            var end = source.IndexOf('\n', lineStart); if (end < 0) end = source.Length;
            var line = source.Substring(lineStart, end - lineStart).TrimStart();
            if (line.StartsWith("```") || line.StartsWith("~~~")) fenced = !fenced;
            else if (!fenced) ParseRange(lineStart, end, InlineStyle.None, hidden, sourceStyles, 0);
            lineStart = end + 1;
        }
        var visible = new StringBuilder(); var positions = new List<int>(); var styles = new List<InlineStyle>();
        for (var i = 0; i < source.Length; i++)
            if (!hidden[i]) { visible.Append(source[i]); positions.Add(i); styles.Add(sourceStyles[i]); }
        Visible = visible.ToString(); sourcePositions = positions.ToArray(); Styles = styles.ToArray();
        Markers.Sort((a, b) => a.Offset.CompareTo(b.Offset));
    }

    private void ParseRange(int start, int end, InlineStyle inherited, bool[] hidden, InlineStyle[] styles, int depth)
    {
        for (int i = start; i < end; i++) styles[i] |= inherited;
        if (depth > 16) return;
        for (var open = start; open < end; open++)
        {
            var ch = Source[open];
            if (ch == (char)92) { open++; continue; }
            if (ch == '`')
            {
                var run = 1; while (open + run < end && Source[open + run] == ch) run++;
                var closing = Source.IndexOf(new string(ch, run), open + run, end - open - run, StringComparison.Ordinal);
                if (closing >= 0) open = closing + run - 1;
                continue;
            }
            if (ch != '*' && ch != '_' && ch != '~') continue;
            var available = 1; while (open + available < end && Source[open + available] == ch) available++;
            var matched = false;
            foreach (var count in ch == '~' ? new[] { 2 } : new[] { 3, 2, 1 })
            {
                if (available < count || (ch == '~' && available != 2)) continue;
                var contentStart = open + count;
                if (contentStart >= end || char.IsWhiteSpace(Source[contentStart])) continue;
                if (ch == '_' && open > start && char.IsLetterOrDigit(Source[open - 1])) continue;
                if (ch == '*' && count == 1 && open > start && char.IsDigit(Source[open - 1]) && char.IsDigit(Source[contentStart])) continue;
                for (var close = contentStart + 1; close < end; close++)
                {
                    if (Source[close] == (char)92) { close++; continue; }
                    if (Source[close] != ch) continue;
                    var run = 1; while (close + run < end && Source[close + run] == ch) run++;
                    if (run < count) { close += run - 1; continue; }
                    // Odd closing runs may close an inner italic span before bold.
                    var innerItalic = 0;
                    if (count == 2 && run >= 3)
                        for (var p = contentStart; p < close; p++)
                        {
                            if (Source[p] == (char)92) { p++; continue; }
                            if (Source[p] != ch) continue;
                            var innerRun = 1;
                            while (p + innerRun < close && Source[p + innerRun] == ch) innerRun++;
                            innerItalic += innerRun % 2; p += innerRun - 1;
                        }
                    var actualClose = close + (count == 2 && run >= 3 && innerItalic % 2 == 1 ? 1 : 0);
                    if (char.IsWhiteSpace(Source[actualClose - 1])) { close += run - 1; continue; }
                    if (ch == '_' && actualClose + count < end && char.IsLetterOrDigit(Source[actualClose + count])) { close += run - 1; continue; }
                    var style = ch == '~' ? InlineStyle.Strike : count == 3 ? InlineStyle.Bold | InlineStyle.Italic : count == 2 ? InlineStyle.Bold : InlineStyle.Italic;
                    for (var j = 0; j < count; j++) { hidden[open + j] = true; hidden[actualClose + j] = true; }
                    Markers.Add(new Marker(open, count)); Markers.Add(new Marker(actualClose, count));
                    ParseRange(contentStart, actualClose, inherited | style, hidden, styles, depth + 1);
                    open = actualClose + count - 1; matched = true; break;
                }
                if (matched) break;
            }
            if (!matched) open += available - 1;
        }
    }
    public int VisibleOffset(int sourceOffset)
    {
        var found = Array.BinarySearch(sourcePositions, sourceOffset);
        return found >= 0 ? found : ~found;
    }
    // There is no hidden caret stop; insertion style is resolved from visible text.
    public int SourceOffset(int visibleOffset)
    {
        visibleOffset = Math.Max(0, Math.Min(Visible.Length, visibleOffset));
        if (visibleOffset == 0) return 0;
        if (visibleOffset == Visible.Length) return Source.Length;
        var left = sourcePositions[visibleOffset - 1] + 1;
        var right = sourcePositions[visibleOffset];
        if (Styles[visibleOffset - 1] == InlineStyle.None) return left;
        return right;
    }
    public InlineStyle InsertionStyle(int start, int length)
    {
        var inherit = InlineStyle.None;
        if (length > 0)
        {
            inherit = Styles[start];
            for (var i = start + 1; i < start + length; i++) inherit &= Styles[i];
        }
        else if (start > 0) inherit = Styles[start - 1];
        return inherit;
    }
    public string Replace(int start, int length, string inserted, InlineStyle? insertionStyle = null)
    {
        var inherit = insertionStyle ?? InsertionStyle(start, length);
        var text = Visible.Remove(start, length).Insert(start, inserted);
        var styles = Styles.Take(start).Concat(Enumerable.Repeat(inherit, inserted.Length)).Concat(Styles.Skip(start + length)).ToArray();
        if (insertionStyle.HasValue && length == 0 && inherit != InlineStyle.None)
            for (var i = start - 1; i >= 0 && char.IsWhiteSpace(text[i]) && text[i] != '\r' && text[i] != '\n'; i--)
                styles[i] = inherit;
        return Serialize(text, styles);
    }
    public string Toggle(int start, int length, InlineStyle style)
    {
        var styles = (InlineStyle[])Styles.Clone();
        var significant = Enumerable.Range(start, length).Where(i => !char.IsWhiteSpace(Visible[i])).ToArray();
        var remove = significant.Length > 0 && significant.All(i => (styles[i] & style) == style);
        for (var i = start; i < start + length; i++)
            if (!char.IsWhiteSpace(Visible[i]) || (i > start && i < start + length - 1))
                styles[i] = remove ? styles[i] & ~style : styles[i] | style;
        return Serialize(Visible, styles);
    }
    private static string Serialize(string text, InlineStyle[] styles)
    {
        var result = new StringBuilder();
        var active = new List<InlineStyle>();
        var order = new[] { InlineStyle.Strike, InlineStyle.Bold, InlineStyle.Italic };
        for (int i = 0; i <= text.Length; i++)
        {
            var style = i == text.Length || text[i] == '\r' || text[i] == '\n' ? InlineStyle.None : styles[i];
            // Markdown delimiters cannot surround only edge whitespace.
            if (i < text.Length && char.IsWhiteSpace(text[i]))
            {
                var next = i; while (next < text.Length && char.IsWhiteSpace(text[next]) && text[next] != '\r' && text[next] != '\n') next++;
                style &= next < styles.Length ? styles[next] : InlineStyle.None;
                style &= i > 0 ? styles[i - 1] : InlineStyle.None;
            }
            var desired = order.Where(s => (style & s) != 0).ToArray();
            var common = 0; while (common < active.Count && common < desired.Length && active[common] == desired[common]) common++;
            for (var j = active.Count - 1; j >= common; j--) result.Append(Delimiter(active[j]));
            active.RemoveRange(common, active.Count - common);
            for (var j = common; j < desired.Length; j++) { result.Append(Delimiter(desired[j])); active.Add(desired[j]); }
            if (i < text.Length) result.Append(text[i]);
        }
        return result.ToString();
    }
    private static string Delimiter(InlineStyle style) => style == InlineStyle.Bold ? "**" : style == InlineStyle.Italic ? "*" : "~~";
}
