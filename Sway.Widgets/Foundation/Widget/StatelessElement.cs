namespace Sway.Widgets;

public sealed class StatelessElement(StatelessWidget widget) : ComponentElement(widget)
{
    protected override Widget Build() => ((StatelessWidget)Widget).Build(this);
}
