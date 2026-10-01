using System.Globalization;
using SkiaSharp;

namespace Sway.Core.Styling;

/// <summary>Environment that unit conversion depends on.</summary>
public readonly record struct StyleContext(float ViewportWidth, float ViewportHeight, float RootFontSize = 16);

public static class CssValues
{
    static readonly Dictionary<string, SKColor> Named = new(StringComparer.OrdinalIgnoreCase)
    {
        ["transparent"] = SKColors.Transparent, ["black"] = SKColors.Black, ["white"] = SKColors.White,
        ["red"] = new(255, 0, 0), ["green"] = new(0, 128, 0), ["blue"] = new(0, 0, 255),
        ["yellow"] = new(255, 255, 0), ["orange"] = new(255, 165, 0), ["purple"] = new(128, 0, 128),
        ["gray"] = new(128, 128, 128), ["grey"] = new(128, 128, 128), ["silver"] = new(192, 192, 192),
        ["lightgray"] = new(211, 211, 211), ["lightgrey"] = new(211, 211, 211), ["darkgray"] = new(169, 169, 169),
        ["navy"] = new(0, 0, 128), ["teal"] = new(0, 128, 128), ["maroon"] = new(128, 0, 0),
        ["lime"] = new(0, 255, 0), ["aqua"] = new(0, 255, 255), ["cyan"] = new(0, 255, 255),
        ["fuchsia"] = new(255, 0, 255), ["magenta"] = new(255, 0, 255), ["pink"] = new(255, 192, 203),
        ["brown"] = new(165, 42, 42), ["gold"] = new(255, 215, 0), ["indigo"] = new(75, 0, 130),
        ["violet"] = new(238, 130, 238), ["crimson"] = new(220, 20, 60), ["tomato"] = new(255, 99, 71),
        ["coral"] = new(255, 127, 80), ["salmon"] = new(250, 128, 114), ["khaki"] = new(240, 230, 140),
        ["whitesmoke"] = new(245, 245, 245), ["gainsboro"] = new(220, 220, 220), ["dimgray"] = new(105, 105, 105),
        ["slategray"] = new(112, 128, 144), ["steelblue"] = new(70, 130, 180), ["skyblue"] = new(135, 206, 235),
        ["royalblue"] = new(65, 105, 225), ["dodgerblue"] = new(30, 144, 255), ["seagreen"] = new(46, 139, 87),
        ["forestgreen"] = new(34, 139, 34), ["darkgreen"] = new(0, 100, 0), ["darkred"] = new(139, 0, 0),
        ["darkblue"] = new(0, 0, 139), ["rebeccapurple"] = new(102, 51, 153),
    };

    /// <summary>Splits on top-level whitespace, keeping function calls such as rgb(1, 2, 3) together.</summary>
    public static List<string> SplitTokens(string value)
    {
        var tokens = new List<string>();
        int depth = 0, start = -1;
        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];
            if (c == '(') depth++;
            else if (c == ')') depth--;

            if (char.IsWhiteSpace(c) && depth == 0)
            {
                if (start >= 0) { tokens.Add(value[start..i]); start = -1; }
            }
            else if (start < 0) start = i;
        }
        if (start >= 0) tokens.Add(value[start..]);
        return tokens;
    }

    public static bool TryColor(string value, SKColor currentColor, out SKColor color)
    {
        value = value.Trim();
        color = default;

        if (value.Equals("currentcolor", StringComparison.OrdinalIgnoreCase)) { color = currentColor; return true; }
        if (Named.TryGetValue(value, out color)) return true;

        if (value.StartsWith('#')) return TryHex(value[1..], out color);

        int open = value.IndexOf('(');
        if (open > 0 && value.EndsWith(')'))
        {
            string fn = value[..open].Trim().ToLowerInvariant();
            var args = value[(open + 1)..^1].Split(new[] { ',', ' ', '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (fn is "rgb" or "rgba" && args.Length >= 3)
            {
                if (!TryChannel(args[0], 255, out var r) || !TryChannel(args[1], 255, out var g) || !TryChannel(args[2], 255, out var b)) return false;
                float a = 1;
                if (args.Length >= 4 && !TryChannel(args[3], 1, out a)) return false;
                color = new SKColor((byte)r, (byte)g, (byte)b, (byte)Math.Round(Math.Clamp(a, 0, 1) * 255));
                return true;
            }
            if (fn is "hsl" or "hsla" && args.Length >= 3)
            {
                if (!float.TryParse(args[0].Replace("deg", ""), NumberStyles.Float, CultureInfo.InvariantCulture, out var h)
                    || !TryChannel(args[1], 1, out var s) || !TryChannel(args[2], 1, out var l)) return false;
                float a = 1;
                if (args.Length >= 4 && !TryChannel(args[3], 1, out a)) return false;
                var c = SKColor.FromHsl(((h % 360) + 360) % 360, Math.Clamp(s, 0, 1) * 100, Math.Clamp(l, 0, 1) * 100);
                color = c.WithAlpha((byte)Math.Round(Math.Clamp(a, 0, 1) * 255));
                return true;
            }
        }
        return false;
    }

    static bool TryHex(string hex, out SKColor color)
    {
        color = default;
        if (hex.Length is 3 or 4) hex = string.Concat(hex.Select(ch => new string(ch, 2)));
        if (hex.Length is not (6 or 8)) return false;
        if (!uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var v)) return false;
        color = hex.Length == 6
            ? new SKColor((byte)(v >> 16), (byte)(v >> 8), (byte)v)
            : new SKColor((byte)(v >> 24), (byte)(v >> 16), (byte)(v >> 8), (byte)v);
        return true;
    }

    // Parses a number or percentage into [0, scale]; percentages map 100% to scale.
    static bool TryChannel(string text, float scale, out float value)
    {
        bool percent = text.EndsWith('%');
        bool ok = float.TryParse(percent ? text[..^1] : text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        if (ok && percent) value = value / 100f * scale;
        return ok;
    }

    public static bool TryLength(string value, float fontSize, in StyleContext ctx, out Length length)
    {
        value = value.Trim();
        length = default;

        if (value.Equals("auto", StringComparison.OrdinalIgnoreCase)) { length = Length.Auto; return true; }
        if (value.StartsWith("calc(", StringComparison.OrdinalIgnoreCase))
        {
            var calc = new CalcParser(value[5..^1], fontSize, ctx);
            if (!calc.TryEvaluate(out var px)) return false;
            length = Length.Px(px);
            return true;
        }

        if (!TrySplitUnit(value, out var number, out var unit)) return false;
        switch (unit)
        {
            case "" when number == 0: length = Length.Zero; return true;
            case "px": length = Length.Px(number); return true;
            case "%": length = new Length(number, LengthUnit.Percent); return true;
            case "em": length = Length.Px(number * fontSize); return true;
            case "rem": length = Length.Px(number * ctx.RootFontSize); return true;
            case "pt": length = Length.Px(number * 96f / 72f); return true;
            case "vw": length = Length.Px(number / 100f * ctx.ViewportWidth); return true;
            case "vh": length = Length.Px(number / 100f * ctx.ViewportHeight); return true;
            default: return false;
        }
    }

    public static bool TrySplitUnit(string value, out float number, out string unit)
    {
        int i = 0;
        while (i < value.Length && (char.IsDigit(value[i]) || value[i] is '.' or '-' or '+')) i++;
        unit = value[i..].ToLowerInvariant();
        return float.TryParse(value[..i], NumberStyles.Float, CultureInfo.InvariantCulture, out number);
    }

    /// <summary>
    /// Substitutes var() references. Returns null when a reference has no value and no fallback,
    /// which makes the declaration invalid at computed-value time.
    /// </summary>
    public static string? ResolveVars(string value, IReadOnlyDictionary<string, string> custom, int depth = 0)
    {
        if (depth > 16) return null;

        int at;
        while ((at = value.IndexOf("var(", StringComparison.Ordinal)) >= 0)
        {
            int open = at + 3, level = 0, close = -1;
            for (int i = open; i < value.Length; i++)
            {
                if (value[i] == '(') level++;
                else if (value[i] == ')' && --level == 0) { close = i; break; }
            }
            if (close < 0) return null;

            string inner = value[(open + 1)..close];
            int comma = CssParser.TopLevelSplits(inner, ',').FirstOrDefault(-1);
            string name = (comma < 0 ? inner : inner[..comma]).Trim();
            string? fallback = comma < 0 ? null : inner[(comma + 1)..].Trim();

            string? replacement = null;
            if (custom.TryGetValue(name, out var raw)) replacement = ResolveVars(raw, custom, depth + 1);
            replacement ??= fallback is null ? null : ResolveVars(fallback, custom, depth + 1);
            if (replacement is null) return null;

            value = value[..at] + replacement + value[(close + 1)..];
        }
        return value;
    }

    /// <summary>calc() over absolute lengths (px, em, rem, vw, vh, pt) and plain numbers. Percentages are not supported.</summary>
    sealed class CalcParser
    {
        readonly string _s;
        readonly float _fontSize;
        readonly StyleContext _ctx;
        int _i;

        public CalcParser(string s, float fontSize, in StyleContext ctx) { _s = s; _fontSize = fontSize; _ctx = ctx; }

        public bool TryEvaluate(out float px)
        {
            px = 0;
            try
            {
                var (v, hasUnit) = Sum();
                SkipSpace();
                if (_i != _s.Length || !hasUnit) return false;
                px = v;
                return true;
            }
            catch (FormatException) { return false; }
        }

        (float v, bool unit) Sum()
        {
            var (left, lu) = Product();
            while (true)
            {
                SkipSpace();
                if (_i >= _s.Length || _s[_i] is not ('+' or '-')) return (left, lu);
                char op = _s[_i++];
                var (right, ru) = Product();
                if (lu != ru) throw new FormatException();
                left = op == '+' ? left + right : left - right;
            }
        }

        (float v, bool unit) Product()
        {
            var (left, lu) = Atom();
            while (true)
            {
                SkipSpace();
                if (_i >= _s.Length || _s[_i] is not ('*' or '/')) return (left, lu);
                char op = _s[_i++];
                var (right, ru) = Atom();
                if (op == '*') { if (lu && ru) throw new FormatException(); left *= right; lu |= ru; }
                else { if (ru) throw new FormatException(); left /= right; }
            }
        }

        (float v, bool unit) Atom()
        {
            SkipSpace();
            if (_i < _s.Length && _s[_i] == '(')
            {
                _i++;
                var inner = Sum();
                SkipSpace();
                if (_i >= _s.Length || _s[_i] != ')') throw new FormatException();
                _i++;
                return inner;
            }

            int start = _i;
            while (_i < _s.Length && (char.IsLetterOrDigit(_s[_i]) || _s[_i] is '.' or '%' || (_s[_i] is '-' or '+' && _i == start))) _i++;
            string token = _s[start.._i];
            if (token.Length == 0 || token.EndsWith('%')) throw new FormatException();

            if (!TrySplitUnit(token, out var number, out var unit)) throw new FormatException();
            if (unit.Length == 0) return (number, false);
            if (!TryLength(token, _fontSize, _ctx, out var len) || len.Unit != LengthUnit.Px) throw new FormatException();
            return (len.Value, true);
        }

        void SkipSpace() { while (_i < _s.Length && char.IsWhiteSpace(_s[_i])) _i++; }
    }
}
