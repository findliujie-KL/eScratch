using System;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace eScratch;

public sealed class ScratchFont
{
    public string Source { get; }
    public string Name { get; }
    private readonly Lazy<string> chineseSample;
    public string ChineseSample => chineseSample.Value;
    public static bool IsTextFont(FontFamily family)
    {
        // The default is a fallback chain rather than a single physical face.
        if (family.Source == "Cascadia Code, Consolas") return true;
        try
        {
            var face = new Typeface(family, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
            if (!face.TryGetGlyphTypeface(out var glyph) || glyph.Symbol) return false;
            // Inspect actual character coverage; fallback rendering would make
            // icon fonts appear to support ordinary text. Allow all scripts.
            return glyph.CharacterToGlyphMap.Keys
                .Where(code => code >= 0 && code <= 0x10ffff && (code < 0xd800 || code > 0xdfff))
                .Select(code => char.ConvertFromUtf32(code))
                .Where(text => char.IsLetter(text, 0)).Take(16).Count() == 16;
        }
        catch (Exception) { return false; }
    }
    public ScratchFont(FontFamily family)
    {
        Source = family.Source;
        Name = family.FamilyNames.Where(n => n.Key.IetfLanguageTag.StartsWith("zh", StringComparison.OrdinalIgnoreCase))
            .OrderBy(n => n.Key.IetfLanguageTag.Equals("zh-CN", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .Select(n => n.Value).FirstOrDefault() ?? Source;
        // Inspect only fonts whose rows are displayed, and do not mistake system
        // fallback glyphs for the selected font's own Chinese character support.
        chineseSample = new Lazy<string>(() =>
        {
            if (Name.Any(c => (c >= '\u3400' && c <= '\u9fff') || (c >= '\uf900' && c <= '\ufaff'))) return "";
            try
            {
                const string sample = "中文字体";
                var face = new Typeface(family, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
                return face.TryGetGlyphTypeface(out var glyph) && sample.All(c => glyph.CharacterToGlyphMap.ContainsKey(c)) ? sample : "";
            }
            catch (Exception) { return ""; }
        });
    }
    public override string ToString() => Name;
}
