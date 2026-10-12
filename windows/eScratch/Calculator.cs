using System;
using System.Globalization;

namespace eScratch;

public static class Calculator
{
    public static bool TryLiveResult(string line, out string result)
    {
        result = "";
        if (line.Length > 512 || line.IndexOf('=') >= 0) return false;
        var expression = line.Trim();
        // A standalone number is a note, not a calculation. Require an operator after it.
        if (expression.Length == 0 || expression.Substring(1).IndexOfAny(new[] { '+', '-', '−', '*', '×', '/', '÷', '%' }) < 0) return false;
        if (!TryCompleteLine(expression, out var completed)) return false;
        result = completed.Substring(completed.LastIndexOf(" = ", StringComparison.Ordinal) + 3);
        return true;
    }
    public static bool TryCompleteLine(string line, out string completed)
    {
        completed = line;
        var expression = line.TrimEnd();
        var marker = expression.LastIndexOf(" = ", StringComparison.Ordinal);
        var existingPlaces = -1;
        var previousScale = 0d;
        var previousNumber = 0d;
        if (marker >= 0)
        {
            var previous = expression.Substring(marker + 3);
            if (NumericText.TryRead(previous, 0, out var oldResult) && oldResult.End == previous.Length)
            {
                var decimalPoint = oldResult.Number.IndexOf('.');
                var exponent = oldResult.Number.IndexOfAny(new[] { 'e', 'E' });
                existingPlaces = decimalPoint < 0 ? 0 : (exponent < 0 ? oldResult.Number.Length : exponent) - decimalPoint - 1;
                previousScale = oldResult.Percent ? 0.01 : oldResult.UnitScale;
                previousNumber = double.Parse(oldResult.Number, CultureInfo.InvariantCulture);
                expression = expression.Substring(0, marker);
            }
        }
        if (expression.Length == 0 || expression.Length > 512) return false;
        try
        {
            var parser = new Parser(expression);
            var value = parser.Parse();
            if (double.IsNaN(value) || double.IsInfinity(value)) return false;
            var scale = previousScale > 0 ? previousScale : ResultScale(value, parser.PreferredUnitScale);
            value /= scale;
            var places = 2;
            if (existingPlaces >= 0)
            {
                places = Math.Min(14, existingPlaces + 2);
                while (places <= 14 && Math.Round(value, places, MidpointRounding.AwayFromZero) == previousNumber) places += 2;
                if (places > 14) return true;
            }
            var rounded = Math.Round(value, places, MidpointRounding.AwayFromZero);
            var result = existingPlaces < 0
                ? (rounded == 0 ? "0" : rounded.ToString("0.##", CultureInfo.InvariantCulture))
                : rounded.ToString("F" + places, CultureInfo.InvariantCulture).TrimEnd('0').TrimEnd('.');
            completed = expression + " = " + result + (scale == 0.01 ? "%" : Suffix(scale));
            return true;
        }
        catch (FormatException) { return false; }
    }

    public static bool TryCycleDisplayUnit(string line, out string completed)
    {
        completed = line;
        var text = line.TrimEnd();
        var marker = text.LastIndexOf(" = ", StringComparison.Ordinal);
        var expression = marker < 0 ? text : text.Substring(0, marker);
        if (expression.Length == 0 || expression.Length > 512) return false;
        NumericText.Token previous = default;
        if (marker >= 0)
        {
            var result = text.Substring(marker + 3);
            if (!NumericText.TryRead(result, 0, out previous) || previous.End != result.Length) return false;
        }
        try
        {
            var value = new Parser(expression).Parse();
            if (double.IsNaN(value) || double.IsInfinity(value)) return false;
            var absolute = Math.Abs(value);
            var scales = absolute < 100 ? new[] { 1d, 0.01d } : new[] { 1d, 1_000d, 1_000_000d, 1_000_000_000d, 1_000_000_000_000d };
            var eligible = new System.Collections.Generic.List<double>();
            foreach (var scale in scales)
                if (scale == 1 || (absolute >= 100 ? absolute / scale >= 0.01 : true)) eligible.Add(scale);
            var current = marker < 0 ? 0 : previous.Percent ? eligible.IndexOf(0.01d) : eligible.IndexOf(previous.UnitScale);
            var next = eligible[(current + 1 + eligible.Count) % eligible.Count];
            var converted = value / next;
            var shown = next != 1
                ? Math.Round(converted, next == 0.01 ? 2 : 4, MidpointRounding.AwayFromZero)
                    .ToString(next == 0.01 ? "0.##" : "0.####", CultureInfo.InvariantCulture)
                : absolute < 100
                    ? Math.Round(converted, 2, MidpointRounding.AwayFromZero).ToString("0.##", CultureInfo.InvariantCulture)
                    : FormatCycleValue(converted, true);
            completed = expression + " = " + shown + (next == 0.01 ? "%" : Suffix(next));
            return true;
        }
        catch (FormatException) { return false; }
    }

    private static string FormatCycleValue(double value, bool group)
    {
        // Round off binary floating-point noise, while retaining the digits needed to preserve the value.
        var precise = value.ToString("G15", CultureInfo.InvariantCulture);
        if (decimal.TryParse(precise, NumberStyles.Float, CultureInfo.InvariantCulture, out var exact))
            return exact.ToString(group ? "#,##0.##############" : "0.##############", CultureInfo.InvariantCulture);
        return precise;
    }

    private static double ResultScale(double value, double preferred)
    {
        if (preferred < 1_000 || value == 0) return 1;
        var absolute = Math.Abs(value);
        var automatic = absolute >= 1_000_000_000_000 ? 1_000_000_000_000d
            : absolute >= 1_000_000_000 ? 1_000_000_000d
            : absolute >= 1_000_000 ? 1_000_000d
            : absolute >= 1_000 ? 1_000d : 1d;
        return preferred > automatic && absolute >= preferred / 10 ? preferred : automatic;
    }
    private static string Suffix(double scale) => scale == 1_000_000_000_000 ? "T"
        : scale == 1_000_000_000 ? "B" : scale == 1_000_000 ? "M" : scale == 1_000 ? "K" : "";

    private sealed class Parser
    {
        private readonly string text;
        private int position, depth;
        public double PreferredUnitScale { get; private set; }
        public Parser(string text) { this.text = text; }
        public double Parse()
        {
            var value = Expression();
            SkipSpace();
            if (position != text.Length) throw new FormatException();
            return value;
        }
        private void SkipSpace() { while (position < text.Length && char.IsWhiteSpace(text[position])) position++; }
        private bool Take(char value)
        {
            SkipSpace();
            if (position >= text.Length || text[position] != value) return false;
            position++;
            return true;
        }
        private double Expression()
        {
            var value = Term();
            while (true)
            {
                if (Take('+')) value += Term();
                else if (Take('-') || Take('−')) value -= Term();
                else return value;
            }
        }
        private double Term()
        {
            var value = Unary();
            while (true)
            {
                if (Take('*') || Take('×')) value *= Unary();
                else if (Take('/') || Take('÷')) value /= Unary();
                else return value;
            }
        }
        private double Unary()
        {
            if (++depth > 32) throw new FormatException();
            try
            {
                if (Take('+')) return Unary();
                if (Take('-') || Take('−')) return -Unary();
                if (Take('('))
                {
                    var value = Expression();
                    if (!Take(')')) throw new FormatException();
                    return value;
                }
                return Number();
            }
            finally { depth--; }
        }
        private double Number()
        {
            SkipSpace();
            if (!NumericText.TryRead(text, position, out var token)) throw new FormatException();
            position = token.End;
            if (token.UnitScale >= 1_000) PreferredUnitScale = Math.Max(PreferredUnitScale, token.UnitScale);
            return token.Value;
        }
    }
}
