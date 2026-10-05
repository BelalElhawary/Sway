using SkiaSharp;

namespace Sway.Widgets;

public sealed class ClipOval(Widget? child = null, Key? key = null) : StatelessWidget(key)
{
    // A circular clip is a rounded rect whose radius is half the shorter side; approximated via LayoutBuilder-free max radius.
    public override Widget Build(BuildContext context) => new ClipRRect(BorderRadius.Circular(10000), child);
}
