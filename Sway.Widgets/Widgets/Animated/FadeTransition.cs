using SkiaSharp;

namespace Sway.Widgets;

public sealed class FadeTransition(Animation<float> opacity, Widget? child = null, Key? key = null) : StatelessWidget(key)
{
    public override Widget Build(BuildContext context) =>
        new AnimatedBuilder(opacity, (_, c) => new Opacity(opacity.Value, c), child);
}
