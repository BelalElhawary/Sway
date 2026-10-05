using SkiaSharp;

namespace Sway.Widgets;

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
