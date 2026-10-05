using SkiaSharp;

namespace Sway.Widgets;

public enum PointerEventKind { Down, Move, Up, Cancel, Hover, Scroll }

/// <summary>A pointer action in global (window) coordinates. <see cref="LocalPosition"/> is rewritten per target by the router.</summary>
public sealed record PointerEvent(PointerEventKind Kind, int Pointer, Offset Position, Offset Delta = default, Offset ScrollDelta = default, long TimestampMs = 0)
{
    /// <summary>Position in the receiving render object's own coordinates.</summary>
    public Offset LocalPosition { get; init; } = Position;
}

public sealed record HitTestEntry(RenderObject Target, SKMatrix GlobalToLocal)
{
    public Offset ToLocal(Offset global)
    {
        var p = GlobalToLocal.MapPoint(global.Dx, global.Dy);
        return new(p.X, p.Y);
    }
}

public sealed class HitTestResult
{
    readonly List<HitTestEntry> _path = new();
    SKMatrix _current = SKMatrix.Identity;

    public IReadOnlyList<HitTestEntry> Path => _path;

    internal void Add(RenderObject target) => _path.Add(new HitTestEntry(target, _current));

    /// <summary>Tests a child whose origin sits at <paramref name="offset"/> inside the current object.</summary>
    public bool AddWithPaintOffset(Offset offset, Offset position, Func<HitTestResult, Offset, bool> hitTest)
    {
        var saved = _current;
        _current = SKMatrix.Concat(SKMatrix.CreateTranslation(-offset.Dx, -offset.Dy), _current);
        bool hit = hitTest(this, position - offset);
        _current = saved;
        return hit;
    }

    /// <summary>Tests a child painted under <paramref name="forward"/> (child-local to parent-local).</summary>
    public bool AddWithTransform(SKMatrix forward, Offset position, Func<HitTestResult, Offset, bool> hitTest)
    {
        if (!forward.TryInvert(out var inverse)) return false;
        var saved = _current;
        _current = SKMatrix.Concat(inverse, _current);
        var p = inverse.MapPoint(position.Dx, position.Dy);
        bool hit = hitTest(this, new Offset(p.X, p.Y));
        _current = saved;
        return hit;
    }
}

/// <summary>A render object with a size, laid out by <see cref="BoxConstraints"/> from its parent.</summary>
public abstract class RenderBox : RenderObject
{
    BoxConstraints _constraints;
    Size? _size;

    public BoxConstraints Constraints => _constraints;
    public Size? SizeOrNull => _size;

    public Size Size
    {
        get => _size ?? throw new InvalidOperationException($"{GetType().Name} was not laid out.");
        protected set => _size = value;
    }

    public void Layout(BoxConstraints constraints)
    {
        if (!NeedsLayout && _size is not null && constraints == _constraints) return;
        _constraints = constraints;
        ClearNeedsLayout();
        InLayout = true;
        try { PerformLayout(); }
        finally { InLayout = false; }
        if (_size is null) throw new InvalidOperationException($"{GetType().Name}.PerformLayout did not set a size.");
        _size = constraints.Constrain(_size.Value);
    }

    /// <summary>Sets <see cref="Size"/> from <see cref="Constraints"/> and lays out children.</summary>
    protected abstract void PerformLayout();

    internal override void LayoutAsRoot() => Layout(_constraints);

    protected static Offset OffsetOf(RenderBox child) => ((BoxParentData)child.ParentData!).Offset;
    protected static void SetOffset(RenderBox child, Offset o) => ((BoxParentData)child.ParentData!).Offset = o;

    public override bool HitTest(HitTestResult result, Offset position)
    {
        if (_size is not { } s || !s.ToRect().Contains(position)) return false;
        if (HitTestChildren(result, position) || HitTestSelf(position))
        {
            result.Add(this);
            return true;
        }
        return false;
    }

    protected virtual bool HitTestSelf(Offset position) => false;

    protected virtual bool HitTestChildren(HitTestResult result, Offset position) => false;

    protected static bool HitTestChild(RenderBox child, HitTestResult result, Offset position) =>
        result.AddWithPaintOffset(OffsetOf(child), position, child.HitTest);

    /// <summary>The distance from the top to the first text baseline, or null if there is none.</summary>
    public virtual float? GetDistanceToBaseline() => null;

    // Intrinsic sizing: the size a box would choose with unbounded constraints, used by IntrinsicWidth/Height.
    public virtual float MinIntrinsicWidth(float height) => 0;
    public virtual float MaxIntrinsicWidth(float height) => 0;
    public virtual float MinIntrinsicHeight(float width) => 0;
    public virtual float MaxIntrinsicHeight(float width) => 0;
}

/// <summary>Root of the render tree; sized to the window.</summary>
public sealed class RenderView : RenderObjectWithChild
{
    public Size WindowSize { get; private set; }

    public void Configure(Size size)
    {
        if (WindowSize == size) return;
        WindowSize = size;
        MarkNeedsLayout();
    }

    internal override void LayoutAsRoot()
    {
        ClearNeedsLayout();
        Child?.Layout(BoxConstraints.Tight(WindowSize));
    }

    public override void Paint(PaintingContext context, Offset offset)
    {
        if (Child is not null) context.PaintChild(Child, offset);
    }

    public HitTestResult HitTestAt(Offset position)
    {
        var result = new HitTestResult();
        Child?.HitTest(result, position);
        return result;
    }
}

public sealed class RootWidget(Widget? child) : SingleChildRenderObjectWidget(child)
{
    public override RenderObject CreateRenderObject(BuildContext context) => new RenderView();
}
