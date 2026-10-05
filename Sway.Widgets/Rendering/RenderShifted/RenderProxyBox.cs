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
