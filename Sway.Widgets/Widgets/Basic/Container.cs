using SkiaSharp;

namespace Sway.Widgets;

public sealed class Container(
    Widget? child = null,
    IAlignment? alignment = null,
    EdgeInsets? padding = null,
    SKColor? color = null,
    BoxDecoration? decoration = null,
    BoxDecoration? foregroundDecoration = null,
    float? width = null,
    float? height = null,
    BoxConstraints? constraints = null,
    EdgeInsets? margin = null,
    SKMatrix? transform = null,
    Key? key = null) : StatelessWidget(key)
{
    public override Widget Build(BuildContext context)
    {
        if (color is not null && decoration is not null)
            throw new ArgumentException("Cannot provide both a color and a decoration; put the color inside the BoxDecoration.");

        var current = child;
        if (child is null && (constraints is null || !constraints.Value.IsTight))
            current = new LimitedBox(0, 0, new ConstrainedBox(BoxConstraints.Expand()));
        else if (alignment is not null)
            current = new Align(alignment, current);

        var effectivePadding = padding;
        var effectiveDecoration = decoration ?? (color is { } c ? new BoxDecoration(Color: c) : null);
        if (effectiveDecoration is not null && effectiveDecoration.Padding != EdgeInsets.Zero)
            effectivePadding = (effectivePadding ?? EdgeInsets.Zero) + effectiveDecoration.Padding;
        if (effectivePadding is { } p) current = new Padding(p, current);
        if (effectiveDecoration is not null) current = new DecoratedBox(effectiveDecoration, current);
        if (foregroundDecoration is not null) current = new DecoratedBox(foregroundDecoration, current, foreground: true);

        var effectiveConstraints = constraints;
        if (width is not null || height is not null)
            effectiveConstraints = (effectiveConstraints ?? new BoxConstraints(0, float.PositiveInfinity, 0, float.PositiveInfinity)).Tighten(width, height);
        if (effectiveConstraints is { } ec) current = new ConstrainedBox(ec, current);
        if (margin is { } m) current = new Padding(m, current);
        if (transform is { } t) current = new Transform(t, current);
        return current!;
    }
}
