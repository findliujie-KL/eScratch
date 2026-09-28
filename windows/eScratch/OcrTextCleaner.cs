using System;
using System.Text.RegularExpressions;

namespace eScratch;

public static class OcrTextCleaner
{
    private static readonly Regex BetweenHan = new(@"(?<=[\u3007\u3400-\u9fff\uf900-\ufaff])[ \t]+(?=[\u3007\u3400-\u9fff\uf900-\ufaff])");
    private static readonly Regex BeforeChinesePunctuation = new(@"(?<=[\u3007\u3400-\u9fff\uf900-\ufaff])[ \t]+(?=[，。！？；：、!?])");
    private static readonly Regex AfterChinesePunctuation = new(@"(?<=[，。！？；：、!?])[ \t]+(?=[\u3007\u3400-\u9fff\uf900-\ufaff])");
    private static readonly Regex SingleLineBreak = new(@"(?<=[^\r\n])[ \t]*\r?\n[ \t]*(?=[^\r\n])");
    private static readonly Regex ListStart = new(@"^(?:[-*•]|\d+[.)])\s");

    public static string Clean(string text, bool joinWrappedLines)
    {
        text = AfterChinesePunctuation.Replace(BeforeChinesePunctuation.Replace(BetweenHan.Replace(text.Trim(), ""), ""), "");
        if (!joinWrappedLines) return text;
        return SingleLineBreak.Replace(text, match =>
        {
            var before = text[match.Index - 1];
            var after = text[match.Index + match.Length];
            var nextLine = text.Substring(match.Index + match.Length).Split('\r', '\n')[0];
            if (ListStart.IsMatch(nextLine) || ":：".IndexOf(before) >= 0) return "\n";
            return (IsHan(before) || "，。！？；：、".IndexOf(before) >= 0) &&
                (IsHan(after) || "，。！？；：、".IndexOf(after) >= 0) ? "" : " ";
        });
    }

    private static bool IsHan(char c) => c == '\u3007' || c >= '\u3400' && c <= '\u9fff' || c >= '\uf900' && c <= '\ufaff';
}
