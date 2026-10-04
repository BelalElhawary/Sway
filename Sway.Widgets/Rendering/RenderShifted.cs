using SkiaSharp;

namespace Sway.Widgets;

/// <summary>Sizes itself to its child and paints it in place.</summary>
public class RenderProxyBox : RenderObjectWithChildBox
{
    protected override void PerformLayout()
    {
        if (Child is { } c)
        {
            c.Layout(Constraints);
            Size = c.Size;
        }
        else Size = Constraints.Smallest;
    }

    public override void Paint(PaintingContext context, Offset offset)
    {
        if (Child is { } c) context.PaintChild(c, offset);
    }

    protected override bool HitTestChildren(HitTestResult result, Offset position) =>
        Child is { } c && c.HitTest(result, position);

    public override float? GetDistanceToBaseline() => Child?.GetDistanceToBaseline();
    public override float MinIntrinsicWidth(float h) => Child?.MinIntrinsicWidth(h) ?? 0;
    public override float MaxIntrinsicWidth(float h) => Child?.MaxIntrinsicWidth(h) ?? 0;
    public override float MinIntrinsicHeight(float w) => Child?.MinIntrinsicHeight(w) ?? 0;
    public override float MaxIntrinsicHeight(float w) => Child?.MaxIntrinsicHeight(w) ?? 0;
}

/// <summary>A <see cref="RenderBox"/> with one optional box child (the box-typed counterpart of <see cref="RenderObjectWithChild"/>).</summary>
public abstract class RenderObjectWithChildBox : RenderBox, IRenderChildHolder
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

public enum HitTestBehavior { DeferToChild, Opaque, Translucent }

/// <summary>Proxy box that also counts as a hit target depending on <see cref="HitTestBehavior"/>.</summary>
public class RenderProxyBoxWithHitTestBehavior : RenderProxyBox
{
    public HitTestBehavior Behavior { get; set; } = HitTestBehavior.DeferToChild;

    public override bool HitTest(HitTestResult result, Offset position)
    {
        bool hitTarget = false;
        if (SizeOrNull is { } s && s.ToRect().Contains(position))
        {
            hitTarget = HitTestChildren(result, position) || Behavior == HitTestBehavior.Opaque;
            if (hitTarget || Behavior == HitTestBehavior.Translucent) result.Add(this);
        }
        return hitTarget;
    }
}

public sealed class RenderPadding(EdgeInsets padding) : RenderObjectWithChildBox
{
    EdgeInsets _padding = padding;

    public EdgeInsets Padding
    {
        get => _padding;
        set { if (_padding == value) return; _padding = value; MarkNeedsLayout(); }
    }

    protected override void PerformLayout()
    {
        if (Child is not { } c)
        {
            Size = Constraints.Constrain(_padding.Collapsed);
            return;
        }
        c.Layout(Constraints.Deflate(_padding));
        SetOffset(c, new Offset(_padding.Left, _padding.Top));
        Size = Constraints.Constrain(new Size(c.Size.Width + _padding.Horizontal, c.Size.Height + _padding.Vertical));
    }

    public override void Paint(PaintingContext context, Offset offset)
    {
        if (Child is { } c) context.PaintChild(c, offset + OffsetOf(c));
    }

    protected override bool HitTestChildren(HitTestResult result, Offset position) =>
        Child is { } c && HitTestChild(c, result, position);

    public override float? GetDistanceToBaseline() => Child?.GetDistanceToBaseline() + _padding.Top;
    public override float MinIntrinsicWidth(float h) => (Child?.MinIntrinsicWidth(Math.Max(0, h - _padding.Vertical)) ?? 0) + _padding.Horizontal;
    public override float MaxIntrinsicWidth(float h) => (Child?.MaxIntrinsicWidth(Math.Max(0, h - _padding.Vertical)) ?? 0) + _padding.Horizontal;
    public override float MinIntrinsicHeight(float w) => (Child?.MinIntrinsicHeight(Math.Max(0, w - _padding.Horizontal)) ?? 0) + _padding.Vertical;
    public override float MaxIntrinsicHeight(float w) => (Child?.MaxIntrinsicHeight(Math.Max(0, w - _padding.Horizontal)) ?? 0) + _padding.Vertical;
}

/// <summary>Imposes additional constraints on its child (SizedBox, ConstrainedBox).</summary>
public sealed class RenderConstrainedBox(BoxConstraints additional) : RenderProxyBox
{
    BoxConstraints _additional = additional;

    public BoxConstraints AdditionalConstraints
    {
        get => _additional;
        set { if (_additional == value) return; _additional = value; MarkNeedsLayout(); }
    }

    protected override void PerformLayout()
    {
        var effective = _additional.Enforce(Constraints);
        if (Child is { } c)
        {
            c.Layout(effective);
            Size = c.Size;
        }
        else Size = effective.Smallest;
    }

    public override float MinIntrinsicWidth(float h) => _additional.HasTightWidth ? _additional.MinWidth : _additional.Enforce(new(0, float.PositiveInfinity, 0, float.PositiveInfinity)).ConstrainWidth(base.MinIntrinsicWidth(h));
    public override float MaxIntrinsicWidth(float h) => _additional.HasTightWidth ? _additional.MinWidth : _additional.Enforce(new(0, float.PositiveInfinity, 0, float.PositiveInfinity)).ConstrainWidth(base.MaxIntrinsicWidth(h));
    public override float MinIntrinsicHeight(float w) => _additional.HasTightHeight ? _additional.MinHeight : _additional.ConstrainHeight(base.MinIntrinsicHeight(w));
    public override float MaxIntrinsicHeight(float w) => _additional.HasTightHeight ? _additional.MinHeight : _additional.ConstrainHeight(base.MaxIntrinsicHeight(w));
}

/// <summary>Positions its child inside itself (Align, Center).</summary>
public sealed class RenderPositionedBox(Alignment alignment, float? widthFactor, float? heightFactor) : RenderObjectWithChildBox
{
    Alignment _alignment = alignment;
    float? _widthFactor = widthFactor, _heightFactor = heightFactor;

    public void Update(Alignment alignment, float? widthFactor, float? heightFactor)
    {
        if (_alignment == alignment && _widthFactor == widthFactor && _heightFactor == heightFactor) return;
        _alignment = alignment; _widthFactor = widthFactor; _heightFactor = heightFactor;
        MarkNeedsLayout();
    }

    protected override void PerformLayout()
    {
        bool shrinkW = _widthFactor is not null || !Constraints.HasBoundedWidth;
        bool shrinkH = _heightFactor is not null || !Constraints.HasBoundedHeight;

        if (Child is not { } c)
        {
            Size = Constraints.Constrain(new Size(shrinkW ? 0 : float.PositiveInfinity, shrinkH ? 0 : float.PositiveInfinity));
            return;
        }

        c.Layout(Constraints.Loosen());
        Size = Constraints.Constrain(new Size(
            shrinkW ? c.Size.Width * (_widthFactor ?? 1) : float.PositiveInfinity,
            shrinkH ? c.Size.Height * (_heightFactor ?? 1) : float.PositiveInfinity));
        SetOffset(c, _alignment.AlongSize(Size, c.Size));
    }

    public override void Paint(PaintingContext context, Offset offset)
    {
        if (Child is { } c) context.PaintChild(c, offset + OffsetOf(c));
    }

    protected override bool HitTestChildren(HitTestResult result, Offset position) =>
        Child is { } c && HitTestChild(c, result, position);

    public override float? GetDistanceToBaseline() => Child?.GetDistanceToBaseline() + (Child is { } c ? OffsetOf(c).Dy : 0);
    public override float MinIntrinsicWidth(float h) => (Child?.MinIntrinsicWidth(h) ?? 0) * (_widthFactor ?? 1);
    public override float MaxIntrinsicWidth(float h) => (Child?.MaxIntrinsicWidth(h) ?? 0) * (_widthFactor ?? 1);
    public override float MinIntrinsicHeight(float w) => (Child?.MinIntrinsicHeight(w) ?? 0) * (_heightFactor ?? 1);
    public override float MaxIntrinsicHeight(float w) => (Child?.MaxIntrinsicHeight(w) ?? 0) * (_heightFactor ?? 1);
}

public sealed class RenderDecoratedBox(BoxDecoration decoration, TextDirection direction, bool foreground = false) : RenderProxyBox
{
    BoxDecoration _decoration = decoration;
    TextDirection _direction = direction;

    public void Update(BoxDecoration decoration, TextDirection direction)
    {
        if (_decoration.Equals(decoration) && _direction == direction) return;
        _decoration = decoration;
        _direction = direction;
        MarkNeedsPaint();
    }

    public override void Paint(PaintingContext context, Offset offset)
    {
        if (!foreground) _decoration.Paint(context.Canvas, Size.ToRect(offset), _direction);
        base.Paint(context, offset);
        if (foreground) _decoration.Paint(context.Canvas, Size.ToRect(offset), _direction);
    }

    protected override bool HitTestSelf(Offset position) => true;
}

public sealed class RenderOpacity(float opacity) : RenderProxyBox
{
    float _opacity = opacity;

    public float Opacity
    {
        get => _opacity;
        set { if (_opacity == value) return; _opacity = value; MarkNeedsPaint(); }
    }

    public override void Paint(PaintingContext context, Offset offset)
    {
        if (Child is { } c) context.PushOpacity(offset, _opacity, (ctx, o) => ctx.PaintChild(c, o));
    }
}

public sealed class RenderClip(BorderRadius? radius, bool antiAlias = true) : RenderProxyBox
{
    BorderRadius? _radius = radius;

    public BorderRadius? Radius
    {
        get => _radius;
        set { if (_radius == value) return; _radius = value; MarkNeedsPaint(); }
    }

    public override void Paint(PaintingContext context, Offset offset)
    {
        if (Child is not { } c) return;
        var rect = Size.ToRect();
        if (_radius is { IsZero: false } r)
            context.PushClipRRect(offset, rect, r, (ctx, o) => ctx.PaintChild(c, o));
        else
            context.PushClipRect(offset, rect, (ctx, o) => ctx.PaintChild(c, o));
    }

    protected override bool HitTestChildren(HitTestResult result, Offset position) =>
        Child is { } c && c.HitTest(result, position);
}

public sealed class RenderTransform(SKMatrix transform, Alignment? origin) : RenderProxyBox
{
    SKMatrix _transform = transform;
    Alignment? _origin = origin;

    public void Update(SKMatrix transform, Alignment? origin)
    {
        if (_transform == transform && _origin == origin) return;
        _transform = transform; _origin = origin;
        MarkNeedsPaint();
    }

    SKMatrix Effective()
    {
        var o = (_origin ?? Alignment.Center).AlongSize(Size, Size.Zero);
        var toOrigin = SKMatrix.CreateTranslation(o.Dx, o.Dy);
        var fromOrigin = SKMatrix.CreateTranslation(-o.Dx, -o.Dy);
        // Apply: move origin to (0,0), transform, move back.
        return SKMatrix.Concat(toOrigin, SKMatrix.Concat(_transform, fromOrigin));
    }

    public override void Paint(PaintingContext context, Offset offset)
    {
        if (Child is { } c) context.PushTransform(offset, Effective(), (ctx, o) => ctx.PaintChild(c, o));
    }

    protected override bool HitTestChildren(HitTestResult result, Offset position) =>
        Child is { } c && result.AddWithTransform(Effective(), position, c.HitTest);
}

public sealed class RenderPointerListener : RenderProxyBoxWithHitTestBehavior
{
    public Action<PointerEvent>? OnPointerDown, OnPointerMove, OnPointerUp, OnPointerCancel, OnPointerScroll;

    public override void HandlePointerEvent(PointerEvent e, HitTestEntry entry)
    {
        switch (e.Kind)
        {
            case PointerEventKind.Down: OnPointerDown?.Invoke(e); break;
            case PointerEventKind.Move: OnPointerMove?.Invoke(e); break;
            case PointerEventKind.Up: OnPointerUp?.Invoke(e); break;
            case PointerEventKind.Cancel: OnPointerCancel?.Invoke(e); break;
            case PointerEventKind.Scroll: OnPointerScroll?.Invoke(e); break;
        }
    }
}

public enum MouseCursor { Default, Click, Text, Forbidden, Grab, Grabbing, ResizeHorizontal, ResizeVertical, Wait, Move }

public sealed class RenderMouseRegion : RenderProxyBoxWithHitTestBehavior
{
    public Action<PointerEvent>? OnEnter, OnExit, OnHover;
    public MouseCursor Cursor = MouseCursor.Default;
    public bool Opaque { get => Behavior == HitTestBehavior.Opaque; set => Behavior = value ? HitTestBehavior.Opaque : HitTestBehavior.Translucent; }

    public RenderMouseRegion() { Behavior = HitTestBehavior.Opaque; }
}
