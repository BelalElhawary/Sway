namespace Sway.Widgets;

public abstract class RenderObjectWidget(Key? key = null) : Widget(key)
{
    public abstract RenderObject CreateRenderObject(BuildContext context);
    public virtual void UpdateRenderObject(BuildContext context, RenderObject renderObject) { }
    public virtual void DidUnmountRenderObject(RenderObject renderObject) { }
}
