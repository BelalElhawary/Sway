namespace Sway.Widgets;

public sealed class Listener(Widget? child = null, Action<PointerEvent>? onPointerDown = null, Action<PointerEvent>? onPointerMove = null,
    Action<PointerEvent>? onPointerUp = null, Action<PointerEvent>? onPointerCancel = null, Action<PointerEvent>? onPointerScroll = null,
    HitTestBehavior behavior = HitTestBehavior.DeferToChild, Key? key = null) : SingleChildRenderObjectWidget(child, key)
{
    public override RenderObject CreateRenderObject(BuildContext context)
    {
        var r = new RenderPointerListener();
        Apply(r);
        return r;
    }

    public override void UpdateRenderObject(BuildContext context, RenderObject ro) => Apply((RenderPointerListener)ro);

    void Apply(RenderPointerListener r)
    {
        r.OnPointerDown = onPointerDown; r.OnPointerMove = onPointerMove; r.OnPointerUp = onPointerUp;
        r.OnPointerCancel = onPointerCancel; r.OnPointerScroll = onPointerScroll; r.Behavior = behavior;
    }
}
