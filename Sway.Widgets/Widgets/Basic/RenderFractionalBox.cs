using SkiaSharp;

namespace Sway.Widgets;

sealed class RenderFractionalBox(float? widthFactor, float? heightFactor) : RenderProxyBox
{
    float? _w = widthFactor, _h = heightFactor;

    public void Update(float? w, float? h)
    {
        if (_w == w && _h == h) return;
        _w = w; _h = h;
        MarkNeedsLayout();
    }

    protected override void PerformLayout()
    {
        var c = Constraints;
        var inner = new BoxConstraints(
            _w is { } w ? c.MaxWidth * w : c.MinWidth, _w is { } w2 ? c.MaxWidth * w2 : c.MaxWidth,
            _h is { } h ? c.MaxHeight * h : c.MinHeight, _h is { } h2 ? c.MaxHeight * h2 : c.MaxHeight);
        if (Child is { } child)
        {
            child.Layout(inner.Enforce(c));
            Size = c.Constrain(child.Size);
        }
        else Size = c.Constrain(inner.Smallest);
    }
}
