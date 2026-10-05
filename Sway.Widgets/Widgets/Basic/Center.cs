using SkiaSharp;

namespace Sway.Widgets;

public sealed class Center(Widget? child = null, float? widthFactor = null, float? heightFactor = null, Key? key = null)
    : StatelessWidget(key)
{
    public override Widget Build(BuildContext context) => new Align(Alignment.Center, child, widthFactor, heightFactor);
}
