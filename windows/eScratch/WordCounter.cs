using System.Text;
namespace eScratch;
public static class WordCounter
{
    // Rules measured against Microsoft Word 16.0.19127 ComputeStatistics(0).
    // East Asian characters/symbols count separately; other nonempty runs count
    // once. This intentionally includes standalone punctuation and emoji, like Word.
    // Keep the WPF/Electron implementations and Word reference fixtures in sync.
    private static readonly (int Start, int End)[] IndividualRanges =
    [
        (0x1100, 0x11FF),
        (0x2010, 0x2010),
        (0x2016, 0x2016),
        (0x2025, 0x2025),
        (0x2027, 0x2027),
        (0x2032, 0x2033),
        (0x2035, 0x2037),
        (0x203B, 0x203B),
        (0x2048, 0x2049),
        (0x2103, 0x2103),
        (0x2105, 0x2105),
        (0x2109, 0x2109),
        (0x2121, 0x2121),
        (0x2160, 0x2182),
        (0x2185, 0x2189),
        (0x2190, 0x2193),
        (0x2196, 0x2199),
        (0x2208, 0x2208),
        (0x220F, 0x220F),
        (0x2211, 0x2211),
        (0x2215, 0x2215),
        (0x221A, 0x221A),
        (0x221D, 0x2220),
        (0x2223, 0x2223),
        (0x2225, 0x2225),
        (0x2227, 0x222B),
        (0x222E, 0x222E),
        (0x2234, 0x2237),
        (0x223D, 0x223D),
        (0x2248, 0x2248),
        (0x224C, 0x224C),
        (0x2252, 0x2252),
        (0x2260, 0x2261),
        (0x2264, 0x2267),
        (0x226E, 0x226F),
        (0x2295, 0x2295),
        (0x2299, 0x2299),
        (0x22A5, 0x22A5),
        (0x22BF, 0x22BF),
        (0x2312, 0x2312),
        (0x2460, 0x2469),
        (0x2474, 0x24B5),
        (0x24D0, 0x24E9),
        (0x2500, 0x254B),
        (0x2550, 0x2573),
        (0x2581, 0x258F),
        (0x2593, 0x2595),
        (0x25A0, 0x25A1),
        (0x25B2, 0x25B3),
        (0x25BC, 0x25BD),
        (0x25C6, 0x25C7),
        (0x25CB, 0x25CB),
        (0x25CE, 0x25CF),
        (0x25E2, 0x25E5),
        (0x2605, 0x2606),
        (0x2609, 0x2609),
        (0x2640, 0x2640),
        (0x2642, 0x2642),
        (0x2E80, 0x2FFF),
        (0x3001, 0xA4CF),
        (0xA960, 0xA97F),
        (0xAC00, 0xD7FF),
        (0xF900, 0xFAFF),
        (0xFE30, 0xFE6F),
        (0xFF00, 0xFFEF),
        (0x1B000, 0x1B2FF),
        (0x20000, 0x3134F),
    ];
    public static int Count(string text)
    {
        int count = 0;
        bool inRun = false, canAttachVariation = false;
        foreach (var scalar in Compatibility.CodePoints(text))
        {
            int c = scalar;
            if (c is 0x200B or 0xFEFF) continue;
            if (c is >= 0x09 and <= 0x0D or 0x20 or 0xA0 or 0x2005 or 0x2013 or 0x2014 or 0x2022 or 0x3000)
            { inRun = false; canAttachVariation = false; continue; }
            if (canAttachVariation && (c is >= 0xFE00 and <= 0xFE0F or >= 0xE0100 and <= 0xE01EF)) continue;
            if (IsIndividual(c)) { count++; inRun = false; }
            else if (!inRun) { count++; inRun = true; }
            canAttachVariation = true;
        }
        return count;
    }
    private static bool IsIndividual(int c)
    {
        // Sorted ranges permit early exit for the common Latin/Arabic case.
        foreach (var (start, end) in IndividualRanges)
        {
            if (c < start) return false;
            if (c <= end) return true;
        }
        return false;
    }
}
