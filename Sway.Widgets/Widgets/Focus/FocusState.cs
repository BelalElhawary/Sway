namespace Sway.Widgets;

sealed class FocusState : State<Focus>
{
    FocusNode? _owned;
    FocusNode _node = null!;
    bool _hadFocus;

    public override void InitState()
    {
        Attach();
        if (Widget.Autofocus)
            WidgetsBinding.Instance.ScheduleFrameCallback(_ => { if (Mounted) _node.RequestFocus(); });
    }

    void Attach()
    {
        _node = Widget.Node ?? (_owned ??= new FocusNode());
        _node.Element = (Element?)Context;
        _node.CanRequestFocus = Widget.CanRequestFocus;
        _node.SkipTraversal = Widget.SkipTraversal;
        _node.TrapsFocus = Widget.TrapFocus;
        _node.Order = FocusTraversalOrder.Of(Context);
        _node.Parent = Focus.MaybeOf(Context);
        _node.Changed += OnChanged;
        WidgetsBinding.Instance.Focus.Register(_node);
        if (Widget.OnKey is not null) _node.OnKey = Widget.OnKey;
        _hadFocus = _node.HasFocus;
    }

    void Detach()
    {
        _node.Changed -= OnChanged;
        WidgetsBinding.Instance.Focus.Unregister(_node);
        _node.Element = null;
    }

    void OnChanged()
    {
        bool has = _node.HasFocus;
        if (has == _hadFocus) return;
        _hadFocus = has;
        Widget.OnFocusChange?.Invoke(has);
        if (Mounted) SetState();
    }

    public override void DidUpdateWidget(Focus old)
    {
        if (!ReferenceEquals(old.Node, Widget.Node) && !(old.Node is null && Widget.Node is null))
        {
            Detach();
            Attach();
        }
        else
        {
            _node.CanRequestFocus = Widget.CanRequestFocus;
            _node.SkipTraversal = Widget.SkipTraversal;
            _node.TrapsFocus = Widget.TrapFocus;
            _node.Order = FocusTraversalOrder.Of(Context);
            if (Widget.OnKey is not null) _node.OnKey = Widget.OnKey;
        }
    }

    public override void Dispose() => Detach();

    public override Widget Build(BuildContext context) =>
        new FocusScopeMarker(_node, _node.HasFocus, Widget.Child);
}
