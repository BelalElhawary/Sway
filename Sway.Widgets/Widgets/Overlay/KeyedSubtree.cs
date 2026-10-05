using SkiaSharp;

namespace Sway.Widgets;

/// <summary>Gives a subtree a key without changing how it builds.</summary>
public sealed class KeyedSubtree(Key key, Widget child) : StatelessWidget(key)
{
    public override Widget Build(BuildContext context) => child;
}
