using SkiaSharp;

namespace Sway.Widgets;

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
