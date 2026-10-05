namespace Sway.Widgets;

public abstract class StatefulWidget(Key? key = null) : Widget(key)
{
    public abstract State CreateState();
    public override Element CreateElement() => new StatefulElement(this);
}
