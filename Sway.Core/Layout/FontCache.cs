using SkiaSharp;
using Sway.Core.Styling;

namespace Sway.Core.Layout;

/// <summary>Shared SKFont instances keyed by the style properties that affect glyph shaping.</summary>
public static class FontCache
{
    readonly record struct Key(string Family, float Size, int Weight, bool Italic);

    static readonly Dictionary<Key, SKFont> Fonts = new();
    static readonly List<(string family, int weight, bool italic, SKTypeface face)> Registered = new();
    static readonly Dictionary<(string, int, bool), SKTypeface> Typefaces = new();

    public static SKFont Get(ComputedStyle style)
    {
        var key = new Key(style.FontFamily, style.FontSize, style.FontWeight, style.Italic);
        lock (Fonts)
        {
            if (!Fonts.TryGetValue(key, out var font))
            {
                font = new SKFont(GetTypeface(style.FontFamily, style.FontWeight, style.Italic), style.FontSize)
                {
                    Edging = SKFontEdging.SubpixelAntialias,
                    Subpixel = true,
                };
                Fonts[key] = font;
            }
            return font;
        }
    }

    /// <summary>Makes a typeface available under a family name, as an @font-face rule does.</summary>
    public static void RegisterFace(string family, int weight, bool italic, SKTypeface face)
    {
        lock (Fonts)
        {
            Registered.Add((family, weight, italic, face));
            // Anything resolved earlier may now have a better match.
            Fonts.Clear();
            Typefaces.Clear();
        }
    }

    // The registered face of a family closest to the requested style: italic first, then nearest weight.
    // On a tie the face registered last wins, so a later @font-face can override an earlier one.
    static SKTypeface? FindRegistered(string family, int weight, bool italic)
    {
        SKTypeface? best = null;
        int bestScore = int.MaxValue;
        foreach (var (name, w, it, face) in Registered)
        {
            if (!name.Equals(family, StringComparison.OrdinalIgnoreCase)) continue;
            int score = (it == italic ? 0 : 10000) + Math.Abs(w - weight);
            if (score <= bestScore) { best = face; bestScore = score; }
        }
        return best;
    }

    static SKTypeface GetTypeface(string familyList, int weight, bool italic)
    {
        if (Typefaces.TryGetValue((familyList, weight, italic), out var cached)) return cached;

        var fontStyle = new SKFontStyle(weight, (int)SKFontStyleWidth.Normal, italic ? SKFontStyleSlant.Italic : SKFontStyleSlant.Upright);
        SKTypeface? found = null;
        foreach (var raw in familyList.Split(','))
        {
            string family = raw.Trim().Trim('"', '\'');
            if (family.Length == 0) continue;

            if (FindRegistered(family, weight, italic) is { } registered)
            {
                found = registered;
                break;
            }

            if (family is "sans-serif" or "system-ui") family = "Segoe UI";
            else if (family == "serif") family = "Times New Roman";
            else if (family == "monospace") family = "Consolas";

            var candidate = SKTypeface.FromFamilyName(family, fontStyle);
            // FromFamilyName silently substitutes a default face; only accept a real family match.
            if (candidate is not null && candidate.FamilyName.Equals(family, StringComparison.OrdinalIgnoreCase))
            {
                found = candidate;
                break;
            }
        }

        found ??= SKTypeface.FromFamilyName(null, fontStyle) ?? SKTypeface.Default;
        Typefaces[(familyList, weight, italic)] = found;
        return found;
    }

    public static float LineHeight(ComputedStyle style)
    {
        if (style.LineHeight is { } lh)
            return style.LineHeightIsMultiplier ? lh * style.FontSize : lh;

        var font = Get(style);
        return font.Spacing;
    }

    public static float Measure(string text, ComputedStyle style) => TextShaper.MeasureShaped(text, Get(style));
}
