using SkiaSharp;

namespace Sway.Widgets;

/// <summary>
/// A list that animates items in and out. Keep your data in sync: call <c>controller.InsertItem(i)</c> after adding to your
/// collection and <c>controller.RemoveItem(i, ...)</c> after removing from it.
/// </summary>
public sealed class AnimatedList(AnimatedListController controller, int initialItemCount, Func<BuildContext, int, Animation<float>, Widget> itemBuilder,
    Axis scrollDirection = Axis.Vertical, EdgeInsets? padding = null, ScrollController? scrollController = null, Key? key = null) : StatefulWidget(key)
{
    internal AnimatedListController Controller => controller;
    internal int InitialItemCount => initialItemCount;
    internal Func<BuildContext, int, Animation<float>, Widget> ItemBuilder => itemBuilder;
    internal Axis ScrollDirection => scrollDirection;
    internal EdgeInsets? Padding => padding;
    internal ScrollController? ScrollController => scrollController;
    public override State CreateState() => new AnimatedListState();
}
