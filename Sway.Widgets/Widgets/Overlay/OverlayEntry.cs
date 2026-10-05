using SkiaSharp;

namespace Sway.Widgets;

/// <summary>A widget layered above the app (menus, dialogs, tooltips). Insert it with <see cref="OverlayState.Insert"/>.</summary>
public sealed class OverlayEntry(Func<BuildContext, Widget> builder)
{
    internal OverlayState? State;
    public Func<BuildContext, Widget> Builder { get; } = builder;

    public void Remove()
    {
        State?.Remove(this);
        State = null;
    }

    public void MarkNeedsBuild() => State?.Rebuild();
}
