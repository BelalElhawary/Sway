using SkiaSharp;

namespace Sway.Widgets;

/// <summary>Positioned with start/end edges resolved by the ambient text direction.</summary>
public sealed class PositionedDirectional(Widget child, float? start = null, float? top = null, float? end = null, float? bottom = null,
    float? width = null, float? height = null, Key? key = null) : StatelessWidget(key)
{
    public override Widget Build(BuildContext context)
    {
        bool rtl = Directionality.Of(context) == TextDirection.Rtl;
        return new Positioned(child, rtl ? end : start, top, rtl ? start : end, bottom, width, height);
    }
}
