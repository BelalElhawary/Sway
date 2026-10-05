using SkiaSharp;

namespace Sway.Widgets;

public abstract class ParentData { }

public class BoxParentData : ParentData
{
    public Offset Offset;
}

/// <summary>Owns the render tree's dirty state and asks the host for a frame when something changes.</summary>
public sealed class PipelineOwner
{
    readonly HashSet<RenderObject> _layoutRoots = new();
    bool _framePending;

    public Action? OnNeedsFrame { get; set; }
    public bool NeedsLayout => _layoutRoots.Count > 0;

    internal void RequestLayout(RenderObject root)
    {
        _layoutRoots.Add(root);
        RequestFrame();
    }

    public void RequestFrame()
    {
        if (_framePending) return;
        _framePending = true;
        OnNeedsFrame?.Invoke();
    }

    public void FlushLayout()
    {
        // A layout pass can dirty more roots (e.g. LayoutBuilder), so loop until stable.
        for (int guard = 0; _layoutRoots.Count > 0 && guard < 16; guard++)
        {
            var roots = _layoutRoots.ToArray();
            _layoutRoots.Clear();
            foreach (var r in roots) r.LayoutAsRoot();
        }
    }

    internal void FrameDone() => _framePending = false;
}

public abstract class RenderObject : IDisposable
{
    bool _needsLayout = true;

    public RenderObject? Parent { get; private set; }
    public ParentData? ParentData { get; set; }
    public PipelineOwner? Owner { get; private set; }
    protected bool NeedsLayout => _needsLayout;

    public virtual void Attach(PipelineOwner owner)
    {
        Owner = owner;
        VisitChildren(c => c.Attach(owner));
        if (_needsLayout) MarkNeedsLayoutSelf();
    }

    public virtual void Detach()
    {
        Owner = null;
        VisitChildren(c => c.Detach());
    }

    protected void AdoptChild(RenderObject child)
    {
        child.Parent = this;
        SetupParentData(child);
        if (Owner is not null) child.Attach(Owner);
        MarkNeedsLayout();
    }

    protected void DropChild(RenderObject child)
    {
        child.Parent = null;
        if (Owner is not null) child.Detach();
        MarkNeedsLayout();
    }

    protected virtual void SetupParentData(RenderObject child)
    {
        if (child.ParentData is not BoxParentData) child.ParentData = new BoxParentData();
    }

    public abstract void VisitChildren(Action<RenderObject> visitor);

    protected bool InLayout { get; set; }

    public void MarkNeedsLayout()
    {
        // Children adopted or dropped while this object is mid-layout are laid out by the same pass.
        if (InLayout) return;
        _needsLayout = true;
        MarkNeedsLayoutSelf();
    }

    // Flags each ancestor; stops at one already flagged (its own walk reached the root) or one mid-layout
    // (it is about to lay this subtree out anyway).
    void MarkNeedsLayoutSelf()
    {
        for (var o = this; ; o = o.Parent!)
        {
            var p = o.Parent;
            if (p is null)
            {
                o.Owner?.RequestLayout(o);
                return;
            }
            if (p.InLayout) return;
            if (p._needsLayout) { Owner?.RequestFrame(); return; }
            p._needsLayout = true;
        }
    }

    public void MarkNeedsPaint() => Owner?.RequestFrame();

    internal void ClearNeedsLayout() => _needsLayout = false;
    internal bool NeedsLayoutInternal => _needsLayout;

    /// <summary>Lays out a tree root whose constraints come from the host, not a parent.</summary>
    internal abstract void LayoutAsRoot();

    public abstract void Paint(PaintingContext context, Offset offset);

    /// <summary>Tests <paramref name="position"/> (local to this object) and records hits, deepest first.</summary>
    public virtual bool HitTest(HitTestResult result, Offset position) => false;

    /// <summary>Converts a point in this object's coordinates to window coordinates, including scroll shifts (ignores other transforms).</summary>
    public Offset LocalToGlobal(Offset local)
    {
        var p = local;
        for (var o = this; o.Parent is not null; o = o.Parent)
        {
            if (o.ParentData is BoxParentData bpd) p += bpd.Offset;
            if (o.Parent is IScrollViewport viewport) p += viewport.PaintShift;
        }
        return p;
    }

    public virtual void HandlePointerEvent(PointerEvent e, HitTestEntry entry) { }

    public virtual void Dispose() { }
}

/// <summary>Render object that holds one box child.</summary>
/// <summary>Anything that hosts at most one box child.</summary>
public interface IRenderChildHolder { RenderBox? Child { get; set; } }

public abstract class RenderObjectWithChild : RenderObject, IRenderChildHolder
{
    RenderBox? _child;

    public RenderBox? Child
    {
        get => _child;
        set
        {
            if (ReferenceEquals(_child, value)) return;
            if (_child is not null) DropChild(_child);
            _child = value;
            if (_child is not null) AdoptChild(_child);
        }
    }

    public override void VisitChildren(Action<RenderObject> visitor)
    {
        if (_child is not null) visitor(_child);
    }
}

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

/// <summary>Draws into a canvas and composes the clip/opacity/transform effects that wrap child painting.</summary>
public sealed class PaintingContext(SKCanvas canvas)
{
    public SKCanvas Canvas { get; } = canvas;

    // Overflow a child may legitimately paint outside its box (shadows, glows, transforms) before it is culled.
    const float CullMargin = 96;

    /// <summary>Paints <paramref name="child"/>, skipping it when it lies entirely outside the current clip.</summary>
    public void PaintChild(RenderBox child, Offset offset)
    {
        if (child.SizeOrNull is { } s &&
            Canvas.QuickReject(new SKRect(offset.Dx - CullMargin, offset.Dy - CullMargin, offset.Dx + s.Width + CullMargin, offset.Dy + s.Height + CullMargin)))
            return;
        child.Paint(this, offset);
    }

    public void PushClipRect(Offset offset, Rect rect, Action<PaintingContext, Offset> painter)
    {
        Canvas.Save();
        Canvas.ClipRect(rect.Shift(offset).ToSk(), antialias: true);
        painter(this, offset);
        Canvas.Restore();
    }

    public void PushClipRRect(Offset offset, Rect rect, BorderRadius radius, Action<PaintingContext, Offset> painter)
    {
        Canvas.Save();
        Canvas.ClipRoundRect(radius.ToRoundRect(rect.Shift(offset)), antialias: true);
        painter(this, offset);
        Canvas.Restore();
    }

    public void PushOpacity(Offset offset, float opacity, Action<PaintingContext, Offset> painter)
    {
        if (opacity <= 0) return;
        if (opacity >= 1) { painter(this, offset); return; }
        using var paint = new SKPaint { Color = SKColors.White.WithAlpha((byte)(opacity * 255)) };
        Canvas.SaveLayer(paint);
        painter(this, offset);
        Canvas.Restore();
    }

    /// <summary>Paints under <paramref name="transform"/> (which is applied about the origin of <paramref name="offset"/>).</summary>
    public void PushTransform(Offset offset, SKMatrix transform, Action<PaintingContext, Offset> painter)
    {
        Canvas.Save();
        Canvas.Translate(offset.Dx, offset.Dy);
        Canvas.Concat(in transform);
        painter(this, Offset.Zero);
        Canvas.Restore();
    }
}
