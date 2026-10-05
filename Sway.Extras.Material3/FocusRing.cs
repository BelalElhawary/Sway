using SkiaSharp;

using Sway.Widgets;

namespace Sway.Extras.Material3;

static class FocusRing
{
    /// <summary>Draws a translucent focus outline around <paramref name="child"/> without affecting layout.</summary>
    public static Widget Around(bool visible, BorderRadius radius, Widget child, SKColor color, float inset = -3) =>
        visible
            ? new Stack([child, Positioned.Fill(new IgnorePointer(new DecoratedBox(
                new BoxDecoration(Border: Border.All(color.WithOpacity(0.5f), 3), BorderRadius: radius))), inset, inset, inset, inset)], clip: false)
            : child;
}
