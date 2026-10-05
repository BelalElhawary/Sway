namespace Sway.Widgets;

/// <summary>Sets the Tab position of the focusable widgets below it: lower numbers are visited first, others (0) follow tree order.</summary>
public sealed class FocusTraversalOrder(float order, Widget child, Key? key = null) : InheritedWidget(child, key)
{
    public float Order => order;
    public override bool UpdateShouldNotify(InheritedWidget old) => ((FocusTraversalOrder)old).Order != Order;
    internal static float Of(BuildContext context) => context.Get<FocusTraversalOrder>()?.Order ?? 0;
}
