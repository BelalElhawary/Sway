using SkiaSharp;
using Sway.Core.Dom;
using Sway.Core.Layout;
using Sway.Core.Styling;

namespace Sway.Core.Rendering;

/// <summary>Executes a display list against a Skia canvas.</summary>
public sealed class Painter
{
    public void Paint(SKCanvas canvas, Document document, IReadOnlyList<PaintOp> ops, PaintContext context)
    {
        var root = document.Root;
        // As in CSS, the root element's background fills the whole canvas.
        canvas.Clear(root.Style.BackgroundColor.Alpha == 0 ? SKColors.White : root.Style.BackgroundColor);

        foreach (var op in ops)
        {
            switch (op.Kind)
            {
                case OpKind.Box: PaintBox(canvas, op.Element!, context); break;
                case OpKind.Popup: ControlPainter.PaintPopup(canvas, op.Element!, context, document.ViewportHeight); break;
                case OpKind.Text: PaintText(canvas, op.Text!); break;
                case OpKind.PushScroll: PushScroll(canvas, op.Element!); break;
                case OpKind.PushTransform:
                    canvas.Save();
                    canvas.Concat(Affine.OfElement(op.Element!).ToSkia());
                    break;
                case OpKind.PushEffects: PushEffects(canvas, op.Element!.Style); break;
                case OpKind.PopScroll or OpKind.PopTransform or OpKind.PopEffects: canvas.Restore(); break;
                case OpKind.Scrollbars: PaintScrollbars(canvas, op.Element!); break;
            }
        }
    }

    // ---- shapes ----

    /// <summary>
    /// A rounded rectangle with per-corner radii, optionally shrunk by <paramref name="inset"/> on every side.
    /// Radii that would overlap are scaled down together, as CSS specifies.
    /// </summary>
    public static SKRoundRect RoundRect(SKRect rect, float[] radii, float inset = 0)
    {
        var box = new SKRect(rect.Left + inset, rect.Top + inset, rect.Right - inset, rect.Bottom - inset);
        float w = Math.Max(0, box.Width), h = Math.Max(0, box.Height);

        var r = radii.Select(v => Math.Max(0, v - inset)).ToArray();
        float scale = 1;
        void Fit(float length, float sum) { if (sum > length && sum > 0) scale = Math.Min(scale, length / sum); }
        Fit(w, r[0] + r[1]);
        Fit(h, r[1] + r[2]);
        Fit(w, r[3] + r[2]);
        Fit(h, r[0] + r[3]);

        var result = new SKRoundRect();
        result.SetRectRadii(new SKRect(box.Left, box.Top, box.Left + w, box.Top + h), new[]
        {
            new SKPoint(r[0] * scale, r[0] * scale), new SKPoint(r[1] * scale, r[1] * scale),
            new SKPoint(r[2] * scale, r[2] * scale), new SKPoint(r[3] * scale, r[3] * scale),
        });
        return result;
    }

    static void PushScroll(SKCanvas canvas, ElementNode el)
    {
        canvas.Save();
        float border = (el.Style.BorderWidth[0] + el.Style.BorderWidth[1] + el.Style.BorderWidth[2] + el.Style.BorderWidth[3]) / 4;
        var box = el.BorderRect;
        using var clip = RoundRect(el.PaddingBox, el.Style.EffectiveRadii(box.Width, box.Height).Select(r => Math.Max(0, r - border)).ToArray());
        canvas.ClipRoundRect(clip, SKClipOperation.Intersect, antialias: true);
        canvas.Translate(-el.ScrollX, -el.ScrollY);
    }

    // ---- opacity and filters ----

    static void PushEffects(SKCanvas canvas, ComputedStyle st)
    {
        using var paint = new SKPaint { Color = SKColors.White.WithAlpha((byte)Math.Round(st.Opacity * 255)) };

        SKImageFilter? filter = null;
        foreach (var op in st.Filters) filter = ApplyFilter(op, filter);
        paint.ImageFilter = filter;

        canvas.SaveLayer(paint);
        filter?.Dispose();
    }

    static SKImageFilter ApplyFilter(FilterOp op, SKImageFilter? input)
    {
        switch (op.Kind)
        {
            case FilterKind.Blur:
                return SKImageFilter.CreateBlur(op.Amount, op.Amount, input);
            case FilterKind.DropShadow:
                return SKImageFilter.CreateDropShadow(op.X, op.Y, op.Amount / 2, op.Amount / 2, op.Color, input);
            default:
                using (var color = SKColorFilter.CreateColorMatrix(ColorMatrix(op)))
                    return SKImageFilter.CreateColorFilter(color, input);
        }
    }

    // 4x5 row-major matrices from the CSS Filter Effects spec; the fifth column is a 0..1 offset.
    static float[] ColorMatrix(FilterOp op)
    {
        float a = op.Amount;
        switch (op.Kind)
        {
            case FilterKind.Brightness:
                return new[] { a, 0, 0, 0, 0, 0, a, 0, 0, 0, 0, 0, a, 0, 0, 0, 0, 0, 1, 0 };
            case FilterKind.Contrast:
            {
                float o = 0.5f * (1 - a);
                return new[] { a, 0, 0, 0, o, 0, a, 0, 0, o, 0, 0, a, 0, o, 0, 0, 0, 1, 0 };
            }
            case FilterKind.Invert:
            {
                float s = 1 - 2 * Math.Min(a, 1), o = Math.Min(a, 1);
                return new[] { s, 0, 0, 0, o, 0, s, 0, 0, o, 0, 0, s, 0, o, 0, 0, 0, 1, 0 };
            }
            case FilterKind.Opacity:
                return new[] { 1, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, Math.Min(a, 1), 0 };
            case FilterKind.Grayscale:
            {
                float g = 1 - Math.Min(a, 1);
                return new[]
                {
                    0.2126f + 0.7874f * g, 0.7152f - 0.7152f * g, 0.0722f - 0.0722f * g, 0, 0,
                    0.2126f - 0.2126f * g, 0.7152f + 0.2848f * g, 0.0722f - 0.0722f * g, 0, 0,
                    0.2126f - 0.2126f * g, 0.7152f - 0.7152f * g, 0.0722f + 0.9278f * g, 0, 0,
                    0, 0, 0, 1, 0
                };
            }
            case FilterKind.Sepia:
            {
                float s = 1 - Math.Min(a, 1);
                return new[]
                {
                    0.393f + 0.607f * s, 0.769f - 0.769f * s, 0.189f - 0.189f * s, 0, 0,
                    0.349f - 0.349f * s, 0.686f + 0.314f * s, 0.168f - 0.168f * s, 0, 0,
                    0.272f - 0.272f * s, 0.534f - 0.534f * s, 0.131f + 0.869f * s, 0, 0,
                    0, 0, 0, 1, 0
                };
            }
            case FilterKind.Saturate:
                return new[]
                {
                    0.213f + 0.787f * a, 0.715f - 0.715f * a, 0.072f - 0.072f * a, 0, 0,
                    0.213f - 0.213f * a, 0.715f + 0.285f * a, 0.072f - 0.072f * a, 0, 0,
                    0.213f - 0.213f * a, 0.715f - 0.715f * a, 0.072f + 0.928f * a, 0, 0,
                    0, 0, 0, 1, 0
                };
            default: // hue-rotate
            {
                float r = a * MathF.PI / 180f, cos = MathF.Cos(r), sin = MathF.Sin(r);
                return new[]
                {
                    0.213f + cos * 0.787f - sin * 0.213f, 0.715f - cos * 0.715f - sin * 0.715f, 0.072f - cos * 0.072f + sin * 0.928f, 0, 0,
                    0.213f - cos * 0.213f + sin * 0.143f, 0.715f + cos * 0.285f + sin * 0.140f, 0.072f - cos * 0.072f - sin * 0.283f, 0, 0,
                    0.213f - cos * 0.213f - sin * 0.787f, 0.715f - cos * 0.715f + sin * 0.715f, 0.072f + cos * 0.928f + sin * 0.072f, 0, 0,
                    0, 0, 0, 1, 0
                };
            }
        }
    }

    // ---- boxes ----

    static void PaintBox(SKCanvas canvas, ElementNode el, PaintContext context)
    {
        var st = el.Style;
        if (st.VisibilityHidden) return;

        var rect = el.BorderRect;
        if (rect.Width <= 0 || rect.Height <= 0) return;

        var radii = st.EffectiveRadii(rect.Width, rect.Height);
        using var shape = RoundRect(rect, radii);

        // Outer shadows first, bottom layer (the last one listed) first.
        for (int i = st.BoxShadows.Count - 1; i >= 0; i--)
            if (!st.BoxShadows[i].Inset) PaintOuterShadow(canvas, rect, radii, st.BoxShadows[i], shape);

        if (st.BackgroundColor.Alpha > 0)
        {
            using var paint = new SKPaint { Color = st.BackgroundColor, IsAntialias = true };
            canvas.DrawRoundRect(shape, paint);
        }

        // The first gradient listed is the topmost, so paint the list backwards.
        for (int i = st.BackgroundGradients.Count - 1; i >= 0; i--)
        {
            using var shader = GradientShader.Create(st.BackgroundGradients[i], rect);
            if (shader is null) continue;
            using var paint = new SKPaint { Shader = shader, IsAntialias = true };
            canvas.DrawRoundRect(shape, paint);
        }

        for (int i = st.BoxShadows.Count - 1; i >= 0; i--)
            if (st.BoxShadows[i].Inset) PaintInsetShadow(canvas, rect, st, radii, st.BoxShadows[i]);

        PaintBorders(canvas, st, rect, radii);
        if (Controls.IsReplaced(el)) ControlPainter.Paint(canvas, el, context);

        if (st.OutlineWidth > 0)
        {
            // The outline sits outside the border box and never affects layout.
            float grow = st.OutlineOffset + st.OutlineWidth / 2;
            var outline = new SKRect(rect.Left - grow, rect.Top - grow, rect.Right + grow, rect.Bottom + grow);
            using var paint = new SKPaint
            {
                Color = st.OutlineColor ?? st.Color, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = st.OutlineWidth
            };
            float radius = radii.Max();
            float r = radius > 0 ? radius + grow : 0;
            canvas.DrawRoundRect(outline, r, r, paint);
        }
    }

    // A blurred copy of the (spread, offset) shape, kept out from under the element itself.
    static void PaintOuterShadow(SKCanvas canvas, SKRect rect, float[] baseRadii, Shadow shadow, SKRoundRect elementShape)
    {
        if (shadow.Color.Alpha == 0) return;

        var spreadRect = new SKRect(rect.Left - shadow.Spread, rect.Top - shadow.Spread, rect.Right + shadow.Spread, rect.Bottom + shadow.Spread);
        var radii = baseRadii.Select(r => r > 0 ? Math.Max(0, r + shadow.Spread) : 0).ToArray();
        using var shape = RoundRect(spreadRect, radii);

        canvas.Save();
        canvas.ClipRoundRect(elementShape, SKClipOperation.Difference, antialias: true);
        canvas.Translate(shadow.X, shadow.Y);

        using var paint = new SKPaint { Color = shadow.Color, IsAntialias = true };
        if (shadow.Blur > 0) paint.MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, shadow.Blur / 2);
        canvas.DrawRoundRect(shape, paint);
        canvas.Restore();
    }

    // An inset shadow is the area outside a shrunken, offset hole, clipped to the padding box.
    static void PaintInsetShadow(SKCanvas canvas, SKRect rect, ComputedStyle st, float[] baseRadii, Shadow shadow)
    {
        if (shadow.Color.Alpha == 0) return;

        float border = (st.BorderWidth[0] + st.BorderWidth[1] + st.BorderWidth[2] + st.BorderWidth[3]) / 4;
        using var padding = RoundRect(rect, baseRadii, inset: border);
        var bounds = padding.Rect;

        var holeRect = new SKRect(bounds.Left + shadow.X + shadow.Spread, bounds.Top + shadow.Y + shadow.Spread,
            bounds.Right + shadow.X - shadow.Spread, bounds.Bottom + shadow.Y - shadow.Spread);
        var holeRadii = baseRadii.Select(r => Math.Max(0, r - border - shadow.Spread)).ToArray();
        using var hole = RoundRect(holeRect, holeRadii);

        float margin = shadow.Blur * 2 + Math.Abs(shadow.X) + Math.Abs(shadow.Y) + shadow.Spread + 2;
        using var outer = new SKRoundRect(new SKRect(bounds.Left - margin, bounds.Top - margin, bounds.Right + margin, bounds.Bottom + margin));

        // Opposite winding directions cut the hole out of the frame.
        var builder = new SKPathBuilder();
        builder.AddRoundRect(outer, SKPathDirection.Clockwise);
        builder.AddRoundRect(hole, SKPathDirection.CounterClockwise);
        using var frame = builder.Detach();

        canvas.Save();
        canvas.ClipRoundRect(padding, SKClipOperation.Intersect, antialias: true);
        using var paint = new SKPaint { Color = shadow.Color, IsAntialias = true };
        if (shadow.Blur > 0) paint.MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, shadow.Blur / 2);
        canvas.DrawPath(frame, paint);
        canvas.Restore();
    }

    static void PaintBorders(SKCanvas canvas, ComputedStyle st, SKRect rect, float[] radii)
    {
        var w = st.BorderWidth;
        if (w[0] <= 0 && w[1] <= 0 && w[2] <= 0 && w[3] <= 0) return;

        bool uniform = w[0] == w[1] && w[1] == w[2] && w[2] == w[3]
            && Enumerable.Range(1, 3).All(i => st.BorderColorOf(i) == st.BorderColorOf(0));

        if (uniform)
        {
            // Stroke along the centre line of the border so the outer edge matches the box.
            using var paint = new SKPaint
            {
                Color = st.BorderColorOf(0), IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = w[0]
            };
            using var centre = RoundRect(rect, radii, inset: w[0] / 2);
            canvas.DrawRoundRect(centre, paint);
            return;
        }

        PaintMixedBorders(canvas, st, rect, radii);
    }

    // Borders whose sides differ: clip to the rounded ring, then fill four mitred wedges, one per side.
    static void PaintMixedBorders(SKCanvas canvas, ComputedStyle st, SKRect rect, float[] radii)
    {
        var w = st.BorderWidth;
        float top = w[ComputedStyle.Top], right = w[ComputedStyle.Right], bottom = w[ComputedStyle.Bottom], left = w[ComputedStyle.Left];

        var innerRect = new SKRect(rect.Left + left, rect.Top + top, rect.Right - right, rect.Bottom - bottom);
        // Each inner corner radius shrinks by the thicker of the two borders meeting there.
        var innerRadii = new[]
        {
            Math.Max(0, radii[0] - Math.Max(top, left)), Math.Max(0, radii[1] - Math.Max(top, right)),
            Math.Max(0, radii[2] - Math.Max(bottom, right)), Math.Max(0, radii[3] - Math.Max(bottom, left))
        };

        using var outer = RoundRect(rect, radii);
        using var inner = RoundRect(innerRect, innerRadii);

        canvas.Save();
        canvas.ClipRoundRect(outer, SKClipOperation.Intersect, antialias: true);
        if (innerRect.Width > 0 && innerRect.Height > 0) canvas.ClipRoundRect(inner, SKClipOperation.Difference, antialias: true);

        void Wedge(int side, params SKPoint[] points)
        {
            if (w[side] <= 0) return;
            var builder = new SKPathBuilder();
            builder.MoveTo(points[0].X, points[0].Y);
            for (int i = 1; i < points.Length; i++) builder.LineTo(points[i].X, points[i].Y);
            builder.Close();
            using var path = builder.Detach();
            using var paint = new SKPaint { Color = st.BorderColorOf(side), IsAntialias = true };
            canvas.DrawPath(path, paint);
        }

        var (l, t, r, b) = (rect.Left, rect.Top, rect.Right, rect.Bottom);
        var (il, it, ir, ib) = (innerRect.Left, innerRect.Top, innerRect.Right, innerRect.Bottom);
        Wedge(ComputedStyle.Top, new(l, t), new(r, t), new(ir, it), new(il, it));
        Wedge(ComputedStyle.Right, new(r, t), new(r, b), new(ir, ib), new(ir, it));
        Wedge(ComputedStyle.Bottom, new(r, b), new(l, b), new(il, ib), new(ir, ib));
        Wedge(ComputedStyle.Left, new(l, b), new(l, t), new(il, it), new(il, ib));
        canvas.Restore();
    }

    // ---- text ----

    static void PaintText(SKCanvas canvas, TextNode text)
    {
        if (text.Runs.Count == 0) return;
        var style = text.ParentElement!.Style;
        if (style.VisibilityHidden) return;

        var font = FontCache.Get(style);
        using var paint = new SKPaint { Color = style.Color, IsAntialias = true };

        foreach (var run in text.Runs)
        {
            // Shadows go underneath, last-listed first.
            for (int i = style.TextShadows.Count - 1; i >= 0; i--)
            {
                var shadow = style.TextShadows[i];
                using var shadowPaint = new SKPaint { Color = shadow.Color, IsAntialias = true };
                if (shadow.Blur > 0) shadowPaint.MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, shadow.Blur / 2);
                canvas.DrawText(run.Text, run.X + shadow.X, run.Baseline + shadow.Y, SKTextAlign.Left, font, shadowPaint);
            }

            canvas.DrawText(run.Text, run.X, run.Baseline, SKTextAlign.Left, font, paint);
            if (style.TextDecoration != TextDecoration.None) PaintDecoration(canvas, style, font, run, paint);
        }
    }

    static void PaintDecoration(SKCanvas canvas, ComputedStyle style, SKFont font, TextRun run, SKPaint paint)
    {
        float thickness = Math.Max(1, style.FontSize / 14f);
        if (style.TextDecoration.HasFlag(TextDecoration.Underline))
        {
            float y = run.Baseline + Math.Max(1.5f, font.Metrics.UnderlinePosition ?? style.FontSize * 0.1f);
            canvas.DrawRect(run.X, y, run.Width, thickness, paint);
        }
        if (style.TextDecoration.HasFlag(TextDecoration.LineThrough))
        {
            float y = run.Baseline + font.Metrics.Ascent * 0.32f;
            canvas.DrawRect(run.X, y, run.Width, thickness, paint);
        }
    }

    /// <summary>Thin overlay thumbs, shown whenever there is something to scroll to.</summary>
    static void PaintScrollbars(SKCanvas canvas, ElementNode el)
    {
        if (el.Parent is not null && el.Style.VisibilityHidden) return;

        using var paint = new SKPaint { Color = new SKColor(128, 128, 128, 150), IsAntialias = true };

        if (Scrollbars.VerticalThumb(el) is { } v)
            canvas.DrawRoundRect(v, Scrollbars.Thickness / 2, Scrollbars.Thickness / 2, paint);

        if (Scrollbars.HorizontalThumb(el) is { } h)
            canvas.DrawRoundRect(h, Scrollbars.Thickness / 2, Scrollbars.Thickness / 2, paint);
    }
}
