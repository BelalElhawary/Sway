using SkiaSharp;

namespace Sway.Widgets;

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
