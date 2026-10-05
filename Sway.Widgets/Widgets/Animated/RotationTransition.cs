using SkiaSharp;

namespace Sway.Widgets;

public sealed class RotationTransition(Animation<float> turns, Widget? child = null, Alignment? alignment = null, Key? key = null) : StatelessWidget(key)
{
    public override Widget Build(BuildContext context) =>
        new AnimatedBuilder(turns, (_, c) => Transform.Rotate(turns.Value * MathF.PI * 2, c, alignment), child);
}
