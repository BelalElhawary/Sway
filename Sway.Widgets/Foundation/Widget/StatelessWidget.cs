namespace Sway.Widgets;

public abstract class StatelessWidget(Key? key = null) : Widget(key)
{
    public abstract Widget Build(BuildContext context);
    public override Element CreateElement() => new StatelessElement(this);
}
