using SkiaSharp;

namespace Sway.Widgets;

/// <summary>Shows a window onto a larger child, shifted by the scroll position.</summary>
public sealed class RenderViewport : RenderObjectWithChildBox, IScrollViewport
{
    Axis _axis;
    ScrollPosition _position;
    bool _shrinkWrap;
    readonly ScrollbarInteraction _scrollbar = new();

    public RenderViewport(Axis axis, ScrollPosition position, bool shrinkWrap = false)
    {
        _axis = axis;
        _position = position;
        _shrinkWrap = shrinkWrap;
    }

    public void Update(Axis axis, ScrollPosition position, bool shrinkWrap = false)
    {
        if (_shrinkWrap != shrinkWrap) { _shrinkWrap = shrinkWrap; MarkNeedsLayout(); }
        if (_axis == axis && ReferenceEquals(_position, position)) return;
        if (Owner is not null) _position.Changed -= OnScroll;
        _axis = axis;
        _position = position;
        if (Owner is not null) _position.Changed += OnScroll;
        MarkNeedsLayout();
    }

    void OnScroll() => MarkNeedsPaint();

    Axis IScrollViewport.ScrollAxis => _axis;
    ScrollPosition IScrollViewport.Position => _position;
    Offset IScrollViewport.PaintShift => ChildOffset;

    public override void Attach(PipelineOwner owner)
    {
        base.Attach(owner);
        _position.Changed += OnScroll;
    }

    public override void Detach()
    {
        _position.Changed -= OnScroll;
        base.Detach();
    }

    bool Vertical => _axis == Axis.Vertical;

    protected override void PerformLayout()
    {
        var c = Constraints;
        if (Child is not { } child)
        {
            Size = c.Biggest.Equals(default) ? Size.Zero : c.Constrain(Size.Zero);
            _position.ApplyContentDimensions(0, 0);
            return;
        }

        child.Layout(Vertical
            ? new BoxConstraints(c.MinWidth, c.MaxWidth, 0, float.PositiveInfinity)
            : new BoxConstraints(0, float.PositiveInfinity, c.MinHeight, c.MaxHeight));

        float viewportMain = Vertical
            ? (c.HasBoundedHeight && !_shrinkWrap ? c.MaxHeight : child.Size.Height)
            : (c.HasBoundedWidth && !_shrinkWrap ? c.MaxWidth : child.Size.Width);
        float cross = Vertical
            ? (c.HasBoundedWidth ? c.MaxWidth : child.Size.Width)
            : (c.HasBoundedHeight ? c.MaxHeight : child.Size.Height);
        Size = c.Constrain(Vertical ? new Size(cross, viewportMain) : new Size(viewportMain, cross));

        float viewport = Vertical ? Size.Height : Size.Width;
        float content = Vertical ? child.Size.Height : child.Size.Width;
        _position.ApplyContentDimensions(viewport, content - viewport);
    }

    Offset ChildOffset => Vertical ? new Offset(0, -_position.Pixels) : new Offset(-_position.Pixels, 0);

    public override void Paint(PaintingContext context, Offset offset)
    {
        if (Child is not { } child) return;
        context.PushClipRect(offset, Size.ToRect(), (ctx, o) => ctx.PaintChild(child, o + ChildOffset));
        _scrollbar.Paint(context.Canvas, Size, _axis, _position, offset, MarkNeedsPaint);
    }

    public override bool HitTest(HitTestResult result, Offset position)
    {
        if (SizeOrNull is not { } size || !size.ToRect().Contains(position)) return false;
        if (_scrollbar.HitTest(size, _axis, _position, position))
        {
            result.Add(this);
            return true;
        }
        return base.HitTest(result, position);
    }

    public override void HandlePointerEvent(PointerEvent e, HitTestEntry entry) => _scrollbar.Handle(e, Size, _axis, _position);

    protected override bool HitTestChildren(HitTestResult result, Offset position) =>
        Child is { } c && result.AddWithPaintOffset(ChildOffset, position, c.HitTest);

    public override float MinIntrinsicWidth(float h) => Child?.MinIntrinsicWidth(h) ?? 0;
    public override float MaxIntrinsicWidth(float h) => Child?.MaxIntrinsicWidth(h) ?? 0;
    public override float MinIntrinsicHeight(float w) => Child?.MinIntrinsicHeight(w) ?? 0;
    public override float MaxIntrinsicHeight(float w) => Child?.MaxIntrinsicHeight(w) ?? 0;
}
