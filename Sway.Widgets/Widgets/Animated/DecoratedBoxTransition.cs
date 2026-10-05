using SkiaSharp;

namespace Sway.Widgets;

public sealed class DecoratedBoxTransition(Animation<BoxDecoration> decoration, Widget? child = null, Key? key = null) : StatelessWidget(key)
{
    public override Widget Build(BuildContext context) =>
        new AnimatedBuilder(decoration, (_, c) => new DecoratedBox(decoration.Value, c), child);
}
