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

    /// <summary>
    /// A size-bounded cache that evicts by age instead of clearing everything: entries untouched for a whole generation are
    /// dropped, so a full cache never throws away what the current screen is still using (and never reshapes it all at once).
    /// </summary>
    internal sealed class GenerationCache<TKey, TValue>(int generationSize, Action<TValue> release) where TKey : notnull
    {
        Dictionary<TKey, TValue> _current = new();
        Dictionary<TKey, TValue> _previous = new();

        public int Count => _current.Count + _previous.Count;

        public bool TryGetValue(TKey key, out TValue value)
        {
            if (_current.TryGetValue(key, out value!)) return true;
            if (!_previous.Remove(key, out value!)) return false;
            _current[key] = value; // touched this generation: keep it
            return true;
        }

        public void Add(TKey key, TValue value)
        {
            if (_current.Count >= generationSize)
            {
                foreach (var stale in _previous.Values) release(stale);
                _previous = _current;
                _current = new Dictionary<TKey, TValue>();
            }
            _current[key] = value;
        }
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
        if (cp == 0 || font.GetGlyph(cp) != 0) return font;

        SKTypeface? face;
        lock (Fallbacks)
        {
            if (!FaceForChar.TryGetValue((font.Typeface, cp), out face))
            {
                face = Fallbacks.Where(f => { using var ff = new SKFont(f); return ff.GetGlyph(cp) != 0; }).OrderBy(f => Math.Abs(f.FontWeight - font.Typeface.FontWeight)).FirstOrDefault();
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

    /// <summary>
    /// When RTL text needs a fallback face that lacks some of its neutral characters (the Arabic face has no "$" or brackets),
    /// splits it into logical pieces so those characters are drawn with the requested font instead of as missing-glyph boxes.
    /// Null when one font covers the whole text.
    /// </summary>
    static List<(string Text, SKFont Font)>? Split(string text, SKFont font)
    {
        var face = Resolve(text, font);
        if (ReferenceEquals(face, font)) return null;

        var pieces = new List<(string, SKFont)>();
        var sb = new System.Text.StringBuilder();
        bool? primary = null;
        void Flush()
        {
            if (sb.Length > 0) pieces.Add((sb.ToString(), primary == true ? font : face));
            sb.Clear();
        }
        foreach (var rune in text.EnumerateRunes())
        {
            bool usePrimary = !(rune.Value <= 0xFFFF && IsRtlChar((char)rune.Value)) && face.GetGlyph(rune.Value) == 0 && font.GetGlyph(rune.Value) != 0;
            if (primary != usePrimary) { Flush(); primary = usePrimary; }
            // The requested font is drawn unshaped, so it does not mirror brackets inside right-to-left text itself.
            sb.Append(usePrimary ? Mirror(rune) : rune.ToString());
        }
        Flush();
        return pieces.Count > 1 || pieces[0].Item2 == font ? pieces : null;
    }

    static string Mirror(System.Text.Rune r) => r.Value switch
    {
        '(' => ")", ')' => "(", '[' => "]", ']' => "[", '{' => "}", '}' => "{", '<' => ">", '>' => "<",
        _ => r.ToString(),
    };

    static float PieceWidth(string text, SKFont font) =>
        ContainsRtl(text) ? GetShaped(text, font).Result?.Width ?? font.MeasureText(text) : font.MeasureText(text);

    const int MaxCached = 2048;
    static readonly GenerationCache<ShapeKey, Shaped> ShapeCache = new(MaxCached, shaped => shaped.Blob?.Dispose());

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
            try
            {
                var shaped = new Shaped { Result = GetShaper(font.Typeface).Shape(text, font), Font = font };
                ShapeCache.Add(key, shaped);
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

        if (Split(text, font) is { } pieces) return pieces.Sum(p => PieceWidth(p.Text, p.Font));
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

        if (ContainsRtl(text) && Split(text, font) is { } pieces)
        {
            float total = pieces.Sum(p => PieceWidth(p.Text, p.Font)), before = 0;
            int at = 0;
            foreach (var (pt, pf) in pieces)
            {
                float w = PieceWidth(pt, pf), left = rtl ? total - before - w : before;
                var po = CaretOffsetsOf(pt, pf, rtl);
                for (int j = 0; j <= pt.Length; j++) offsets[at + j] = left + po[j];
                at += pt.Length; before += w;
            }
            return offsets;
        }
        return CaretOffsetsOf(text, font, rtl);
    }

    static float[] CaretOffsetsOf(string text, SKFont font, bool rtl)
    {
        var offsets = new float[text.Length + 1];
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

        if (Split(text, font) is { } pieces)
        {
            float total = pieces.Sum(p => PieceWidth(p.Text, p.Font));
            float px = x + textAlign switch { SKTextAlign.Center => -total / 2, SKTextAlign.Right => -total, _ => 0 };
            // Pieces are logical; right-to-left text puts the first one at the right.
            foreach (var (pt, pf) in Enumerable.Reverse(pieces))
            {
                DrawShapedText(canvas, pt, px, baseline, pf, paint);
                px += PieceWidth(pt, pf);
            }
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
