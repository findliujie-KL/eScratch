using System;
using System.Linq;
using eScratch;

internal static class InlineMarkdownChecks
{
    public static void Run(Action<bool, string> check)
    {
        var model = new InlineMarkdown("plain **bold** *italic* ~~gone~~");
        check(model.Visible == "plain bold italic gone", "Only inline formatting markers are hidden");
        check(model.Styles[6] == InlineStyle.Bold && model.Styles[11] == InlineStyle.Italic && model.Styles[18] == InlineStyle.Strike, "Inline styles map to visible characters");
        check(new InlineMarkdown("# Heading\n- List item\n1. First\n2*3*4").Visible == "# Heading\n- List item\n1. First\n2*3*4", "Headings, lists and multiplication stay literal");
        check(new InlineMarkdown("`**code**`\n```\n**code**\n```").Markers.Count == 0, "Code retains visible markings without inline formatting");
        check(new InlineMarkdown("**bold *and italic***").Visible == "bold and italic", "Nested emphasis hides its matching markers");
        check(new InlineMarkdown("***both***").Styles.All(s => s == (InlineStyle.Bold | InlineStyle.Italic)), "Combined bold and italic are supported");
        model = new InlineMarkdown("**hello**");
        check(model.Replace(2, 0, "NEW") == "**heNEWllo**", "Typing within bold preserves the formatting");
        check(model.Replace(0, 0, "before") == "before**hello**" && model.Replace(5, 0, "after") == "**helloafter**", "Typing before formatting stays plain and typing after extends it");
        check(model.Replace(0, 5, "replacement") == "**replacement**", "Replacing a whole bold word retains bold");
        check(model.Replace(0, 5, "") == "", "Deleting a complete formatted span leaves no orphan markers");
        check(new InlineMarkdown(model.Toggle(1, 3, InlineStyle.Italic)).Visible == "hello", "Applying a second format to part of a span preserves its text");
        var mixed = new InlineMarkdown(model.Toggle(1, 3, InlineStyle.Italic));
        check(mixed.Styles[0] == InlineStyle.Bold && mixed.Styles[1] == (InlineStyle.Bold | InlineStyle.Italic) && mixed.Styles[4] == InlineStyle.Bold, "Partial nested formatting retains correct ranges");
        check(model.Toggle(0, 5, InlineStyle.Bold) == "hello", "Repeating a formatting action removes it");
        check(new InlineMarkdown("**hello** world").Replace(3, 5, "X") == "**hel**Xrld", "Replacing across a formatting boundary leaves balanced markers");
        check(new InlineMarkdown("中文").Toggle(0, 2, InlineStyle.Strike) == "~~中文~~", "Formatting supports Chinese text");
        var formats = new[] { InlineStyle.Bold, InlineStyle.Italic, InlineStyle.Strike };
        var crossing = new InlineMarkdown("**abcedef**hijk");
        check(crossing.Toggle(4, 7, InlineStyle.Bold) == "**abcedefhijk**", "Bold applies uniformly to a mixed bold/plain selection");
        var crossedItalic = new InlineMarkdown(crossing.Toggle(4, 7, InlineStyle.Italic));
        check(crossedItalic.Visible == "abcedefhijk" && crossedItalic.Styles.Take(4).All(s => s == InlineStyle.Bold) &&
            crossedItalic.Styles.Skip(4).Take(3).All(s => s == (InlineStyle.Bold | InlineStyle.Italic)) &&
            crossedItalic.Styles.Skip(7).All(s => s == InlineStyle.Italic), "Italic crossing a bold boundary preserves both style ranges");
        foreach (var first in formats)
            foreach (var second in formats)
                for (var begin = 0; begin < 4; begin++)
                    for (var count = 1; count <= 4 - begin; count++)
                    {
                        var firstPass = new InlineMarkdown(new InlineMarkdown("word").Toggle(0, 4, first));
                        var secondPass = new InlineMarkdown(firstPass.Toggle(begin, count, second));
                        if (secondPass.Visible != "word") throw new Exception($"Formatting lost text: {first}/{second} {begin}/{count}: {secondPass.Source}");
                    }
        check(true, "Formatting combinations preserve visible text for all selection subranges");
        foreach (var initial in formats)
            foreach (var applied in formats)
                for (var start = 0; start < 8; start++)
                    for (var length = 1; length <= 8 - start; length++)
                    {
                        var first = new InlineMarkdown(new InlineMarkdown("abcdefgh").Toggle(0, 4, initial));
                        var actual = new InlineMarkdown(first.Toggle(start, length, applied));
                        var expected = (InlineStyle[])first.Styles.Clone();
                        var remove = expected.Skip(start).Take(length).All(s => (s & applied) != 0);
                        for (var i = start; i < start + length; i++) expected[i] = remove ? expected[i] & ~applied : expected[i] | applied;
                        if (actual.Visible != first.Visible || !actual.Styles.SequenceEqual(expected))
                            throw new Exception($"Crossing format mismatch {initial}/{applied} {start}/{length}: {actual.Source}");
                    }
        check(true, "Mixed-format selections preserve text and exact styles across all boundaries");
    }
}
