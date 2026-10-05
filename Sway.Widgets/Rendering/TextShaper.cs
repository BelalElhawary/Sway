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

    // Shaping is the expensive step (HarfBuzz, and much slower under WASM), and layout, caret mapping and painting all
    // shape the same strings every frame. Results are kept per (typeface, size, text) and drawn from a cached blob.
    readonly record struct ShapeKey(SKTypeface Face, float Size, float ScaleX, string Text);
    sealed class Shaped
    {
        public SKShaper.Result Result = null!;
        public SKFont Font = null!;
        public SKTextBlob? Blob;
    }

    static readonly List<SKTypeface> Fallbacks = new();
    static readonly Dictionary<(SKTypeface, SKFont), SKFont> FallbackFonts = new();
    static readonly Dictionary<(SKTypeface, int), SKTypeface?> FaceForChar = new();

    /// <summary>
    /// Registers a typeface used for characters the requested font has no glyph for (e.g. Arabic on a platform
    /// without system fonts, such as WebAssembly).
    /// </summary>
    public static void RegisterFallback(SKTypeface face)
    {
        lock (Fallbacks) { Fallbacks.Add(face); FaceForChar.Clear(); }
    }

    /// <summary>The font to shape with: the requested one when it covers the text's first RTL character, else a fallback that does.</summary>
    static SKFont Resolve(string text, SKFont font)
    {
        FontCache.EnsureLoaded();
        int cp = 0;
        foreach (var rune in text.EnumerateRunes())
            if (rune.Value <= 0xFFFF && IsRtlChar((char)rune.Value)) { cp = rune.Value; break; }
        if (cp == 0 || font.Typeface.ContainsGlyph(cp)) return font;

        SKTypeface? face;
        lock (Fallbacks)
        {
            if (!FaceForChar.TryGetValue((font.Typeface, cp), out face))
            {
                face = Fallbacks.Where(f => f.ContainsGlyph(cp)).OrderBy(f => Math.Abs(f.FontWeight - font.Typeface.FontWeight)).FirstOrDefault();
                face ??= SKFontManager.Default.MatchCharacter(font.Typeface.FamilyName, font.Typeface.FontStyle, null, cp);
                FaceForChar[(font.Typeface, cp)] = face;
            }
        }
        if (face is null) return font;

        lock (Fallbacks)
        {
            if (!FallbackFonts.TryGetValue((face, font), out var f))
            {
                f = new SKFont(face, font.Size, font.ScaleX, font.SkewX) { Edging = font.Edging, Subpixel = font.Subpixel };
                FallbackFonts[(face, font)] = f;
            }
            return f;
        }
    }

    const int MaxCached = 2048;
    static readonly Dictionary<ShapeKey, Shaped> ShapeCache = new();

    // If the native HarfBuzz library cannot load (e.g. missing WASM asset), shaping is disabled for good. Retrying would
    // throw on every measure and paint, which is extremely slow.
    static bool _unavailable;

    static Shaped GetShaped(string text, SKFont font)
    {
        if (_unavailable) return new Shaped();
        font = Resolve(text, font);
        var key = new ShapeKey(font.Typeface, font.Size, font.ScaleX, text);
        lock (ShapeCache)
        {
            if (ShapeCache.TryGetValue(key, out var hit)) return hit;
            if (ShapeCache.Count >= MaxCached)
            {
                foreach (var old in ShapeCache.Values) old.Blob?.Dispose();
                ShapeCache.Clear();
            }
            try
            {
                var shaped = new Shaped { Result = GetShaper(font.Typeface).Shape(text, font), Font = font };
                ShapeCache[key] = shaped;
                return shaped;
            }
            catch (Exception e) when (e is DllNotFoundException or EntryPointNotFoundException or TypeInitializationException)
            {
                _unavailable = true;
                Console.Error.WriteLine($"HarfBuzz unavailable, falling back to unshaped text: {e.Message}");
                return new Shaped();
            }
        }
    }

    static SKTextBlob? BuildBlob(SKShaper.Result r, SKFont font)
    {
        if (r.Codepoints.Length == 0) return null;
        using var builder = new SKTextBlobBuilder();
        var run = builder.AllocatePositionedRun(font, r.Codepoints.Length);
        var glyphs = run.Glyphs;
        var positions = run.Positions;
        for (int i = 0; i < r.Codepoints.Length; i++)
        {
            glyphs[i] = (ushort)r.Codepoints[i];
            positions[i] = new SKPoint(r.Points[i].X, r.Points[i].Y);
        }
        return builder.Build();
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

        return GetShaped(text, font).Result?.Width ?? font.MeasureText(text);
    }

    /// <summary>
    /// X offset of every caret boundary in a run (length + 1 entries, from the run's left edge). Offsets come from the
    /// shaper's glyph clusters, so they land on the glyphs that are drawn, including joined Arabic forms.
    /// </summary>
    public static float[] CaretOffsets(string text, SKFont font, bool rtl)
    {
        var offsets = new float[text.Length + 1];
        if (text.Length == 0) return offsets;

        var shaped = ContainsRtl(text) ? GetShaped(text, font).Result : null;
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

        var shaped = GetShaped(text, font);
        if (shaped.Result is null)
        {
            canvas.DrawText(text, x, baseline, textAlign, font, paint);
            return;
        }
        float dx = textAlign switch { SKTextAlign.Center => -shaped.Result.Width / 2, SKTextAlign.Right => -shaped.Result.Width, _ => 0 };
        SKTextBlob? blob;
        lock (ShapeCache) blob = shaped.Blob ??= BuildBlob(shaped.Result, shaped.Font);
        if (blob is not null) canvas.DrawText(blob, x + dx, baseline, paint);
    }
}
