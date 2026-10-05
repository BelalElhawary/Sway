using SkiaSharp;

namespace Sway.Widgets;

/// <summary>Rebuilds <paramref name="builder"/> whenever <paramref name="animation"/> notifies. Pass static subtrees as <c>child</c>.</summary>
public sealed class AnimatedBuilder(IListenable animation, Func<BuildContext, Widget?, Widget> builder, Widget? child = null, Key? key = null) : StatefulWidget(key)
{
    internal IListenable Animation => animation;
    internal Func<BuildContext, Widget?, Widget> Builder => builder;
    internal Widget? Child => child;
    public override State CreateState() => new AnimatedBuilderState();
}
