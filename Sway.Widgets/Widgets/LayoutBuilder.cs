namespace Sway.Widgets;

/// <summary>Builds its child from the constraints its parent gives it, at layout time.</summary>
public sealed class LayoutBuilder(Func<BuildContext, BoxConstraints, Widget> builder, Key? key = null) : RenderObjectWidget(key)
{
    internal Func<BuildContext, BoxConstraints, Widget> Builder => builder;

    public override Element CreateElement() => new LayoutBuilderElement(this);
    public override RenderObject CreateRenderObject(BuildContext context) => new RenderLayoutBuilder();
}

sealed class LayoutBuilderElement : RenderObjectElement
{
    Element? _child;
    BoxConstraints? _builtFor;
    bool _needsBuild = true;

    public LayoutBuilderElement(LayoutBuilder widget) : base(widget) { }

    RenderLayoutBuilder RO => (RenderLayoutBuilder)RenderObject;

    public override void Mount(Element? parent)
    {
        base.Mount(parent);
        RO.BuildChild = BuildChild;
    }

    public override void Update(Widget newWidget)
    {
        base.Update(newWidget);
        _needsBuild = true;
        RO.MarkNeedsLayout();
    }

    internal override void DidChangeDependencies()
    {
        // The builder may read inherited widgets (themes, media queries); rebuild it on the next layout.
        _needsBuild = true;
        RO.MarkNeedsLayout();
    }

    void BuildChild(BoxConstraints constraints)
    {
        if (!_needsBuild && _builtFor == constraints) return;
        _needsBuild = false;
        _builtFor = constraints;
        _child = UpdateChild(_child, ((LayoutBuilder)Widget).Builder(this, constraints));
        ResyncChildren();
    }

    internal override void ResyncChildren() => RO.Child = _child?.FindRenderObject() as RenderBox;

    public override void VisitChildren(Action<Element> visitor)
    {
        if (_child is not null) visitor(_child);
    }
}

/// <summary>Asks its element to build the child during layout, passing the incoming constraints.</summary>
public sealed class RenderLayoutBuilder : RenderProxyBox
{
    public Action<BoxConstraints>? BuildChild;

    protected override void PerformLayout()
    {
        BuildChild?.Invoke(Constraints);
        base.PerformLayout();
    }

    // The child does not exist until layout, so it cannot be measured ahead of it.
    public override float MinIntrinsicWidth(float h) => 0;
    public override float MaxIntrinsicWidth(float h) => 0;
    public override float MinIntrinsicHeight(float w) => 0;
    public override float MaxIntrinsicHeight(float w) => 0;
}

/// <summary>
/// Lays its child out with different constraints than it received, letting it overflow the parent.
/// Null limits keep the incoming constraint on that side.
/// </summary>
public sealed class OverflowBox(Widget? child = null, IAlignment? alignment = null, float? minWidth = null, float? maxWidth = null,
    float? minHeight = null, float? maxHeight = null, Key? key = null) : SingleChildRenderObjectWidget(child, key)
{
    BoxConstraints? Limits => minWidth is null && maxWidth is null && minHeight is null && maxHeight is null
        ? null : new BoxConstraints(minWidth ?? 0, maxWidth ?? float.PositiveInfinity, minHeight ?? 0, maxHeight ?? float.PositiveInfinity);

    Alignment Resolve(BuildContext c) => (alignment ?? Alignment.Center).Resolve(Directionality.Of(c));

    public override RenderObject CreateRenderObject(BuildContext context) =>
        new RenderOverflowBox(Resolve(context), minWidth, maxWidth, minHeight, maxHeight);

    public override void UpdateRenderObject(BuildContext context, RenderObject ro) =>
        ((RenderOverflowBox)ro).Update(Resolve(context), minWidth, maxWidth, minHeight, maxHeight);
}

public sealed class RenderOverflowBox(Alignment alignment, float? minWidth, float? maxWidth, float? minHeight, float? maxHeight)
    : RenderObjectWithChildBox
{
    Alignment _alignment = alignment;
    float? _minWidth = minWidth, _maxWidth = maxWidth, _minHeight = minHeight, _maxHeight = maxHeight;

    public void Update(Alignment alignment, float? minWidth, float? maxWidth, float? minHeight, float? maxHeight)
    {
        if (_alignment == alignment && _minWidth == minWidth && _maxWidth == maxWidth && _minHeight == minHeight && _maxHeight == maxHeight) return;
        _alignment = alignment; _minWidth = minWidth; _maxWidth = maxWidth; _minHeight = minHeight; _maxHeight = maxHeight;
        MarkNeedsLayout();
    }

    BoxConstraints ChildConstraints() => new(
        _minWidth ?? Constraints.MinWidth, _maxWidth ?? Constraints.MaxWidth,
        _minHeight ?? Constraints.MinHeight, _maxHeight ?? Constraints.MaxHeight);

    protected override void PerformLayout()
    {
        // The box itself takes the largest size its parent allows, like Align without shrink-wrapping.
        Size = Constraints.Biggest;
        if (Child is not { } c) return;
        c.Layout(ChildConstraints());
        SetOffset(c, _alignment.AlongSize(Size, c.Size));
    }

    public override void Paint(PaintingContext context, Offset offset)
    {
        if (Child is { } c) context.PaintChild(c, offset + OffsetOf(c));
    }

    protected override bool HitTestChildren(HitTestResult result, Offset position) =>
        Child is { } c && HitTestChild(c, result, position);
}

/// <summary>A box of a fixed size whose child is laid out with the parent's constraints and may overflow it.</summary>
public sealed class SizedOverflowBox(Size size, Widget? child = null, IAlignment? alignment = null, Key? key = null)
    : SingleChildRenderObjectWidget(child, key)
{
    Alignment Resolve(BuildContext c) => (alignment ?? Alignment.Center).Resolve(Directionality.Of(c));

    public override RenderObject CreateRenderObject(BuildContext context) => new RenderSizedOverflowBox(Resolve(context), size);

    public override void UpdateRenderObject(BuildContext context, RenderObject ro) =>
        ((RenderSizedOverflowBox)ro).Update(Resolve(context), size);
}

public sealed class RenderSizedOverflowBox(Alignment alignment, Size requestedSize) : RenderObjectWithChildBox
{
    Alignment _alignment = alignment;
    Size _requestedSize = requestedSize;

    public void Update(Alignment alignment, Size size)
    {
        if (_alignment == alignment && _requestedSize == size) return;
        _alignment = alignment; _requestedSize = size;
        MarkNeedsLayout();
    }

    protected override void PerformLayout()
    {
        Size = Constraints.Constrain(_requestedSize);
        if (Child is not { } c) return;
        c.Layout(Constraints);
        SetOffset(c, _alignment.AlongSize(Size, c.Size));
    }

    public override void Paint(PaintingContext context, Offset offset)
    {
        if (Child is { } c) context.PaintChild(c, offset + OffsetOf(c));
    }

    protected override bool HitTestChildren(HitTestResult result, Offset position) =>
        Child is { } c && HitTestChild(c, result, position);

    public override float? GetDistanceToBaseline() => Child?.GetDistanceToBaseline() + (Child is { } c ? OffsetOf(c).Dy : 0);
}

public sealed class MultiChildLayoutParentData : BoxParentData
{
    public object? Id;
}

/// <summary>Tags a child of <see cref="CustomMultiChildLayout"/> with the id its delegate lays it out by.</summary>
public sealed class LayoutId(object id, Widget child, Key? key = null) : ParentDataWidget(child, key)
{
    public object Id => id;

    public override bool DebugIsValidParent(RenderObject parent) => parent is RenderCustomMultiChildLayout;

    public override void ApplyParentData(RenderObject ro)
    {
        if (ro.ParentData is not MultiChildLayoutParentData pd) ro.ParentData = pd = new MultiChildLayoutParentData();
        if (Equals(pd.Id, id)) return;
        pd.Id = id;
        ro.Parent?.MarkNeedsLayout();
    }
}

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

/// <summary>Lays out children with a <see cref="MultiChildLayoutDelegate"/>; each child must be a <see cref="LayoutId"/>.</summary>
public sealed class CustomMultiChildLayout(MultiChildLayoutDelegate layoutDelegate, IReadOnlyList<Widget> children, Key? key = null)
    : MultiChildRenderObjectWidget(children, key)
{
    public override RenderObject CreateRenderObject(BuildContext context) => new RenderCustomMultiChildLayout(layoutDelegate);

    public override void UpdateRenderObject(BuildContext context, RenderObject ro) =>
        ((RenderCustomMultiChildLayout)ro).Delegate = layoutDelegate;
}

public sealed class RenderCustomMultiChildLayout(MultiChildLayoutDelegate layoutDelegate) : RenderBoxContainer
{
    MultiChildLayoutDelegate _delegate = layoutDelegate;

    public MultiChildLayoutDelegate Delegate
    {
        get => _delegate;
        set
        {
            if (ReferenceEquals(_delegate, value)) return;
            bool relayout = value.ShouldRelayout(_delegate);
            _delegate = value;
            if (relayout) MarkNeedsLayout();
        }
    }

    protected override void SetupParentData(RenderObject child)
    {
        if (child.ParentData is not MultiChildLayoutParentData) child.ParentData = new MultiChildLayoutParentData();
    }

    protected override void PerformLayout()
    {
        Size = Constraints.Constrain(_delegate.GetSize(Constraints));
        _delegate.Run(this, Size);
    }

    public override void Paint(PaintingContext context, Offset offset)
    {
        foreach (var c in Children) context.PaintChild(c, offset + OffsetOf(c));
    }

    protected override bool HitTestChildren(HitTestResult result, Offset position)
    {
        for (int i = Children.Count - 1; i >= 0; i--)
            if (HitTestChild(Children[i], result, position)) return true;
        return false;
    }
}
