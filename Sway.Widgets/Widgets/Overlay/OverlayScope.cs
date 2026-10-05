using SkiaSharp;

namespace Sway.Widgets;

sealed class OverlayScope(OverlayState state, Widget child) : InheritedWidget(child)
{
    public OverlayState State => state;
    public override bool UpdateShouldNotify(InheritedWidget old) => false;
}
