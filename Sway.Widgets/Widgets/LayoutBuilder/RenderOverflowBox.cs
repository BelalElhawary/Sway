namespace Sway.Widgets;

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
