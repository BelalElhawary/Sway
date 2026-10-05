using Sway.Assets;
using SkiaSharp;

namespace Sway.Widgets;

/// <summary>Shared SKFont instances keyed by the style properties that affect glyph shaping.</summary>
public static class FontCache
{
    readonly record struct Key(string Family, float Size, int Weight, bool Italic);

    static readonly Dictionary<Key, SKFont> Fonts = new();
    static readonly List<(string family, int weight, bool italic, SKTypeface face)> Registered = new();
    static readonly Dictionary<(string, int, bool), SKTypeface> Typefaces = new();

    // Faces shipped inside Sway.Assets so text looks the same on every platform, including ones without system fonts.
    static readonly (string File, string Family, int Weight, bool Italic, bool ArabicFallback)[] Bundled =
    [
        ("Roboto-Light.ttf", "Roboto", 300, false, false),
        ("Roboto-Regular.ttf", "Roboto", 400, false, false),
        ("Roboto-Italic.ttf", "Roboto", 400, true, false),
        ("Roboto-Medium.ttf", "Roboto", 500, false, false),
        ("Roboto-Bold.ttf", "Roboto", 700, false, false),
        ("NotoSansArabic-Regular.ttf", "Noto Sans Arabic", 400, false, true),
        ("NotoSansArabic-Bold.ttf", "Noto Sans Arabic", 700, false, true),
    ];

    static FontCache()
    {
        foreach (var (file, family, weight, italic, arabic) in Bundled)
        {
            using var stream = BundledFonts.Open(file);
            if (stream is null) continue;
            var face = SKTypeface.FromData(SKData.Create(stream));
            if (face is null) continue;
            Registered.Add((family, weight, italic, face));
            if (arabic) TextShaper.RegisterFallback(face);
        }
    }

    /// <summary>Forces the bundled faces to load.</summary>
    internal static void EnsureLoaded() { }

    public static SKFont Get(string family, float size, int weight, bool italic)
    {
        var key = new Key(family, size, weight, italic);
        lock (Fonts)
        {
            if (!Fonts.TryGetValue(key, out var font))
            {
                font = new SKFont(GetTypeface(family, weight, italic), size)
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
}
