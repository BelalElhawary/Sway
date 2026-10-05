using SkiaSharp;

using Sway.Widgets;

namespace Sway.Extras.Material3;

/// <summary>The 40px round hover/press/focus halo behind a toggle's mark.</summary>
static class Halo
{
    public static Widget Wrap(InteractionState s, SKColor color, bool enabled, Widget mark, float size = 40) =>
        new AnimatedContainer(TimeSpan.FromMilliseconds(100), width: size, height: size, alignment: Alignment.Center,
            decoration: new BoxDecoration(Color: color.WithOpacity(enabled ? StateLayer.Opacity(s) : 0), Shape: BoxShape.Circle), child: mark);
}
