using SkiaSharp;

namespace Sway.Widgets;

public sealed class RenderMouseRegion : RenderProxyBoxWithHitTestBehavior
{
    public Action<PointerEvent>? OnEnter, OnExit, OnHover;
    public MouseCursor Cursor = MouseCursor.Default;
    public bool Opaque { get => Behavior == HitTestBehavior.Opaque; set => Behavior = value ? HitTestBehavior.Opaque : HitTestBehavior.Translucent; }

    public RenderMouseRegion() { Behavior = HitTestBehavior.Opaque; }
}
