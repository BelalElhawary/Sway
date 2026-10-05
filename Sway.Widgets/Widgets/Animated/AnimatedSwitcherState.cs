using SkiaSharp;

namespace Sway.Widgets;

sealed class AnimatedSwitcherState : TickerProviderState<AnimatedSwitcher>
{
    sealed class Entry(Widget? child, AnimationController controller, CurvedAnimation animation)
    {
        public Widget? Child = child;
        public readonly AnimationController Controller = controller;
        public readonly CurvedAnimation Animation = animation;
    }

    readonly List<Entry> _entries = new();
    Entry? _current;

    public override void InitState() => _current = Add(Widget.Child, initial: true);

    Entry Add(Widget? child, bool initial)
    {
        var controller = new AnimationController(this, Widget.Duration, Widget.ReverseDuration);
        var animation = new CurvedAnimation(controller, Widget.InCurve, Widget.OutCurve.Flipped);
        var entry = new Entry(child, controller, animation);
        controller.AddListener(() => { if (Mounted) SetState(); });
        if (initial) controller.SetValue(1); else controller.Forward();
        _entries.Add(entry);
        return entry;
    }

    void Remove(Entry e)
    {
        e.Animation.Dispose();
        e.Controller.Dispose();
        _entries.Remove(e);
    }

    public override void DidUpdateWidget(AnimatedSwitcher old)
    {
        bool same = _current is not null && Widget.Child is not null && _current.Child is not null && Sway.Widgets.Widget.CanUpdate(_current.Child, Widget.Child)
            || (_current?.Child is null && Widget.Child is null);
        if (same)
        {
            _current!.Child = Widget.Child;
            return;
        }

        if (_current is { } outgoing)
        {
            outgoing.Controller.AddStatusListener(s => { if (s == AnimationStatus.Dismissed && Mounted) SetState(() => Remove(outgoing)); });
            outgoing.Controller.Reverse();
        }
        _current = Widget.Child is null ? null : Add(Widget.Child, initial: false);
    }

    public override void Dispose()
    {
        foreach (var e in _entries.ToArray()) Remove(e);
        base.Dispose();
    }

    public override Widget Build(BuildContext context)
    {
        var children = new List<Widget>();
        foreach (var e in _entries)
            if (e.Child is { } child) children.Add(new KeyedSubtree(new ObjectKey(e), Widget.Transition(child, e.Animation)));
        return new Stack(children, alignment: Widget.Alignment, clip: false);
    }
}
