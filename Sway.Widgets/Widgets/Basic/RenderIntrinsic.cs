using SkiaSharp;

namespace Sway.Widgets;

sealed class RenderIntrinsic(Axis axis) : RenderProxyBox
{
    protected override void PerformLayout()
    {
        if (Child is not { } child) { Size = Constraints.Smallest; return; }
        var c = Constraints;
        if (axis == Axis.Horizontal)
        {
            float w = c.ConstrainWidth(child.MaxIntrinsicWidth(c.MaxHeight));
            child.Layout(c.Tighten(width: w));
        }
        else
        {
            float h = c.ConstrainHeight(child.MaxIntrinsicHeight(c.MaxWidth));
            child.Layout(c.Tighten(height: h));
        }
        Size = child.Size;
    }
}
