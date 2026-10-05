using SkiaSharp;

namespace Sway.Widgets;

public sealed class ScaleTransition(Animation<float> scale, Widget? child = null, Alignment? alignment = null, Key? key = null) : StatelessWidget(key)
{
    public override Widget Build(BuildContext context) =>
        new AnimatedBuilder(scale, (_, c) => Transform.Scale(scale.Value, c, alignment), child);
}
