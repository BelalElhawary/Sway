using SkiaSharp;

namespace Sway.Widgets;

public sealed class ColoredBox(SKColor color, Widget? child = null, Key? key = null) : StatelessWidget(key)
{
    public override Widget Build(BuildContext context) => new DecoratedBox(new BoxDecoration(Color: color), child);
}
