using SkiaSharp;

namespace Sway.Widgets;

/// <summary>Slides its child by a fraction of its own size (1.0 = one full width/height).</summary>
public sealed class SlideTransition(Animation<Offset> position, Widget? child = null, Key? key = null) : StatelessWidget(key)
{
    public override Widget Build(BuildContext context) =>
        new AnimatedBuilder(position, (_, c) => new FractionalTranslation(position.Value, c), child);
}
