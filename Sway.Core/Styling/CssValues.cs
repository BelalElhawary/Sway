using System.Globalization;
using SkiaSharp;

namespace Sway.Core.Styling;

/// <summary>Environment that unit conversion depends on.</summary>
public readonly record struct StyleContext(float ViewportWidth, float ViewportHeight, float RootFontSize = 16);

public static class CssValues
{
    // The full CSS Color Module Level 4 named-color keyword list (plus "transparent", handled by TryColor directly).
    static readonly Dictionary<string, SKColor> Named = new(StringComparer.OrdinalIgnoreCase)
    {
        ["aliceblue"] = new(0xF0, 0xF8, 0xFF), ["antiquewhite"] = new(0xFA, 0xEB, 0xD7), ["aqua"] = new(0x00, 0xFF, 0xFF),
        ["aquamarine"] = new(0x7F, 0xFF, 0xD4), ["azure"] = new(0xF0, 0xFF, 0xFF), ["beige"] = new(0xF5, 0xF5, 0xDC),
        ["bisque"] = new(0xFF, 0xE4, 0xC4), ["black"] = new(0x00, 0x00, 0x00), ["blanchedalmond"] = new(0xFF, 0xEB, 0xCD),
        ["blue"] = new(0x00, 0x00, 0xFF), ["blueviolet"] = new(0x8A, 0x2B, 0xE2), ["brown"] = new(0xA5, 0x2A, 0x2A),
        ["burlywood"] = new(0xDE, 0xB8, 0x87), ["cadetblue"] = new(0x5F, 0x9E, 0xA0), ["chartreuse"] = new(0x7F, 0xFF, 0x00),
        ["chocolate"] = new(0xD2, 0x69, 0x1E), ["coral"] = new(0xFF, 0x7F, 0x50), ["cornflowerblue"] = new(0x64, 0x95, 0xED),
        ["cornsilk"] = new(0xFF, 0xF8, 0xDC), ["crimson"] = new(0xDC, 0x14, 0x3C), ["cyan"] = new(0x00, 0xFF, 0xFF),
        ["darkblue"] = new(0x00, 0x00, 0x8B), ["darkcyan"] = new(0x00, 0x8B, 0x8B), ["darkgoldenrod"] = new(0xB8, 0x86, 0x0B),
        ["darkgray"] = new(0xA9, 0xA9, 0xA9), ["darkgreen"] = new(0x00, 0x64, 0x00), ["darkgrey"] = new(0xA9, 0xA9, 0xA9),
        ["darkkhaki"] = new(0xBD, 0xB7, 0x6B), ["darkmagenta"] = new(0x8B, 0x00, 0x8B), ["darkolivegreen"] = new(0x55, 0x6B, 0x2F),
        ["darkorange"] = new(0xFF, 0x8C, 0x00), ["darkorchid"] = new(0x99, 0x32, 0xCC), ["darkred"] = new(0x8B, 0x00, 0x00),
        ["darksalmon"] = new(0xE9, 0x96, 0x7A), ["darkseagreen"] = new(0x8F, 0xBC, 0x8F), ["darkslateblue"] = new(0x48, 0x3D, 0x8B),
        ["darkslategray"] = new(0x2F, 0x4F, 0x4F), ["darkslategrey"] = new(0x2F, 0x4F, 0x4F), ["darkturquoise"] = new(0x00, 0xCE, 0xD1),
        ["darkviolet"] = new(0x94, 0x00, 0xD3), ["deeppink"] = new(0xFF, 0x14, 0x93), ["deepskyblue"] = new(0x00, 0xBF, 0xFF),
        ["dimgray"] = new(0x69, 0x69, 0x69), ["dimgrey"] = new(0x69, 0x69, 0x69), ["dodgerblue"] = new(0x1E, 0x90, 0xFF),
        ["firebrick"] = new(0xB2, 0x22, 0x22), ["floralwhite"] = new(0xFF, 0xFA, 0xF0), ["forestgreen"] = new(0x22, 0x8B, 0x22),
        ["fuchsia"] = new(0xFF, 0x00, 0xFF), ["gainsboro"] = new(0xDC, 0xDC, 0xDC), ["ghostwhite"] = new(0xF8, 0xF8, 0xFF),
        ["gold"] = new(0xFF, 0xD7, 0x00), ["goldenrod"] = new(0xDA, 0xA5, 0x20), ["gray"] = new(0x80, 0x80, 0x80),
        ["green"] = new(0x00, 0x80, 0x00), ["greenyellow"] = new(0xAD, 0xFF, 0x2F), ["grey"] = new(0x80, 0x80, 0x80),
        ["honeydew"] = new(0xF0, 0xFF, 0xF0), ["hotpink"] = new(0xFF, 0x69, 0xB4), ["indianred"] = new(0xCD, 0x5C, 0x5C),
        ["indigo"] = new(0x4B, 0x00, 0x82), ["ivory"] = new(0xFF, 0xFF, 0xF0), ["khaki"] = new(0xF0, 0xE6, 0x8C),
        ["lavender"] = new(0xE6, 0xE6, 0xFA), ["lavenderblush"] = new(0xFF, 0xF0, 0xF5), ["lawngreen"] = new(0x7C, 0xFC, 0x00),
        ["lemonchiffon"] = new(0xFF, 0xFA, 0xCD), ["lightblue"] = new(0xAD, 0xD8, 0xE6), ["lightcoral"] = new(0xF0, 0x80, 0x80),
        ["lightcyan"] = new(0xE0, 0xFF, 0xFF), ["lightgoldenrodyellow"] = new(0xFA, 0xFA, 0xD2), ["lightgray"] = new(0xD3, 0xD3, 0xD3),
        ["lightgreen"] = new(0x90, 0xEE, 0x90), ["lightgrey"] = new(0xD3, 0xD3, 0xD3), ["lightpink"] = new(0xFF, 0xB6, 0xC1),
        ["lightsalmon"] = new(0xFF, 0xA0, 0x7A), ["lightseagreen"] = new(0x20, 0xB2, 0xAA), ["lightskyblue"] = new(0x87, 0xCE, 0xFA),
        ["lightslategray"] = new(0x77, 0x88, 0x99), ["lightslategrey"] = new(0x77, 0x88, 0x99), ["lightsteelblue"] = new(0xB0, 0xC4, 0xDE),
        ["lightyellow"] = new(0xFF, 0xFF, 0xE0), ["lime"] = new(0x00, 0xFF, 0x00), ["limegreen"] = new(0x32, 0xCD, 0x32),
        ["linen"] = new(0xFA, 0xF0, 0xE6), ["magenta"] = new(0xFF, 0x00, 0xFF), ["maroon"] = new(0x80, 0x00, 0x00),
        ["mediumaquamarine"] = new(0x66, 0xCD, 0xAA), ["mediumblue"] = new(0x00, 0x00, 0xCD), ["mediumorchid"] = new(0xBA, 0x55, 0xD3),
        ["mediumpurple"] = new(0x93, 0x70, 0xDB), ["mediumseagreen"] = new(0x3C, 0xB3, 0x71), ["mediumslateblue"] = new(0x7B, 0x68, 0xEE),
        ["mediumspringgreen"] = new(0x00, 0xFA, 0x9A), ["mediumturquoise"] = new(0x48, 0xD1, 0xCC), ["mediumvioletred"] = new(0xC7, 0x15, 0x85),
        ["midnightblue"] = new(0x19, 0x19, 0x70), ["mintcream"] = new(0xF5, 0xFF, 0xFA), ["mistyrose"] = new(0xFF, 0xE4, 0xE1),
        ["moccasin"] = new(0xFF, 0xE4, 0xB5), ["navajowhite"] = new(0xFF, 0xDE, 0xAD), ["navy"] = new(0x00, 0x00, 0x80),
        ["oldlace"] = new(0xFD, 0xF5, 0xE6), ["olive"] = new(0x80, 0x80, 0x00), ["olivedrab"] = new(0x6B, 0x8E, 0x23),
        ["orange"] = new(0xFF, 0xA5, 0x00), ["orangered"] = new(0xFF, 0x45, 0x00), ["orchid"] = new(0xDA, 0x70, 0xD6),
        ["palegoldenrod"] = new(0xEE, 0xE8, 0xAA), ["palegreen"] = new(0x98, 0xFB, 0x98), ["paleturquoise"] = new(0xAF, 0xEE, 0xEE),
        ["palevioletred"] = new(0xDB, 0x70, 0x93), ["papayawhip"] = new(0xFF, 0xEF, 0xD5), ["peachpuff"] = new(0xFF, 0xDA, 0xB9),
        ["peru"] = new(0xCD, 0x85, 0x3F), ["pink"] = new(0xFF, 0xC0, 0xCB), ["plum"] = new(0xDD, 0xA0, 0xDD),
        ["powderblue"] = new(0xB0, 0xE0, 0xE6), ["purple"] = new(0x80, 0x00, 0x80), ["rebeccapurple"] = new(0x66, 0x33, 0x99),
        ["red"] = new(0xFF, 0x00, 0x00), ["rosybrown"] = new(0xBC, 0x8F, 0x8F), ["royalblue"] = new(0x41, 0x69, 0xE1),
        ["saddlebrown"] = new(0x8B, 0x45, 0x13), ["salmon"] = new(0xFA, 0x80, 0x72), ["sandybrown"] = new(0xF4, 0xA4, 0x60),
        ["seagreen"] = new(0x2E, 0x8B, 0x57), ["seashell"] = new(0xFF, 0xF5, 0xEE), ["sienna"] = new(0xA0, 0x52, 0x2D),
        ["silver"] = new(0xC0, 0xC0, 0xC0), ["skyblue"] = new(0x87, 0xCE, 0xEB), ["slateblue"] = new(0x6A, 0x5A, 0xCD),
        ["slategray"] = new(0x70, 0x80, 0x90), ["slategrey"] = new(0x70, 0x80, 0x90), ["snow"] = new(0xFF, 0xFA, 0xFA),
        ["springgreen"] = new(0x00, 0xFF, 0x7F), ["steelblue"] = new(0x46, 0x82, 0xB4), ["tan"] = new(0xD2, 0xB4, 0x8C),
        ["teal"] = new(0x00, 0x80, 0x80), ["thistle"] = new(0xD8, 0xBF, 0xD8), ["tomato"] = new(0xFF, 0x63, 0x47),
        ["turquoise"] = new(0x40, 0xE0, 0xD0), ["violet"] = new(0xEE, 0x82, 0xEE), ["wheat"] = new(0xF5, 0xDE, 0xB3),
        ["white"] = new(0xFF, 0xFF, 0xFF), ["whitesmoke"] = new(0xF5, 0xF5, 0xF5), ["yellow"] = new(0xFF, 0xFF, 0x00),
        ["yellowgreen"] = new(0x9A, 0xCD, 0x32),
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
        if (value.Equals("transparent", StringComparison.OrdinalIgnoreCase)) { color = SKColors.Transparent; return true; }
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
