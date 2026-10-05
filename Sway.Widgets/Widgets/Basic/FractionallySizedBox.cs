using SkiaSharp;

namespace Sway.Widgets;

public sealed class FractionallySizedBox(float? widthFactor = null, float? heightFactor = null, IAlignment? alignment = null, Widget? child = null, Key? key = null)
    : StatelessWidget(key)
{
    public override Widget Build(BuildContext context) => new Align(alignment ?? Alignment.Center, new FractionalBox(widthFactor, heightFactor, child));
}
