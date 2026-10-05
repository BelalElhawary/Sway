using SkiaSharp;

namespace Sway.Widgets;

public sealed class Positioned(Widget child, float? left = null, float? top = null, float? right = null, float? bottom = null,
    float? width = null, float? height = null, Key? key = null) : ParentDataWidget(child, key)
{
    public static Positioned Fill(Widget child, float left = 0, float top = 0, float right = 0, float bottom = 0) =>
        new(child, left, top, right, bottom);

    public override bool DebugIsValidParent(RenderObject parent) => parent is RenderStack;
    public override void ApplyParentData(RenderObject ro)
    {
        if (ro.ParentData is not StackParentData pd) ro.ParentData = pd = new StackParentData();
        if (pd.Left == left && pd.Top == top && pd.Right == right && pd.Bottom == bottom && pd.Width == width && pd.Height == height) return;
        pd.Left = left; pd.Top = top; pd.Right = right; pd.Bottom = bottom; pd.Width = width; pd.Height = height;
        ro.Parent?.MarkNeedsLayout();
    }
}
