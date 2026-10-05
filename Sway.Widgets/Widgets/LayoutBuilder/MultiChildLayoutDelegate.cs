namespace Sway.Widgets;

/// <summary>
/// Decides the size of a <see cref="CustomMultiChildLayout"/> and where each child goes. Inside
/// <see cref="PerformLayout"/>, call <see cref="LayoutChild"/> then <see cref="PositionChild"/> for each child id.
/// </summary>
public abstract class MultiChildLayoutDelegate
{
    Dictionary<object, RenderBox>? _children;
    HashSet<object>? _laidOut;

    /// <summary>Chooses the layout's own size from the incoming constraints.</summary>
    public virtual Size GetSize(BoxConstraints constraints) => constraints.Biggest;

    public abstract void PerformLayout(Size size);

    /// <summary>Whether a replacement delegate needs another layout pass.</summary>
    public virtual bool ShouldRelayout(MultiChildLayoutDelegate oldDelegate) => true;

    public bool HasChild(object id) => _children?.ContainsKey(id) == true;

    public Size LayoutChild(object id, BoxConstraints constraints)
    {
        var child = Find(id);
        if (!_laidOut!.Add(id)) throw new InvalidOperationException($"Child '{id}' was laid out twice.");
        child.Layout(constraints);
        return child.Size;
    }

    public void PositionChild(object id, Offset offset)
    {
        var child = Find(id);
        if (!_laidOut!.Contains(id)) throw new InvalidOperationException($"Child '{id}' must be laid out before it is positioned.");
        ((BoxParentData)child.ParentData!).Offset = offset;
    }

    RenderBox Find(object id) =>
        _children is not null && _children.TryGetValue(id, out var c) ? c : throw new InvalidOperationException($"No child has the layout id '{id}'.");

    internal void Run(RenderCustomMultiChildLayout owner, Size size)
    {
        _children = new();
        _laidOut = new();
        foreach (var child in owner.Children)
        {
            if (child.ParentData is not MultiChildLayoutParentData { Id: { } id })
                throw new InvalidOperationException("Every child of CustomMultiChildLayout must be wrapped in a LayoutId.");
            if (!_children.TryAdd(id, child)) throw new InvalidOperationException($"Duplicate layout id '{id}'.");
        }
        PerformLayout(size);
        _children = null;
        _laidOut = null;
    }
}
