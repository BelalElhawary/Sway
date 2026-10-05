namespace Sway.Widgets;

public abstract class SingleChildRenderObjectWidget(Widget? child, Key? key = null) : RenderObjectWidget(key)
{
    public Widget? Child { get; } = child;
    public override Element CreateElement() => new SingleChildRenderObjectElement(this);
}
