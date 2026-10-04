using SkiaSharp;
using Sway.Core.Dom;
using Sway.Core.Layout;
using Sway.Core.Styling;

namespace Sway.Core.Rendering;

/// <summary>Transient UI state the painter needs: who has focus, caret blink phase, and the open select.</summary>
public sealed class PaintContext
{
    public ElementNode? Focused { get; init; }
    public bool CaretOn { get; init; }
    public ElementNode? OpenSelect { get; init; }
    public int PopupHover { get; init; } = -1;
    public float PopupScroll { get; init; }
}

/// <summary>Draws the insides of form controls: text, caret, selection, check marks and dropdown arrows.</summary>
public static class ControlPainter
{
    static readonly SKColor DefaultAccent = new(0, 117, 255);
    static readonly SKColor Placeholder = new(117, 117, 117);
    static readonly SKColor ControlBorder = new(118, 118, 118);
    static readonly SKColor DisabledGray = new(160, 160, 160);

    public static void Paint(SKCanvas canvas, ElementNode el, PaintContext ctx)
    {
        if (el.Style.VisibilityHidden) return;

        if (Controls.IsTextEditable(el)) PaintText(canvas, el, ctx);
        else if (Controls.IsCheckable(el)) PaintCheckable(canvas, el);
        else if (Controls.IsButtonInput(el)) PaintButtonLabel(canvas, el);
        else if (Controls.IsSelect(el)) PaintSelect(canvas, el);
    }

    // ---- text input and textarea ----

    static void PaintText(SKCanvas canvas, ElementNode el, PaintContext ctx)
    {
        var edit = TextControls.GetEdit(el);
        TextControls.EnsureLines(el, edit);

        var st = el.Style;
        var content = el.ContentRect;
        if (content.Width <= 0 || content.Height <= 0) return;

        var font = FontCache.Get(st);
        float lh = TextControls.LineHeight(el), baseline = TextControls.BaselineOffset(el);
        bool focused = ctx.Focused == el && !el.IsDisabled;
        string text = TextControls.DisplayText(el, edit);
        bool isRtl = st.Direction == Direction.Rtl;

        canvas.Save();
        canvas.ClipRect(content);

        float originX = TextControls.TextOriginX(el, edit);
        using var textPaint = new SKPaint { Color = st.Color, IsAntialias = true };

        if (text.Length == 0)
        {
            if (el.GetAttribute("placeholder") is { Length: > 0 } hint)
            {
                using var hintPaint = new SKPaint { Color = Placeholder, IsAntialias = true };
                float hintWidth = TextControls.Measure(el, hint);
                float hintX = isRtl ? content.Right - hintWidth : content.Left;
                TextShaper.DrawShapedText(canvas, hint, hintX, TextControls.LineTop(el, edit, 0) + baseline, font, hintPaint);
            }
        }

        using var selectionPaint = new SKPaint { Color = (st.AccentColor ?? DefaultAccent).WithAlpha(90) };

        for (int i = 0; i < edit.Lines.Count; i++)
        {
            var line = edit.Lines[i];
            float top = TextControls.LineTop(el, edit, i);
            if (top + lh < content.Top || top > content.Bottom) continue;

            string lineStr = line.End > line.Start ? text.Substring(line.Start, line.End - line.Start) : "";
            float lineWidth = TextControls.Measure(el, lineStr);
            float lineX = isRtl ? content.Right + edit.ScrollX - lineWidth : originX;

            if (focused && edit.HasSelection)
            {
                int from = Math.Max(edit.SelectionStart, line.Start), to = Math.Min(edit.SelectionEnd, line.End);
                bool crossesBreak = edit.SelectionEnd > line.End && line.HardBreak && i < edit.Lines.Count - 1;
                if (from < to || (from <= to && crossesBreak && edit.SelectionStart <= line.End))
                {
                    if (isRtl)
                    {
                        float dist1 = TextControls.Measure(el, text.Substring(line.Start, Math.Max(0, from - line.Start)));
                        float dist2 = TextControls.Measure(el, text.Substring(line.Start, Math.Max(0, to - line.Start)));
                        float selRight = content.Right + edit.ScrollX - dist1;
                        float selLeft = content.Right + edit.ScrollX - dist2;
                        if (crossesBreak) selLeft -= 4;
                        canvas.DrawRect(selLeft, top, Math.Max(0, selRight - selLeft), lh, selectionPaint);
                    }
                    else
                    {
                        float x1 = originX + TextControls.Measure(el, text.Substring(line.Start, Math.Max(0, from - line.Start)));
                        float x2 = originX + TextControls.Measure(el, text.Substring(line.Start, Math.Max(0, to - line.Start)));
                        if (crossesBreak) x2 += 4;
                        canvas.DrawRect(x1, top, Math.Max(0, x2 - x1), lh, selectionPaint);
                    }
                }
            }

            if (lineStr.Length > 0)
                TextShaper.DrawShapedText(canvas, lineStr, lineX, top + baseline, font, textPaint);

            if (focused && ctx.CaretOn && !edit.HasSelection && edit.LineOfIndex(edit.Caret) == i && !Controls.IsReadOnly(el))
            {
                float caretDist = TextControls.Measure(el, text.Substring(line.Start, Math.Clamp(edit.Caret, line.Start, line.End) - line.Start));
                float caretX = isRtl ? content.Right + edit.ScrollX - caretDist : originX + caretDist;
                canvas.DrawRect((float)Math.Round(caretX), top, 1, lh, textPaint);
            }
        }

        canvas.Restore();
    }

    // ---- checkbox and radio ----

    static void PaintCheckable(SKCanvas canvas, ElementNode el)
    {
        var rect = el.ContentRect;
        if (rect.Width <= 0 || rect.Height <= 0) return;

        var st = el.Style;
        bool isChecked = el.Attributes.ContainsKey("checked");
        bool isIndeterminate = !Controls.IsRadio(el) && el.Attributes.ContainsKey("indeterminate");
        bool disabled = el.IsDisabled;
        var accent = disabled ? DisabledGray : st.AccentColor ?? DefaultAccent;
        var edge = disabled ? DisabledGray : ControlBorder;
        bool filled = (isChecked || isIndeterminate) && !Controls.IsRadio(el);

        using var fill = new SKPaint { IsAntialias = true, Color = filled ? accent : SKColors.White };
        using var stroke = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1, Color = filled ? accent : edge };

        if (Controls.IsRadio(el))
        {
            float cx = rect.MidX, cy = rect.MidY, r = Math.Min(rect.Width, rect.Height) / 2 - 0.5f;
            canvas.DrawCircle(cx, cy, r, fill);
            if (isChecked)
            {
                stroke.Color = accent;
                stroke.StrokeWidth = 1.5f;
                canvas.DrawCircle(cx, cy, r - 0.25f, stroke);
                using var dot = new SKPaint { IsAntialias = true, Color = accent };
                canvas.DrawCircle(cx, cy, r * 0.5f, dot);
            }
            else canvas.DrawCircle(cx, cy, r, stroke);
            return;
        }

        var box = new SKRect(rect.Left + 0.5f, rect.Top + 0.5f, rect.Right - 0.5f, rect.Bottom - 0.5f);
        canvas.DrawRoundRect(box, 2.5f, 2.5f, fill);
        canvas.DrawRoundRect(box, 2.5f, 2.5f, stroke);

        if (isIndeterminate)
        {
            using var mark = new SKPaint
            {
                IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1.8f, Color = SKColors.White,
                StrokeCap = SKStrokeCap.Round
            };
            float w = rect.Width, h = rect.Height;
            canvas.DrawLine(rect.Left + w * 0.22f, rect.MidY, rect.Left + w * 0.78f, rect.MidY, mark);
        }
        else if (isChecked)
        {
            using var mark = new SKPaint
            {
                IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1.8f, Color = SKColors.White,
                StrokeCap = SKStrokeCap.Round, StrokeJoin = SKStrokeJoin.Round
            };
            float w = rect.Width, h = rect.Height;
            var builder = new SKPathBuilder();
            builder.MoveTo(rect.Left + w * 0.25f, rect.Top + h * 0.53f);
            builder.LineTo(rect.Left + w * 0.43f, rect.Top + h * 0.70f);
            builder.LineTo(rect.Left + w * 0.76f, rect.Top + h * 0.30f);
            using var path = builder.Detach();
            canvas.DrawPath(path, mark);
        }
    }

    // ---- input type=button | submit | reset ----

    static void PaintButtonLabel(SKCanvas canvas, ElementNode el)
    {
        var st = el.Style;
        string label = TextControls.ButtonLabel(el);
        if (label.Length == 0) return;

        var content = el.ContentRect;
        float width = TextControls.Measure(el, label);
        float x = content.MidX - width / 2;
        float y = content.Top + (content.Height - TextControls.LineHeight(el)) / 2 + TextControls.BaselineOffset(el);

        using var paint = new SKPaint { Color = st.Color, IsAntialias = true };
        TextShaper.DrawShapedText(canvas, label, x, y, FontCache.Get(st), paint);
    }

    // ---- select ----

    static void PaintSelect(SKCanvas canvas, ElementNode el)
    {
        var st = el.Style;
        var options = Controls.Options(el);
        int selected = Controls.SelectedIndex(el, options);
        var content = el.ContentRect;
        bool isRtl = st.Direction == Direction.Rtl;

        if (selected >= 0 && content.Width > 0)
        {
            canvas.Save();
            canvas.ClipRect(content);
            float y = content.Top + (content.Height - TextControls.LineHeight(el)) / 2 + TextControls.BaselineOffset(el);
            using var paint = new SKPaint { Color = st.Color, IsAntialias = true };
            string label = options[selected].Label;
            float textWidth = TextControls.Measure(el, label);
            float textX = isRtl ? content.Right - textWidth : content.Left;
            TextShaper.DrawShapedText(canvas, label, textX, y, FontCache.Get(st), paint);
            canvas.Restore();
        }

        // Chevron on the opposite edge from text start, inside the padding.
        var box = el.BorderRect;
        float cx = isRtl ? box.Left + 13 : box.Right - 13, cy = box.MidY;
        using var chevron = new SKPaint
        {
            IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1.5f, Color = st.Color.WithAlpha(210),
            StrokeCap = SKStrokeCap.Round, StrokeJoin = SKStrokeJoin.Round
        };
        var builder = new SKPathBuilder();
        builder.MoveTo(cx - 3.5f, cy - 1.8f);
        builder.LineTo(cx, cy + 1.8f);
        builder.LineTo(cx + 3.5f, cy - 1.8f);
        using var path = builder.Detach();
        canvas.DrawPath(path, chevron);
    }

    // ---- dropdown list (drawn last, above everything) ----

    public static void PaintPopup(SKCanvas canvas, ElementNode select, PaintContext ctx, float viewportHeight)
    {
        var g = SelectPopup.Compute(select, viewportHeight);
        var style = select.Style;
        var font = FontCache.Get(style);
        var (ox, oy) = SelectPopup.ScrollOffset(select);
        int selected = Controls.SelectedIndex(select, g.Options);
        bool isRtl = style.Direction == Direction.Rtl;

        canvas.Save();
        canvas.Translate(-ox, -oy);

        // A soft shadow so the list reads as floating.
        using (var shadow = new SKPaint { Color = new SKColor(0, 0, 0, 40), IsAntialias = true, MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 3) })
            canvas.DrawRoundRect(new SKRect(g.Rect.Left, g.Rect.Top + 2, g.Rect.Right, g.Rect.Bottom + 2), 4, 4, shadow);

        using (var bg = new SKPaint { Color = SKColors.White, IsAntialias = true })
            canvas.DrawRoundRect(g.Rect, 4, 4, bg);
        using (var edge = new SKPaint { Color = ControlBorder, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1 })
            canvas.DrawRoundRect(g.Rect, 4, 4, edge);

        var inner = new SKRect(g.Rect.Left + 1, g.Rect.Top + 1, g.Rect.Right - 1, g.Rect.Bottom - 1);
        canvas.ClipRect(inner);

        float baseline = TextControls.BaselineOffset(select);
        float textInset = (g.ItemHeight - TextControls.LineHeight(select)) / 2;

        for (int i = 0; i < g.Options.Count; i++)
        {
            float top = inner.Top + i * g.ItemHeight - ctx.PopupScroll;
            if (top + g.ItemHeight < inner.Top || top > inner.Bottom) continue;

            var option = g.Options[i];
            bool hover = i == ctx.PopupHover && !option.Disabled;
            if (hover)
            {
                using var highlight = new SKPaint { Color = new SKColor(59, 130, 246) };
                canvas.DrawRect(inner.Left, top, inner.Width, g.ItemHeight, highlight);
            }
            else if (i == selected)
            {
                using var current = new SKPaint { Color = new SKColor(229, 237, 255) };
                canvas.DrawRect(inner.Left, top, inner.Width, g.ItemHeight, current);
            }

            using var text = new SKPaint
            {
                IsAntialias = true,
                Color = option.Disabled ? DisabledGray : hover ? SKColors.White : SKColors.Black
            };
            float textWidth = FontCache.Measure(option.Label, style);
            float textX = isRtl ? inner.Right - 10 - textWidth : inner.Left + 10;
            TextShaper.DrawShapedText(canvas, option.Label, textX, top + textInset + baseline, font, text);
        }

        canvas.Restore();
    }
}
