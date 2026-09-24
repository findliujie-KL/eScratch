using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using HtmlAgilityPack;

namespace eScratch;

// Clipboard HTML is parsed as data, never loaded into a browser or executed.
public static class MarkdownPaste
{
    private sealed record Part(string Text, string Markdown);
    private static string Escape(string text) => Regex.Replace(text, @"([\\`*_{}\[\]<>#~|])", @"\$1");
    private static string Normalize(string text) => Regex.Replace(text, @"\s+", " ").Trim();

    public static string Convert(string plain, string html, bool hasRtf)
    {
        if (string.IsNullOrWhiteSpace(html)) return plain;
        if (html.Length > 2_000_000 || plain.Length > 2_000_000) return plain;
        var fragment = Regex.Match(html, @"(?s)<!--StartFragment-->(.*?)<!--EndFragment-->");
        var doc = new HtmlDocument();
        doc.LoadHtml(fragment.Success ? fragment.Groups[1].Value : html);
        var root = doc.DocumentNode.SelectSingleNode("//body") ?? doc.DocumentNode;
        var parts = new List<Part>();
        void Mark(string value) => parts.Add(new("", value));
        void Text(string value) { foreach (char c in value) parts.Add(new(c.ToString(), Escape(c.ToString()))); }
        void Visit(HtmlNode node, int depth)
        {
            if (depth > 100) return;
            var name = node.Name.ToLowerInvariant();
            if (name is "script" or "style" or "head" or "iframe" or "object" || node.NodeType == HtmlNodeType.Comment) return;
            if (node.NodeType == HtmlNodeType.Text) { Text(Regex.Replace(HtmlEntity.DeEntitize(node.InnerText), @"\s+", " ")); return; }
            if (name == "br") { Text("\n"); return; }
            var style = node.GetAttributeValue("style", "").ToLowerInvariant();
            if (Regex.IsMatch(style, @"(?:display\s*:\s*none|visibility\s*:\s*hidden)")) return;
            bool bold = name is "b" or "strong" || Regex.IsMatch(style, @"font-weight\s*:\s*(bold|[7-9]00)");
            bool italic = name is "i" or "em" || Regex.IsMatch(style, @"font-style\s*:\s*italic");
            bool strike = name is "s" or "strike" or "del" || style.Contains("line-through");
            bool block = name is "p" or "div" or "li" or "tr" or "blockquote" || Regex.IsMatch(name, "^h[1-6]$");
            if (block && parts.Count > 0) Text("\n");
            if (Regex.IsMatch(name, "^h[1-6]$")) Mark(new string('#', name[1] - '0') + " ");
            if (name == "li") Mark(node.ParentNode.Name == "ol" ? "1. " : "- ");
            if (name == "blockquote") Mark("> ");
            var marker = (bold ? "**" : "") + (italic ? "*" : "") + (strike ? "~~" : "");
            Mark(marker);
            var href = HtmlEntity.DeEntitize(node.GetAttributeValue("href", ""));
            bool link = name == "a" && Uri.TryCreate(href, UriKind.Absolute, out var uri) && uri.Scheme is "https" or "http" or "mailto";
            if (link) Mark("[");
            foreach (var child in node.ChildNodes) Visit(child, depth + 1);
            if (link) Mark("](" + href.Replace(" ", "%20").Replace("(", "%28").Replace(")", "%29") + ")");
            Mark((strike ? "~~" : "") + (italic ? "*" : "") + (bold ? "**" : ""));
            if (name is "td" or "th") Text("\t");
            if (block) Text("\n");
        }
        Visit(root, 0);
        // Normalize whitespace only for comparison; retain the Markdown layout.
        var final = new StringBuilder(); var map = new List<int>();
        for (int i = 0; i < parts.Count; i++)
            foreach (char c in parts[i].Text)
            {
                if (char.IsWhiteSpace(c)) { if (final.Length == 0 || final[^1] == ' ') continue; final.Append(' '); }
                else final.Append(c);
                map.Add(i);
            }
        if (final.Length > 0 && final[^1] == ' ') { final.Length--; map.RemoveAt(map.Count - 1); }
        var inserts = hasRtf ? RecoverDeletions(Normalize(plain), final.ToString()) : new Dictionary<int, string>();
        var before = new Dictionary<int, string>();
        foreach (var pair in inserts) before[pair.Key < map.Count ? map[pair.Key] : map.Count > 0 ? map[^1] + 1 : parts.Count] = pair.Value;
        var result = new StringBuilder();
        for (int i = 0; i <= parts.Count; i++)
        {
            if (before.TryGetValue(i, out var deleted)) result.Append(Regex.Match(deleted, @"^\s*").Value + "~~" + Escape(deleted.Trim()) + "~~" + Regex.Match(deleted, @"\s*$").Value);
            if (i < parts.Count) result.Append(parts[i].Markdown);
        }
        return Regex.Replace(result.ToString(), @"\n[ \t]*\n(?:[ \t]*\n)+", "\n\n").Trim();
    }

    public static Dictionary<int, string> RecoverDeletions(string original, string final)
    {
        var result = new Dictionary<int, string>();
        if (final.Length == 0 || original.Length <= final.Length) return result;
        // Only accept a unique deletion-only alignment. A forward/backward scan
        // avoids quadratic diff costs and rejects ambiguous repeated text.
        var positions = new int[final.Length]; int cursor = 0;
        for (int i = 0; i < final.Length; i++)
        {
            cursor = original.IndexOf(final[i], cursor);
            if (cursor < 0) return result;
            positions[i] = cursor++;
        }
        cursor = original.Length - 1;
        for (int i = final.Length - 1; i >= 0; i--)
        {
            cursor = original.LastIndexOf(final[i], cursor);
            if (cursor != positions[i]) return result;
            cursor--;
        }
        cursor = 0;
        for (int i = 0; i <= final.Length; i++)
        {
            int end = i == final.Length ? original.Length : positions[i];
            if (end > cursor)
            {
                var deleted = original.Substring(cursor, end - cursor);
                if (!string.IsNullOrWhiteSpace(deleted)) result[i] = deleted;
            }
            cursor = end + 1;
        }
        return result;
    }
}
