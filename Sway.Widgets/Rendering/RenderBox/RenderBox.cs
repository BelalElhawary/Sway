using SkiaSharp;

namespace Sway.Widgets;

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
