using SkiaSharp;

namespace Sway.Widgets;

public sealed class ScrollController
{
    public ScrollPosition Position { get; } = new();
    public float Offset => Position.Pixels;
    public float MaxScrollExtent => Position.MaxScrollExtent;

    public void JumpTo(float offset) { Position.StopActivity(); Position.JumpTo(offset); }
    public void AnimateTo(float offset, TimeSpan duration, Curve? curve = null) => Position.AnimateTo(offset, duration, curve);
}
