using SkiaSharp;

namespace Sway.Widgets;

public sealed class SingleChildScrollView(Widget child, Axis scrollDirection = Axis.Vertical, ScrollController? controller = null,
    EdgeInsets? padding = null, Key? key = null, bool wheelScrollsOtherAxis = true, bool shrinkWrap = false) : StatelessWidget(key)
{
    public override Widget Build(BuildContext context) =>
        new Scrollable(scrollDirection, (_, position) =>
            new Viewport(scrollDirection, position, padding is { } p ? new Padding(p, child) : child, shrinkWrap: shrinkWrap), controller, wheelScrollsOtherAxis);
}
