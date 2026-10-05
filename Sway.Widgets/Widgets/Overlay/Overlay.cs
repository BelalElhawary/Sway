using SkiaSharp;

namespace Sway.Widgets;

public sealed class Overlay(Widget initialChild) : StatefulWidget
{
    internal Widget Initial => initialChild;
    public override State CreateState() => new OverlayState();

    public static OverlayState? MaybeOf(BuildContext context) => context.Get<OverlayScope>()?.State;
    public static OverlayState Of(BuildContext context) =>
        MaybeOf(context) ?? throw new InvalidOperationException("No Overlay found above this context.");
}
