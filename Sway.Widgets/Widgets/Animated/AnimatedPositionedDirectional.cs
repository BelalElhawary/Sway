using SkiaSharp;

namespace Sway.Widgets;

/// <summary>An <see cref="AnimatedPositioned"/> whose horizontal edges are start/end, so they swap sides in right-to-left text.</summary>
public sealed class AnimatedPositionedDirectional(Widget child, TimeSpan duration, float? start = null, float? top = null, float? end = null,
    float? bottom = null, float? width = null, float? height = null, Curve? curve = null, Action? onEnd = null, Key? key = null) : StatelessWidget(key)
{
    public override Widget Build(BuildContext context)
    {
        bool rtl = Directionality.Of(context) == TextDirection.Rtl;
        return new AnimatedPositioned(child, duration, rtl ? end : start, top, rtl ? start : end, bottom, width, height, curve, onEnd);
    }
}
