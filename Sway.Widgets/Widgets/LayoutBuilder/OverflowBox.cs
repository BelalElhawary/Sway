namespace Sway.Widgets;

/// <summary>
/// Lays its child out with different constraints than it received, letting it overflow the parent.
/// Null limits keep the incoming constraint on that side.
/// </summary>
public sealed class OverflowBox(Widget? child = null, IAlignment? alignment = null, float? minWidth = null, float? maxWidth = null,
    float? minHeight = null, float? maxHeight = null, Key? key = null) : SingleChildRenderObjectWidget(child, key)
{
    BoxConstraints? Limits => minWidth is null && maxWidth is null && minHeight is null && maxHeight is null
        ? null : new BoxConstraints(minWidth ?? 0, maxWidth ?? float.PositiveInfinity, minHeight ?? 0, maxHeight ?? float.PositiveInfinity);

    Alignment Resolve(BuildContext c) => (alignment ?? Alignment.Center).Resolve(Directionality.Of(c));

    public override RenderObject CreateRenderObject(BuildContext context) =>
        new RenderOverflowBox(Resolve(context), minWidth, maxWidth, minHeight, maxHeight);

    public override void UpdateRenderObject(BuildContext context, RenderObject ro) =>
        ((RenderOverflowBox)ro).Update(Resolve(context), minWidth, maxWidth, minHeight, maxHeight);
}
