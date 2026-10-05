namespace Sway.Widgets;

public abstract class LeafRenderObjectWidget(Key? key = null) : RenderObjectWidget(key)
{
    public override Element CreateElement() => new LeafRenderObjectElement(this);
}
