using SkiaSharp;

namespace Sway.Widgets;

/// <summary>Lays out children in runs, wrapping to a new run when the main axis is full.</summary>
public sealed class Wrap(IReadOnlyList<Widget> children, Axis direction = Axis.Horizontal, WrapAlignment alignment = WrapAlignment.Start,
    float spacing = 0, WrapAlignment runAlignment = WrapAlignment.Start, float runSpacing = 0,
    WrapCrossAlignment crossAxisAlignment = WrapCrossAlignment.Start, TextDirection? textDirection = null,
    VerticalDirection verticalDirection = VerticalDirection.Down, Key? key = null) : MultiChildRenderObjectWidget(children, key)
{
    public override RenderObject CreateRenderObject(BuildContext context) => new RenderWrap(direction, alignment, spacing, runAlignment, runSpacing,
        crossAxisAlignment, textDirection ?? Directionality.Of(context), verticalDirection);

    public override void UpdateRenderObject(BuildContext context, RenderObject ro) => ((RenderWrap)ro).Update(direction, alignment, spacing, runAlignment,
        runSpacing, crossAxisAlignment, textDirection ?? Directionality.Of(context), verticalDirection);
}
