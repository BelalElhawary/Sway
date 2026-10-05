using HarfBuzzSharp;
using SkiaSharp;
using SkiaSharp.HarfBuzz;

namespace Sway.Widgets;

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

    /// <summary>
    /// X offset of every caret boundary in a run (length + 1 entries, from the run's left edge). Offsets come from the
    /// shaper's glyph clusters, so they land on the glyphs that are drawn, including joined Arabic forms.
    /// </summary>
    public static float[] CaretOffsets(string text, SKFont font, bool rtl)
    {
        var offsets = new float[text.Length + 1];
        if (text.Length == 0) return offsets;

        var shaped = ContainsRtl(text) ? GetShaper(font.Typeface).Shape(text, font) : null;
        if (shaped is null || shaped.Clusters.Length == 0)
        {
            for (int i = 1; i <= text.Length; i++) offsets[i] = font.MeasureText(text.AsSpan(0, i));
            return offsets;
        }

        // Clusters are UTF-8 byte offsets; map them back to UTF-16 indices.
        var charAtByte = new Dictionary<int, int> { [0] = 0 };
        int bytes = 0, chars = 0;
        foreach (var rune in text.EnumerateRunes())
        {
            bytes += rune.Utf8SequenceLength;
            chars += rune.Utf16SequenceLength;
            charAtByte[bytes] = chars;
        }

        // Each glyph covers the space up to the next glyph in visual order.
        var points = shaped.Points;
        var byX = Enumerable.Range(0, points.Length).OrderBy(i => points[i].X).ToArray();
        var extents = new SortedDictionary<int, (float Lo, float Hi)>();
        for (int k = 0; k < byX.Length; k++)
        {
            int g = byX[k];
            if (!charAtByte.TryGetValue((int)shaped.Clusters[g], out int cluster)) continue;
            float lo = points[g].X, hi = k + 1 < byX.Length ? points[byX[k + 1]].X : shaped.Width;
            extents[cluster] = extents.TryGetValue(cluster, out var e) ? (Math.Min(e.Lo, lo), Math.Max(e.Hi, hi)) : (lo, hi);
        }

        if (extents.Count == 0)
        {
            for (int i = 1; i <= text.Length; i++) offsets[i] = font.MeasureText(text.AsSpan(0, i));
            return offsets;
        }

        // A cluster starts at the right edge of an RTL run and at the left edge of an LTR run.
        var starts = extents.Keys.ToArray();
        for (int k = 0; k < starts.Length; k++)
        {
            int from = starts[k], to = k + 1 < starts.Length ? starts[k + 1] : text.Length;
            var (lo, hi) = extents[from];
            float startX = rtl ? hi : lo, endX = rtl ? lo : hi;
            for (int j = from; j < to; j++)
                offsets[j] = startX + (endX - startX) * (j - from) / (to - from);
            if (to == text.Length) offsets[to] = endX;
        }
        return offsets;
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
