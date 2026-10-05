namespace Sway.Widgets;

/// <summary>
/// Keeps a child alive but out of sight: while offstage it takes no space, paints nothing, ignores the pointer, and its
/// animations are paused (see <see cref="TickerMode"/>).
/// </summary>
public sealed class Offstage(Widget? child = null, bool offstage = true, Key? key = null) : StatelessWidget(key)
{
    public override Widget Build(BuildContext context) =>
        new TickerMode(!offstage, new OffstageBox(child, offstage));
}
