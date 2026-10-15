using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace eScratch;

public static class NumericText
{
    public static System.Collections.Generic.List<string> CopyFormats(string displayed)
    {
        var formats = new System.Collections.Generic.List<string> { displayed };
        if (!TrySelection(displayed, out var value, out var token)) return formats;
        if (token.Percent) value /= 100;
        void Add(string text) { if (!formats.Contains(text)) formats.Add(text); }
        const string digits = "0.############################";
        Add(value.ToString("#,##0.############################", CultureInfo.InvariantCulture));
        Add(value.ToString(digits, CultureInfo.InvariantCulture));
        foreach (var unit in new[] { (1000m, "K"), (1000000m, "M"), (1000000000m, "B"), (1000000000000m, "T") })
        {
            var scaled = value / unit.Item1;
            if (Math.Abs(scaled) >= 0.01m) Add(scaled.ToString(digits, CultureInfo.InvariantCulture) + unit.Item2);
        }
        if (token.Percent || Math.Abs(value) < 100)
        {
            var percent = (value * 100).ToString(digits, CultureInfo.InvariantCulture);
            Add(percent + "%"); Add(percent + " pct");
        }
        return formats;
    }
    public readonly record struct Token(int End, double Value, double UnitScale, string Number, bool Percent);

    private static readonly Regex NumberPattern = new(
        @"\G(?<number>(?:\d{1,3}(?:,\d{3})+|\d+)(?:\.\d*)?|\.\d+)(?:[eE][+-]?\d+)?(?<unit>\s*(?:hundreds?|thousands?|millions?|billions?|trillions?|[HKMBT])(?![A-Za-z0-9]))?(?<percent>\s*%)?",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static bool TryRead(string text, int start, out Token token)
    {
        token = default;
        var match = NumberPattern.Match(text, start);
        if (!match.Success || match.Index != start) return false;
        var raw = match.Groups["number"].Value.Replace(",", "");
        if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)) return false;
        var scale = Scale(match.Groups["unit"].Value.Trim());
        value *= scale;
        var percent = match.Groups["percent"].Success;
        if (percent) value /= 100;
        if (double.IsNaN(value) || double.IsInfinity(value)) return false;
        token = new Token(match.Index + match.Length, value, scale, raw, percent);
        return true;
    }

    private static double Scale(string unit)
    {
        switch (unit.ToLowerInvariant())
        {
            case "h": case "hundred": case "hundreds": return 100;
            case "k": case "thousand": case "thousands": return 1_000;
            case "m": case "million": case "millions": return 1_000_000;
            case "b": case "billion": case "billions": return 1_000_000_000;
            case "t": case "trillion": case "trillions": return 1_000_000_000_000;
            default: return 1;
        }
    }

    private static bool TrySelection(string selected, out decimal expanded, out Token token)
    {
        expanded = 0; token = default;
        var trimmed = selected.Trim();
        var sign = 1;
        var start = 0;
        if (trimmed.StartsWith("-", StringComparison.Ordinal) || trimmed.StartsWith("−", StringComparison.Ordinal)) { sign = -1; start = 1; }
        else if (trimmed.StartsWith("+", StringComparison.Ordinal)) start = 1;
        if (!TryRead(trimmed, start, out token) || token.End != trimmed.Length) return false;
        if (!decimal.TryParse(token.Number, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)) return false;
        try { expanded = value * (decimal)token.UnitScale * sign; return true; }
        catch (OverflowException) { return false; }
    }

    public static bool TryAddSeparators(string selected, out string formatted)
    {
        formatted = selected;
        if (!TrySelection(selected, out var expanded, out var token)) return false;
        var decimalPlaces = token.UnitScale == 1 && !token.Number.Contains("e") && !token.Number.Contains("E")
            ? Math.Max(0, token.Number.Length - token.Number.IndexOf('.') - 1) : -1;
        if (!token.Number.Contains(".")) decimalPlaces = 0;
        formatted = expanded.ToString(decimalPlaces >= 0 ? "N" + decimalPlaces : "#,##0.############################", CultureInfo.InvariantCulture)
            + (token.Percent ? "%" : "");
        return true;
    }

    public static bool TryToWords(string selected, out string words)
    {
        words = selected;
        if (!TrySelection(selected, out var expanded, out var token)) return false;
        if (expanded < -999_999_999_999_999m || expanded > 999_999_999_999_999m) return false;
        var absolute = Math.Abs(expanded);
        var whole = (long)decimal.Truncate(absolute);
        var fraction = token.UnitScale == 1 && !token.Number.Contains("e") && !token.Number.Contains("E") && token.Number.Contains(".")
            ? token.Number.Substring(token.Number.IndexOf('.') + 1)
            : absolute.ToString("0.############################", CultureInfo.InvariantCulture).Split('.').Length > 1
                ? absolute.ToString("0.############################", CultureInfo.InvariantCulture).Split('.')[1] : "";
        words = (expanded < 0 ? "minus " : "") + WholeWords(whole);
        if (fraction.Length > 0)
        {
            words += " point";
            foreach (var digit in fraction) words += " " + Ones[digit - '0'];
        }
        if (token.Percent) words += " percent";
        return true;
    }

    private static readonly string[] Ones = { "zero", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine", "ten", "eleven", "twelve", "thirteen", "fourteen", "fifteen", "sixteen", "seventeen", "eighteen", "nineteen" };
    private static readonly string[] Tens = { "", "", "twenty", "thirty", "forty", "fifty", "sixty", "seventy", "eighty", "ninety" };
    private static readonly string[] Groups = { "", "thousand", "million", "billion", "trillion" };
    private static string WholeWords(long value)
    {
        if (value == 0) return "zero";
        var result = "";
        for (var group = 0; value > 0; group++, value /= 1000)
        {
            var part = (int)(value % 1000);
            if (part == 0) continue;
            var phrase = UnderThousand(part) + (group == 0 ? "" : " " + Groups[group]);
            result = result.Length == 0 ? phrase : phrase + " " + result;
        }
        return result;
    }
    private static string UnderThousand(int value)
    {
        var result = value >= 100 ? Ones[value / 100] + " hundred" : "";
        value %= 100;
        if (value == 0) return result;
        if (result.Length > 0) result += " ";
        if (value < 20) return result + Ones[value];
        return result + Tens[value / 10] + (value % 10 == 0 ? "" : "-" + Ones[value % 10]);
    }
}
