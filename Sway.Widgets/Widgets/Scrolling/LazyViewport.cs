using SkiaSharp;

namespace Sway.Widgets;

public sealed class LazyViewport(Axis axis, ScrollPosition position, int itemCount, Func<BuildContext, int, Widget> builder,
    float? itemExtent, EdgeInsets padding, Key? key = null) : RenderObjectWidget(key)
{
    internal Axis Axis => axis;
    internal ScrollPosition Position => position;
    internal int ItemCount => itemCount;
    internal Func<BuildContext, int, Widget> Builder => builder;
    internal float? ItemExtent => itemExtent;
    internal EdgeInsets Padding => padding;

    public override Element CreateElement() => new LazyViewportElement(this);
    public override RenderObject CreateRenderObject(BuildContext context) => new RenderLazyViewport();
}
