using Sway.Widgets;

namespace Sway.Extras.Material3;

/// <summary>A panel that slides in over a scrim: a navigation drawer from either side, or a sheet from the bottom.</summary>
public static class Panels
{
    /// <summary>Slides a drawer in from the start (or end) edge. The builder gets a function that slides it out and removes it.</summary>
    public static Action ShowDrawer(BuildContext context, Func<BuildContext, Action, Widget> builder, DrawerSide side = DrawerSide.Start,
        float width = 304, bool dismissible = true) =>
        Show(context, builder, side == DrawerSide.Start ? PanelEdge.Start : PanelEdge.End, width, dismissible);

    /// <summary>Slides a modal bottom sheet up. It can be dragged down to dismiss.</summary>
    public static Action ShowBottomSheet(BuildContext context, Func<BuildContext, Action, Widget> builder, bool dismissible = true) =>
        Show(context, builder, PanelEdge.Bottom, null, dismissible);

    static Action Show(BuildContext context, Func<BuildContext, Action, Widget> builder, PanelEdge edge, float? width, bool dismissible)
    {
        var overlay = Overlay.Of(context);
        var previousFocus = WidgetsBinding.Instance.Focus.Primary;
        OverlayEntry? entry = null;
        Action? requestClose = null;
        void Remove()
        {
            if (entry is null) return;
            entry.Remove();
            entry = null;
            if (previousFocus is { Element.Mounted: true }) previousFocus.RequestFocus();
        }
        entry = new OverlayEntry(ctx => Dialogs.Wrap(context, new ModalPanel(edge, width, dismissible, builder, Remove, close => requestClose = close)));
        overlay.Insert(entry);
        return () => (requestClose ?? Remove)();
    }
}
