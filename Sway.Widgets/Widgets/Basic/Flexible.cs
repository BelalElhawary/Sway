using SkiaSharp;

namespace Sway.Widgets;

public sealed class Flexible(Widget child, int flex = 1, FlexFit fit = FlexFit.Loose, Key? key = null) : ParentDataWidget(child, key)
{
    public override bool DebugIsValidParent(RenderObject parent) => parent is RenderFlex;
    public override void ApplyParentData(RenderObject ro)
    {
        if (ro.ParentData is not FlexParentData pd) ro.ParentData = pd = new FlexParentData();
        if (pd.Flex == flex && pd.Fit == fit) return;
        pd.Flex = flex; pd.Fit = fit;
        ro.Parent?.MarkNeedsLayout();
    }
}
