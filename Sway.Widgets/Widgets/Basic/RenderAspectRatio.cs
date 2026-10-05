using SkiaSharp;

namespace Sway.Widgets;

sealed class RenderAspectRatio(float ratio) : RenderProxyBox
{
    float _ratio = ratio;
    public float Ratio { set { if (_ratio == value) return; _ratio = value; MarkNeedsLayout(); } }

    protected override void PerformLayout()
    {
        var c = Constraints;
        float w = c.MaxWidth, h;
        if (float.IsFinite(w)) h = w / _ratio;
        else { h = c.MaxHeight; w = h * _ratio; }
        var size = c.Constrain(new Size(w, h));
        // Re-derive the other side when constraints clamped one of them.
        if (Math.Abs(size.Width / size.Height - _ratio) > 0.001f)
            size = size.Width / _ratio <= c.MaxHeight ? c.Constrain(new Size(size.Width, size.Width / _ratio)) : c.Constrain(new Size(size.Height * _ratio, size.Height));
        Child?.Layout(BoxConstraints.Tight(size));
        Size = size;
    }
}
