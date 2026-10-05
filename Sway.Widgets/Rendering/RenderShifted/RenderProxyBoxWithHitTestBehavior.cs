using SkiaSharp;

namespace Sway.Widgets;

/// <summary>Proxy box that also counts as a hit target depending on <see cref="HitTestBehavior"/>.</summary>
public class RenderProxyBoxWithHitTestBehavior : RenderProxyBox
{
    public HitTestBehavior Behavior { get; set; } = HitTestBehavior.DeferToChild;

    public override bool HitTest(HitTestResult result, Offset position)
    {
        bool hitTarget = false;
        if (SizeOrNull is { } s && s.ToRect().Contains(position))
        {
            hitTarget = HitTestChildren(result, position) || Behavior == HitTestBehavior.Opaque;
            if (hitTarget || Behavior == HitTestBehavior.Translucent) result.Add(this);
        }
        return hitTarget;
    }
}
