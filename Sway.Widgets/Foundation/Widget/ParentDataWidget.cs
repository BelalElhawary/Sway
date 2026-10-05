namespace Sway.Widgets;

/// <summary>Widget that configures the parent data of the nearest descendant render object (Expanded, Positioned...).</summary>
public abstract class ParentDataWidget(Widget child, Key? key = null) : Widget(key)
{
    public Widget Child { get; } = child;
    public abstract bool DebugIsValidParent(RenderObject parent);
    public abstract void ApplyParentData(RenderObject renderObject);
    public override Element CreateElement() => new ParentDataElement(this);
}
