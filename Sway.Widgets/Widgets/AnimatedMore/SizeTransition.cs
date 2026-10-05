using SkiaSharp;

namespace Sway.Widgets;

/// <summary>Clips and sizes its child along one axis by an animation, like a drawer opening.</summary>
public sealed class SizeTransition(Animation<float> sizeFactor, Widget? child = null, Axis axis = Axis.Vertical, float axisAlignment = 0, Key? key = null)
    : StatelessWidget(key)
{
    public override Widget Build(BuildContext context) => new AnimatedBuilder(sizeFactor, (_, inner) =>
    {
        float factor = Math.Max(0, sizeFactor.Value);
        bool vertical = axis == Axis.Vertical;
        return new ClipRect(new Align(
            vertical ? new Alignment(0, axisAlignment) : new Alignment(axisAlignment, 0), inner,
            widthFactor: vertical ? null : factor, heightFactor: vertical ? factor : null));
    }, child);
}
