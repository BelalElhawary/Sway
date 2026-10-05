namespace Sway.Widgets;

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
