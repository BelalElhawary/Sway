using SkiaSharp;

namespace Sway.Widgets;

public sealed class Expanded(Widget child, int flex = 1, Key? key = null) : ParentDataWidget(child, key)
{
    public override bool DebugIsValidParent(RenderObject parent) => parent is RenderFlex;
    public override void ApplyParentData(RenderObject ro)
    {
        if (ro.ParentData is not FlexParentData pd) ro.ParentData = pd = new FlexParentData();
        if (pd.Flex == flex && pd.Fit == FlexFit.Tight) return;
        pd.Flex = flex; pd.Fit = FlexFit.Tight;
        ro.Parent?.MarkNeedsLayout();
    }
}
