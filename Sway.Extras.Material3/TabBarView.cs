using Sway.Widgets;

namespace Sway.Extras.Material3;

/// <summary>Shows the child for the selected tab, cross-fading when it changes.</summary>
public sealed class TabBarView(int selectedIndex, IReadOnlyList<Widget> children, Key? key = null) : StatelessWidget(key)
{
    public override Widget Build(BuildContext context) =>
        new AnimatedSwitcher(TimeSpan.FromMilliseconds(220), new KeyedSubtree(new ValueKey<int>(selectedIndex), children[selectedIndex]));
}
