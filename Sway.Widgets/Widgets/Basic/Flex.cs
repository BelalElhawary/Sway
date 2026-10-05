using SkiaSharp;

namespace Sway.Widgets;

public sealed class Flex(Axis direction, IReadOnlyList<Widget> children,
    MainAxisAlignment mainAxisAlignment = MainAxisAlignment.Start, MainAxisSize mainAxisSize = MainAxisSize.Max,
    CrossAxisAlignment crossAxisAlignment = CrossAxisAlignment.Center, TextDirection? textDirection = null,
    VerticalDirection verticalDirection = VerticalDirection.Down, float spacing = 0, Key? key = null) : MultiChildRenderObjectWidget(children, key)
{
    public override RenderObject CreateRenderObject(BuildContext context) => new RenderFlex(direction, mainAxisAlignment, mainAxisSize,
        crossAxisAlignment, textDirection ?? Directionality.Of(context), verticalDirection, spacing);

    public override void UpdateRenderObject(BuildContext context, RenderObject ro) => ((RenderFlex)ro).Update(direction, mainAxisAlignment,
        mainAxisSize, crossAxisAlignment, textDirection ?? Directionality.Of(context), verticalDirection, spacing);
}
