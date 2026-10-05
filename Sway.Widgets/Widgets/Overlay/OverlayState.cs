using SkiaSharp;

namespace Sway.Widgets;

public sealed class OverlayState : State<Overlay>
{
    readonly List<OverlayEntry> _entries = new();

    public void Insert(OverlayEntry entry)
    {
        entry.State = this;
        _entries.Add(entry);
        SetState();
    }

    internal void Remove(OverlayEntry entry)
    {
        if (_entries.Remove(entry) && Mounted) SetState();
    }

    internal void Rebuild() { if (Mounted) SetState(); }

    public override Widget Build(BuildContext context)
    {
        var children = new List<Widget> { new KeyedSubtree(new ValueKey<string>("overlay-initial"), Widget.Initial) };
        foreach (var e in _entries)
            children.Add(new KeyedSubtree(new ObjectKey(e), new Builder(e.Builder)));
        return new OverlayScope(this, new Stack(children, fit: StackFit.Expand, clip: false));
    }
}
