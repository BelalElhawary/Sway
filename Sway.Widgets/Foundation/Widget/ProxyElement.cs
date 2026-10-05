namespace Sway.Widgets;

/// <summary>An element with exactly one widget child (Inherited, ParentData).</summary>
public abstract class ProxyElement : ComponentElement
{
    protected ProxyElement(Widget widget) : base(widget) { }
    protected abstract Widget Child { get; }
    protected override Widget Build() => Child;

    public override void Update(Widget newWidget)
    {
        var old = Widget;
        base.Widget = newWidget;
        Updated(old);
        ClearDirty();
        Rebuild();
    }

    protected virtual void Updated(Widget oldWidget) { }
}
