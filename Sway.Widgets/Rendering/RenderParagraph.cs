using SkiaSharp;

namespace Sway.Widgets;

/// <summary>Lays out and paints styled, wrapped text using HarfBuzz-aware measurement.</summary>
public sealed class RenderParagraph : RenderBox
{
    sealed record Segment(string Text, TextStyle Style, SKFont Font, float Width);
    sealed class Line
    {
        public List<Segment> Segments = new();
        public float Width, Height, Baseline, Top;
        public bool HardBreak;
    }

    TextSpan _text;
    TextStyle _baseStyle;
    TextAlign _textAlign;
    TextDirection _direction;
    bool _softWrap;
    TextOverflow _overflow;
    int? _maxLines;
    List<Line> _lines = new();
    bool _didOverflow;

    public RenderParagraph(TextSpan text, TextStyle baseStyle, TextAlign textAlign, TextDirection direction, bool softWrap, TextOverflow overflow, int? maxLines)
    {
        _text = text; _baseStyle = baseStyle; _textAlign = textAlign; _direction = direction;
        _softWrap = softWrap; _overflow = overflow; _maxLines = maxLines;
    }

    public void Update(TextSpan text, TextStyle baseStyle, TextAlign textAlign, TextDirection direction, bool softWrap, TextOverflow overflow, int? maxLines)
    {
        bool layout = !_text.Equals(text) || !_baseStyle.Equals(baseStyle) || _direction != direction || _softWrap != softWrap
            || _overflow != overflow || _maxLines != maxLines || _textAlign != textAlign;
        _text = text; _baseStyle = baseStyle; _textAlign = textAlign; _direction = direction;
        _softWrap = softWrap; _overflow = overflow; _maxLines = maxLines;
        if (layout) MarkNeedsLayout();
    }

    public override void VisitChildren(Action<RenderObject> visitor) { }

    // --- text flattening and line breaking ---

    IEnumerable<(string text, TextStyle style)> Runs()
    {
        IEnumerable<(string, TextStyle)> Walk(TextSpan span, TextStyle inherited)
        {
            var style = inherited.Merge(span.Style);
            if (!string.IsNullOrEmpty(span.Text)) yield return (span.Text, style);
            if (span.Children is not null)
                foreach (var child in span.Children)
                    foreach (var r in Walk(child, style)) yield return r;
        }
        return Walk(_text, TextStyle.Fallback.Merge(_baseStyle));
    }

    static float Measure(string text, TextStyle style, SKFont font)
    {
        float w = TextShaper.MeasureShaped(text, font);
        if (style.LetterSpacing is { } ls) w += ls * text.Length;
        return w;
    }

    List<Line> BuildLines(float maxWidth)
    {
        var lines = new List<Line>();
        var line = new Line();
        float x = 0;

        void Place(string token, TextStyle style, SKFont font, float width)
        {
            if (line.Segments.Count > 0 && ReferenceEquals(line.Segments[^1].Style, style))
            {
                var last = line.Segments[^1];
                line.Segments[^1] = last with { Text = last.Text + token, Width = Measure(last.Text + token, style, font) };
                x = line.Segments.Sum(s => s.Width);
            }
            else
            {
                line.Segments.Add(new Segment(token, style, font, width));
                x += width;
            }
        }

        void EndLine(bool hard)
        {
            line.HardBreak = hard;
            TrimTrailingSpace(line);
            line.Width = line.Segments.Sum(s => s.Width);
            lines.Add(line);
            line = new Line();
            x = 0;
        }

        foreach (var (text, style) in Runs())
        {
            var font = style.ToFont();
            int i = 0;
            while (i < text.Length)
            {
                if (text[i] == '\n') { EndLine(true); i++; continue; }

                int j = i;
                bool space = char.IsWhiteSpace(text[i]);
                while (j < text.Length && text[j] != '\n' && char.IsWhiteSpace(text[j]) == space) j++;
                string token = text.Substring(i, j - i);
                i = j;

                float w = Measure(token, style, font);
                if (_softWrap && !space && x + w > maxWidth && line.Segments.Count > 0)
                    EndLine(false);
                if (space && line.Segments.Count == 0 && lines.Count > 0 && !lines[^1].HardBreak) continue; // no leading space after a soft wrap
                Place(token, style, font, w);
            }
        }
        EndLine(true);
        return lines;
    }

    static void TrimTrailingSpace(Line line)
    {
        while (line.Segments.Count > 0)
        {
            var last = line.Segments[^1];
            string trimmed = last.Text.TrimEnd(' ', '\t');
            if (trimmed.Length == 0) { line.Segments.RemoveAt(line.Segments.Count - 1); continue; }
            if (trimmed.Length != last.Text.Length)
                line.Segments[^1] = last with { Text = trimmed, Width = Measure(trimmed, last.Style, last.Font) };
            break;
        }
    }

    protected override void PerformLayout()
    {
        float maxWidth = Constraints.MaxWidth;
        _lines = BuildLines(maxWidth);
        _didOverflow = false;

        if (_maxLines is { } max && _lines.Count > max)
        {
            _lines = _lines.Take(max).ToList();
            _didOverflow = true;
        }

        float y = 0;
        float widest = 0;
        foreach (var l in _lines)
        {
            float lineH = 0, baseline = 0;
            if (l.Segments.Count == 0)
            {
                var style = TextStyle.Fallback.Merge(_baseStyle);
                var m = style.ToFont().Metrics;
                (lineH, baseline) = LineMetrics(style, m);
            }
            foreach (var s in l.Segments)
            {
                var (h, b) = LineMetrics(s.Style, s.Font.Metrics);
                lineH = Math.Max(lineH, h);
                baseline = Math.Max(baseline, b);
            }
            l.Height = lineH; l.Baseline = baseline; l.Top = y;
            y += lineH;
            widest = Math.Max(widest, l.Width);
        }

        if (_didOverflow && _overflow == TextOverflow.Ellipsis && _lines.Count > 0)
            ApplyEllipsis(_lines[^1], maxWidth);

        // Text hugs its longest line; alignment other than start needs the full width to position lines inside.
        float width = widest;
        if (_textAlign is not (TextAlign.Start or TextAlign.Left) && Constraints.HasBoundedWidth) width = Constraints.MaxWidth;
        Size = Constraints.Constrain(new Size(width, y));
    }

    static (float height, float baseline) LineMetrics(TextStyle style, SKFontMetrics m)
    {
        float ascent = -m.Ascent, descent = m.Descent;
        float natural = ascent + descent + m.Leading;
        float height = style.Height is { } mult ? mult * (style.FontSize ?? TextStyle.DefaultFontSize) : natural;
        float baseline = (height - (ascent + descent)) / 2 + ascent;
        return (height, baseline);
    }

    void ApplyEllipsis(Line line, float maxWidth)
    {
        if (line.Segments.Count == 0 || !maxWidth.IsFinite()) return;
        var last = line.Segments[^1];
        const string ellipsis = "…";
        float ew = Measure(ellipsis, last.Style, last.Font);
        while (line.Segments.Sum(s => s.Width) + ew > maxWidth && line.Segments.Count > 0)
        {
            last = line.Segments[^1];
            if (last.Text.Length <= 1) { line.Segments.RemoveAt(line.Segments.Count - 1); continue; }
            string cut = last.Text[..^1].TrimEnd();
            line.Segments[^1] = last with { Text = cut, Width = Measure(cut, last.Style, last.Font) };
        }
        if (line.Segments.Count > 0)
        {
            last = line.Segments[^1];
            string text = last.Text + ellipsis;
            line.Segments[^1] = last with { Text = text, Width = Measure(text, last.Style, last.Font) };
        }
        line.Width = line.Segments.Sum(s => s.Width);
    }

    // --- painting ---

    bool LineIsRtl(Line l) => _direction == TextDirection.Rtl;

    float StartX(Line line)
    {
        float free = Size.Width - line.Width;
        bool rtl = _direction == TextDirection.Rtl;
        return _textAlign switch
        {
            TextAlign.Center => free / 2,
            TextAlign.Left => 0,
            TextAlign.Right => free,
            TextAlign.End => rtl ? 0 : free,
            _ => rtl ? free : 0,
        };
    }

    public override void Paint(PaintingContext context, Offset offset)
    {
        var canvas = context.Canvas;
        bool clip = _overflow == TextOverflow.Clip && _didOverflow || _overflow == TextOverflow.Clip && _lines.Any(l => l.Width > Size.Width + 0.5f);
        if (clip) { canvas.Save(); canvas.ClipRect(Size.ToRect(offset).ToSk()); }

        foreach (var line in _lines)
        {
            float x = offset.Dx + StartX(line);
            var order = _direction == TextDirection.Rtl ? Enumerable.Reverse(line.Segments) : line.Segments;
            foreach (var seg in order)
            {
                float baseline = offset.Dy + line.Top + line.Baseline;
                DrawSegment(canvas, seg, x, baseline, _direction);
                x += seg.Width;
            }
        }

        if (clip) canvas.Restore();
    }

    static void DrawSegment(SKCanvas canvas, Segment seg, float x, float baseline, TextDirection direction)
    {
        var style = seg.Style;
        var color = style.Color ?? SKColors.Black;

        if (style.Shadows is not null)
            foreach (var sh in style.Shadows)
            {
                using var sp = new SKPaint { Color = sh.Color, IsAntialias = true };
                if (sh.BlurRadius > 0) sp.MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, sh.BlurRadius / 2);
                DrawText(canvas, seg.Text, x + sh.Offset.Dx, baseline + sh.Offset.Dy, seg.Font, sp, style.LetterSpacing, direction);
            }

        using var paint = new SKPaint { Color = color, IsAntialias = true };
        DrawText(canvas, seg.Text, x, baseline, seg.Font, paint, style.LetterSpacing, direction);

        var deco = style.Decoration ?? TextDecoration.None;
        if (deco != TextDecoration.None)
        {
            using var lp = new SKPaint { Color = style.DecorationColor ?? color, StrokeWidth = Math.Max(1, (style.FontSize ?? 14) / 14f), IsAntialias = true };
            var m = seg.Font.Metrics;
            if (deco.HasFlag(TextDecoration.Underline)) canvas.DrawLine(x, baseline + 2, x + seg.Width, baseline + 2, lp);
            if (deco.HasFlag(TextDecoration.LineThrough)) canvas.DrawLine(x, baseline + m.Ascent * 0.35f, x + seg.Width, baseline + m.Ascent * 0.35f, lp);
            if (deco.HasFlag(TextDecoration.Overline)) canvas.DrawLine(x, baseline + m.Ascent, x + seg.Width, baseline + m.Ascent, lp);
        }
    }

    static void DrawText(SKCanvas canvas, string text, float x, float baseline, SKFont font, SKPaint paint, float? letterSpacing, TextDirection direction)
    {
        if (!TextShaper.ContainsRtl(text))
        {
            DrawRun(canvas, text, x, baseline, font, paint, letterSpacing);
            return;
        }

        // Mixed-direction text: split into directional runs and place them in visual order.
        var runs = Bidi.Analyze(text, direction);
        IEnumerable<BidiRun> ordered = direction == TextDirection.Rtl ? Enumerable.Reverse(runs) : runs;
        foreach (var run in ordered)
        {
            string s = text.Substring(run.Start, run.Length);
            DrawRun(canvas, s, x, baseline, font, paint, letterSpacing);
            x += TextShaper.MeasureShaped(s, font);
        }
    }

    static void DrawRun(SKCanvas canvas, string text, float x, float baseline, SKFont font, SKPaint paint, float? letterSpacing)
    {
        if (letterSpacing is { } ls && ls != 0 && !TextShaper.ContainsRtl(text))
        {
            foreach (char c in text)
            {
                canvas.DrawText(c.ToString(), x, baseline, SKTextAlign.Left, font, paint);
                x += font.MeasureText(c.ToString()) + ls;
            }
            return;
        }
        TextShaper.DrawShapedText(canvas, text, x, baseline, font, paint);
    }

    public override float? GetDistanceToBaseline() => _lines.Count > 0 ? _lines[0].Baseline : null;

    // --- intrinsics ---

    public override float MaxIntrinsicWidth(float height) => BuildLines(float.PositiveInfinity).Max(l => l.Width);

    public override float MinIntrinsicWidth(float height)
    {
        float widest = 0;
        foreach (var (text, style) in Runs())
        {
            var font = style.ToFont();
            foreach (var word in text.Split(' ', '\n'))
                widest = Math.Max(widest, Measure(word, style, font));
        }
        return widest;
    }

    public override float MinIntrinsicHeight(float width) => MaxIntrinsicHeight(width);

    public override float MaxIntrinsicHeight(float width)
    {
        var lines = BuildLines(width);
        float h = 0;
        foreach (var l in lines)
        {
            float lh = 0;
            foreach (var s in l.Segments) lh = Math.Max(lh, LineMetrics(s.Style, s.Font.Metrics).height);
            if (l.Segments.Count == 0) lh = LineMetrics(TextStyle.Fallback.Merge(_baseStyle), TextStyle.Fallback.Merge(_baseStyle).ToFont().Metrics).height;
            h += lh;
        }
        return h;
    }
}

static class FloatExt
{
    public static bool IsFinite(this float f) => float.IsFinite(f);
}
