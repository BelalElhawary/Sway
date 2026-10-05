using Sway.Widgets;

namespace Sway.Extras.Material3;

/// <summary>A button that opens a menu below itself; its child defaults to a three-dot icon button.</summary>
public sealed class PopupMenuButton<T>(IReadOnlyList<MenuEntry<T>> items, Action<T> onSelected, Widget? child = null, Key? key = null)
    : StatelessWidget(key)
{
    public override Widget Build(BuildContext context) => new Builder(ctx =>
    {
        void Open() => Menus.Show(ctx, Menus.AnchorOf(ctx), items, onSelected);
        return child is null
            ? new IconButton(new Icon(Icons.MoreVert), Open)
            : new GestureDetector(child, onTap: Open, behavior: HitTestBehavior.Opaque);
    });
}
