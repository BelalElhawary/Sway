namespace Sway.Widgets;

public sealed class MouseRegion(Widget? child = null, Action<PointerEvent>? onEnter = null, Action<PointerEvent>? onExit = null,
    Action<PointerEvent>? onHover = null, MouseCursor cursor = MouseCursor.Default, bool opaque = true, Key? key = null)
    : SingleChildRenderObjectWidget(child, key)
{
    public override RenderObject CreateRenderObject(BuildContext context)
    {
        var r = new RenderMouseRegion();
        Apply(r);
        return r;
    }

    public override void UpdateRenderObject(BuildContext context, RenderObject ro) => Apply((RenderMouseRegion)ro);

    void Apply(RenderMouseRegion r)
    {
        r.OnEnter = onEnter; r.OnExit = onExit; r.OnHover = onHover; r.Cursor = cursor; r.Opaque = opaque;
    }
}
