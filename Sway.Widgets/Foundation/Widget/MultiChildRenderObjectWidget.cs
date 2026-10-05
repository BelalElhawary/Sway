namespace Sway.Widgets;

public abstract class MultiChildRenderObjectWidget(IReadOnlyList<Widget> children, Key? key = null) : RenderObjectWidget(key)
{
    public IReadOnlyList<Widget> Children { get; } = children;
    public override Element CreateElement() => new MultiChildRenderObjectElement(this);
}
