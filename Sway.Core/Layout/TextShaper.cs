using HarfBuzzSharp;
using SkiaSharp;
using SkiaSharp.HarfBuzz;

namespace Sway.Core.Layout;

/// <summary>
/// Handles HarfBuzz text shaping, Arabic contextual shaping, and text measurement.
/// </summary>
public static class TextShaper
{
    static readonly Dictionary<SKTypeface, SKShaper> Shapers = new();

    public static SKShaper GetShaper(SKTypeface typeface)
    {
        lock (Shapers)
        {
            if (!Shapers.TryGetValue(typeface, out var shaper))
            {
                shaper = new SKShaper(typeface);
                Shapers[typeface] = shaper;
            }
            return shaper;
        }
    }

    /// <summary>Checks if a character belongs to an RTL script (Arabic, Hebrew, Syriac, Thaana, etc.).</summary>
    public static bool IsRtlChar(char c)
    {
        return (c >= 0x0590 && c <= 0x08FF)
            || (c >= 0xFB1D && c <= 0xFDFF)
            || (c >= 0xFE70 && c <= 0xFEFC)
            || (c >= 0x10800 && c <= 0x10FFF)
            || (c >= 0x1E800 && c <= 0x1EFFF);
    }

    /// <summary>Checks if a string contains any Arabic or RTL characters.</summary>
    public static bool ContainsRtl(ReadOnlySpan<char> text)
    {
        for (int i = 0; i < text.Length; i++)
            if (IsRtlChar(text[i])) return true;
        return false;
    }

    /// <summary>Determines if a string is predominantly or starts with RTL characters.</summary>
    public static bool IsRtlText(string text)
    {
        foreach (char c in text)
        {
            if (char.IsWhiteSpace(c) || char.IsPunctuation(c) || char.IsDigit(c)) continue;
            return IsRtlChar(c);
        }
        return false;
    }

    /// <summary>Measures shaped text width using HarfBuzz shaper.</summary>
    public static float MeasureShaped(string text, SKFont font)
    {
        if (string.IsNullOrEmpty(text)) return 0;
        if (!ContainsRtl(text)) return font.MeasureText(text);

        var shaper = GetShaper(font.Typeface);
        var result = shaper.Shape(text, font);
        return result?.Width ?? font.MeasureText(text);
    }

    /// <summary>Draws shaped text onto a canvas at (x, baseline).</summary>
    public static void DrawShapedText(SKCanvas canvas, string text, float x, float baseline, SKFont font, SKPaint paint, SKTextAlign textAlign = SKTextAlign.Left)
    {
        if (string.IsNullOrEmpty(text)) return;

        if (!ContainsRtl(text))
        {
            canvas.DrawText(text, x, baseline, textAlign, font, paint);
            return;
        }

        var shaper = GetShaper(font.Typeface);
        canvas.DrawShapedText(shaper, text, x, baseline, textAlign, font, paint);
    }
}
