using SkiaSharp;

namespace Sway.Widgets;

/// <summary>Render object that holds an ordered list of box children.</summary>
public abstract class RenderBoxContainer : RenderBox
{
    readonly List<RenderBox> _children = new();

    public IReadOnlyList<RenderBox> Children => _children;

    public void SetChildren(IReadOnlyList<RenderBox> children)
    {
        bool same = children.Count == _children.Count;
        for (int i = 0; same && i < children.Count; i++) same = ReferenceEquals(children[i], _children[i]);
        if (same) return;

        var keep = new HashSet<RenderBox>(children);
        foreach (var old in _children.ToArray())
            if (!keep.Contains(old)) DropChild(old);

        var had = new HashSet<RenderBox>(_children);
        _children.Clear();
        _children.AddRange(children);
        foreach (var c in children)
            if (!had.Contains(c)) AdoptChild(c);
        MarkNeedsLayout();
    }

    public override void VisitChildren(Action<RenderObject> visitor)
    {
        foreach (var c in _children) visitor(c);
    }
}
