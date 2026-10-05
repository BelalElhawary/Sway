using Sway.Widgets;

namespace Sway.Extras.Material3;

public static class Menus
{
    internal const float ItemHeight = 48, DividerHeight = 17, VerticalPadding = 8, MaxWidth = 280;

    /// <summary>
    /// Opens a menu next to <paramref name="anchor"/> (a rectangle in window coordinates), below it if there is room, otherwise above.
    /// Returns a function that closes it. Escape or a click outside closes it too.
    /// </summary>
    public static Action Show<T>(BuildContext context, Rect anchor, IReadOnlyList<MenuEntry<T>> items, Action<T> onSelected, float minWidth = 112)
    {
        var overlay = Overlay.Of(context);
        var window = WidgetsBinding.Instance.RenderView.WindowSize;
        var previousFocus = WidgetsBinding.Instance.Focus.Primary;
        float estimated = items.Sum(i => i is MenuDivider<T> ? DividerHeight : ItemHeight) + VerticalPadding * 2;
        bool above = anchor.Bottom + 4 + estimated > window.Height && anchor.Top - 4 - estimated > 0;
        bool alignEnd = anchor.Left + MaxWidth > window.Width && anchor.Right - MaxWidth >= 0;

        OverlayEntry? entry = null;
        void Close()
        {
            if (entry is null) return;
            entry.Remove();
            entry = null;
            if (previousFocus is { Element.Mounted: true }) previousFocus.RequestFocus();
        }

        entry = new OverlayEntry(ctx => Dialogs.Wrap(context, new Stack([
            Positioned.Fill(new GestureDetector(onTap: Close, behavior: HitTestBehavior.Opaque)),
            new Positioned(
                new PopupMenu<T>(items, v => { Close(); onSelected(v); }, Close, minWidth),
                left: alignEnd ? null : anchor.Left, right: alignEnd ? window.Width - anchor.Right : null,
                top: above ? null : anchor.Bottom + 4, bottom: above ? window.Height - anchor.Top + 4 : null),
        ], fit: StackFit.Expand, clip: false)));
        overlay.Insert(entry);
        return Close;
    }

    /// <summary>The window rectangle a context's widget occupies, for anchoring a menu to it.</summary>
    public static Rect AnchorOf(BuildContext context) =>
        context.FindRenderObject() is RenderBox { SizeOrNull: { } size } box
            ? new Rect(box.LocalToGlobal(Offset.Zero).Dx, box.LocalToGlobal(Offset.Zero).Dy, size.Width, size.Height)
            : new Rect(0, 0, 0, 0);
}
