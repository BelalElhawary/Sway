using SkiaSharp;

namespace Sway.Widgets;

public sealed class RenderLimitedBox(float maxWidth, float maxHeight) : RenderProxyBox
{
    float _maxWidth = maxWidth, _maxHeight = maxHeight;

    public void Update(float w, float h)
    {
        if (_maxWidth == w && _maxHeight == h) return;
        _maxWidth = w; _maxHeight = h;
        MarkNeedsLayout();
    }

    protected override void PerformLayout()
    {
        var c = Constraints;
        var limited = c.Copy(
            maxWidth: c.HasBoundedWidth ? c.MaxWidth : Math.Clamp(_maxWidth, c.MinWidth, float.PositiveInfinity),
            maxHeight: c.HasBoundedHeight ? c.MaxHeight : Math.Clamp(_maxHeight, c.MinHeight, float.PositiveInfinity));
        if (Child is { } child)
        {
            child.Layout(limited);
            Size = c.Constrain(child.Size);
        }
        else Size = limited.Constrain(Size.Zero);
    }
}
