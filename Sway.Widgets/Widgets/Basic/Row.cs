using SkiaSharp;

namespace Sway.Widgets;

public sealed class Row(IReadOnlyList<Widget> children, MainAxisAlignment mainAxisAlignment = MainAxisAlignment.Start,
    MainAxisSize mainAxisSize = MainAxisSize.Max, CrossAxisAlignment crossAxisAlignment = CrossAxisAlignment.Center,
    TextDirection? textDirection = null, VerticalDirection verticalDirection = VerticalDirection.Down, float spacing = 0, Key? key = null)
    : StatelessWidget(key)
{
    public override Widget Build(BuildContext context) => new Flex(Axis.Horizontal, children, mainAxisAlignment, mainAxisSize,
        crossAxisAlignment, textDirection, verticalDirection, spacing);
}
