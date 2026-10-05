using SkiaSharp;

namespace Sway.Widgets;

public sealed class RenderPointerListener : RenderProxyBoxWithHitTestBehavior
{
    public Action<PointerEvent>? OnPointerDown, OnPointerMove, OnPointerUp, OnPointerCancel, OnPointerScroll;

    public override void HandlePointerEvent(PointerEvent e, HitTestEntry entry)
    {
        switch (e.Kind)
        {
            case PointerEventKind.Down: OnPointerDown?.Invoke(e); break;
            case PointerEventKind.Move: OnPointerMove?.Invoke(e); break;
            case PointerEventKind.Up: OnPointerUp?.Invoke(e); break;
            case PointerEventKind.Cancel: OnPointerCancel?.Invoke(e); break;
            case PointerEventKind.Scroll: OnPointerScroll?.Invoke(e); break;
        }
    }
}
