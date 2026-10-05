using SkiaSharp;

namespace Sway.Widgets;

sealed class RenderIgnorePointer(bool ignoring) : RenderProxyBox
{
    public bool Ignoring { get; set; } = ignoring;
    public override bool HitTest(HitTestResult result, Offset position) => !Ignoring && base.HitTest(result, position);
}
