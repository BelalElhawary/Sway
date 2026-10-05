using SkiaSharp;

namespace Sway.Widgets;

/// <summary>
/// Handles wheel and drag input for a scrollable and builds its viewport. By default the wheel also scrolls along the
/// other axis when it has no movement of its own, so a mouse can scroll a horizontal strip; pass
/// <paramref name="wheelScrollsOtherAxis"/> false to let a vertical wheel pass through to an outer scrollable.
/// </summary>
public sealed class Scrollable(Axis axis, Func<BuildContext, ScrollPosition, Widget> viewportBuilder,
    ScrollController? controller = null, bool wheelScrollsOtherAxis = true, Key? key = null) : StatefulWidget(key)
{
    internal bool WheelScrollsOtherAxis => wheelScrollsOtherAxis;
    internal Axis Axis => axis;
    internal ScrollController? Controller => controller;
    internal Func<BuildContext, ScrollPosition, Widget> ViewportBuilder => viewportBuilder;
    public override State CreateState() => new ScrollableState();
}
