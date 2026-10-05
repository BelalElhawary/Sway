namespace Sway.Widgets;

/// <summary>Widget whose descendants can read it with <c>context.DependOn&lt;T&gt;()</c> and rebuild when it changes.</summary>
public abstract class InheritedWidget(Widget child, Key? key = null) : Widget(key)
{
    public Widget Child { get; } = child;
    public abstract bool UpdateShouldNotify(InheritedWidget oldWidget);
    public override Element CreateElement() => new InheritedElement(this);
}
