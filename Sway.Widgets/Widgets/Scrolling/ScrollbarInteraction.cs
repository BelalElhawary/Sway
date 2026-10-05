using SkiaSharp;

namespace Sway.Widgets;

/// <summary>
/// Scrollbar geometry, painting and pointer handling shared by scrollable render objects. The thumb can be dragged
/// and the track clicked to page; it stays visible while the pointer is over the gutter or a drag is in progress.
/// </summary>
sealed class ScrollbarInteraction
{
    const float Thickness = 6, HoverThickness = 10, Margin = 2, Gutter = 14, Visible = 1.0f, Fade = 0.3f, MinThumb = 24;

    bool _pressed, _dragging;
    float _grab;

    static float Viewport(Size size, Axis axis) => axis == Axis.Vertical ? size.Height : size.Width;

    static (float start, float length) Thumb(Size size, Axis axis, ScrollPosition p)
    {
        float viewport = Viewport(size, axis);
        float total = viewport + p.MaxScrollExtent;
        float length = Math.Min(viewport, Math.Max(MinThumb, viewport * viewport / total));
        float travel = Math.Max(0, viewport - length - Margin * 2);
        float start = Margin + (p.MaxScrollExtent <= 0 ? 0 : travel * p.Pixels / p.MaxScrollExtent);
        return (start, length);
    }

    static bool InGutter(Size size, Axis axis, Offset local) => axis == Axis.Vertical
        ? local.Dx >= size.Width - Gutter && local.Dx < size.Width
        : local.Dy >= size.Height - Gutter && local.Dy < size.Height;

    static float Main(Offset o, Axis axis) => axis == Axis.Vertical ? o.Dy : o.Dx;

    /// <summary>The gutter swallows pointer input while the content can scroll.</summary>
    public bool HitTest(Size size, Axis axis, ScrollPosition p, Offset local) => p.CanScroll && InGutter(size, axis, local);

    public void Handle(PointerEvent e, Size size, Axis axis, ScrollPosition p)
    {
        float main = Main(e.LocalPosition, axis);
        switch (e.Kind)
        {
            // Only presses in the gutter belong to the scrollbar; the viewport also receives presses on its content.
            case PointerEventKind.Down when InGutter(size, axis, e.LocalPosition):
                _pressed = true;
                p.ScrollbarActive = true;
                p.StopActivity();
                var (start, length) = Thumb(size, axis, p);
                if (main >= start && main <= start + length)
                {
                    _dragging = true;
                    _grab = main - start;
                }
                else
                {
                    // Clicking the track pages toward the click.
                    float page = Viewport(size, axis) * 0.9f;
                    p.AnimateTo(p.Pixels + (main < start ? -page : page), TimeSpan.FromMilliseconds(200));
                }
                break;
            case PointerEventKind.Move when _dragging:
                var (_, len) = Thumb(size, axis, p);
                float travel = Math.Max(1, Viewport(size, axis) - len - Margin * 2);
                p.StopActivity();
                p.JumpTo((main - _grab - Margin) / travel * p.MaxScrollExtent);
                break;
            case PointerEventKind.Up or PointerEventKind.Cancel when _pressed:
                _pressed = false;
                _dragging = false;
                p.ScrollbarActive = false;
                break;
        }
    }

    public void Paint(SKCanvas canvas, Size size, Axis axis, ScrollPosition p, Offset offset, Action requestRepaint)
    {
        if (!p.CanScroll) return;
        var local = WidgetsBinding.Instance.Gestures.PointerPosition - offset;
        bool hovered = size.ToRect().Contains(local) && InGutter(size, axis, local);
        bool active = _dragging || hovered;

        float alpha = 1;
        if (!active)
        {
            float age = (float)(WidgetsBinding.Instance.Now - p.LastChange).TotalSeconds;
            if (age > Visible + Fade) return;
            alpha = age <= Visible ? 1 : 1 - (age - Visible) / Fade;
            // Keep frames coming until the fade finishes.
            WidgetsBinding.Instance.ScheduleFrameCallback(_ => requestRepaint());
        }

        var (pos, length) = Thumb(size, axis, p);
        float thickness = active ? HoverThickness : Thickness;
        float near = Margin + (HoverThickness - thickness) / 2;
        var rect = axis == Axis.Vertical
            ? new SKRect(offset.Dx + size.Width - thickness - near, offset.Dy + pos, offset.Dx + size.Width - near, offset.Dy + pos + length)
            : new SKRect(offset.Dx + pos, offset.Dy + size.Height - thickness - near, offset.Dx + pos + length, offset.Dy + size.Height - near);
        using var paint = new SKPaint { Color = new SKColor(0, 0, 0, (byte)((active ? 150 : 100) * alpha)), IsAntialias = true };
        canvas.DrawRoundRect(rect, thickness / 2, thickness / 2, paint);
    }
}
