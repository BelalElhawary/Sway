using SkiaSharp;

namespace Sway.Widgets;

/// <summary>Sizes itself to its child, easing from the previous size whenever the child's size changes.</summary>
public sealed class RenderAnimatedSize(TimeSpan duration, Curve curve, Alignment alignment) : RenderObjectWithChildBox
{
    TimeSpan _duration = duration;
    Curve _curve = curve;
    Alignment _alignment = alignment;
    Size? _from, _target;
    TimeSpan _start;
    bool _animating;

    public void Update(TimeSpan duration, Curve curve, Alignment alignment)
    {
        _duration = duration; _curve = curve;
        if (_alignment != alignment) { _alignment = alignment; MarkNeedsPaint(); }
    }

    protected override void PerformLayout()
    {
        if (Child is not { } child) { Size = Constraints.Smallest; return; }
        child.Layout(Constraints);
        var childSize = child.Size;
        var now = WidgetsBinding.Instance.Now;

        Size current;
        if (_target is null) { _target = childSize; current = childSize; }
        else
        {
            if (_target != childSize)
            {
                _from = CurrentSize(now);
                _target = childSize;
                _start = now;
                _animating = _duration > TimeSpan.Zero;
            }
            current = CurrentSize(now);
            if (_animating && now - _start >= _duration) _animating = false;
        }

        if (_animating)
            WidgetsBinding.Instance.ScheduleFrameCallback(_ => MarkNeedsLayout());
        Size = Constraints.Constrain(current);
        SetOffset(child, _alignment.AlongSize(Size, childSize));
    }

    Size CurrentSize(TimeSpan now)
    {
        if (!_animating || _from is null) return _target!.Value;
        float t = Math.Clamp((float)((now - _start) / _duration), 0, 1);
        return Lerps.Size(_from.Value, _target!.Value, _curve.Transform(t));
    }

    public override void Paint(PaintingContext context, Offset offset)
    {
        if (Child is { } c)
            context.PushClipRect(offset, Size.ToRect(), (ctx, o) => ctx.PaintChild(c, o + OffsetOf(c)));
    }

    protected override bool HitTestChildren(HitTestResult result, Offset position) =>
        Child is { } c && HitTestChild(c, result, position);
}
