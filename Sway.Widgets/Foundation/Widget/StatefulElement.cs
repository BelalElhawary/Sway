namespace Sway.Widgets;

public sealed class StatefulElement : ComponentElement
{
    readonly State _state;

    public StatefulElement(StatefulWidget widget) : base(widget)
    {
        _state = widget.CreateState();
        _state.Element = this;
        _state.WidgetInternal = widget;
    }

    public State State => _state;

    protected override Widget Build() => _state.Build(this);

    protected override void FirstBuild()
    {
        // InitState runs after the element is linked into the tree, so it may read inherited widgets.
        _state.InitState();
        _state.DidChangeDependencies();
        base.FirstBuild();
    }

    public override void Update(Widget newWidget)
    {
        var old = (StatefulWidget)Widget;
        _state.WidgetInternal = (StatefulWidget)newWidget;
        base.Widget = newWidget;
        _state.DidUpdateWidget(old);
        ClearDirty();
        Rebuild();
    }

    internal override void DidChangeDependencies()
    {
        _state.DidChangeDependencies();
        base.DidChangeDependencies();
    }

    public override void Unmount()
    {
        _state.Dispose();
        base.Unmount();
    }
}
