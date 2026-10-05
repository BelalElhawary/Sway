using SkiaSharp;

namespace Sway.Widgets;

sealed class AnimatedPhysicalModelState : ImplicitlyAnimatedWidgetState<AnimatedPhysicalModel>
{
    Tween<SKColor>? _color, _shadow;
    Tween<float>? _elevation;
    Tween<BorderRadius>? _radius;

    protected override void ForEachTween(ITweenVisitor v)
    {
        _color = v.VisitValue(_color, Widget.Color, a => new ColorTween(a, a));
        _shadow = v.VisitValue(_shadow, Widget.ShadowColor, a => new ColorTween(a, a));
        _elevation = v.VisitValue(_elevation, (float?)Widget.Elevation, a => new FloatTween(a, a));
        _radius = v.VisitValue(_radius, (BorderRadius?)Widget.Radius, a => new BorderRadiusTween(a, a));
    }

    /// <summary>Material elevation levels are discrete, so fractional elevations blend the two neighbouring levels.</summary>
    static IReadOnlyList<BoxShadow>? ShadowsFor(float elevation, SKColor shadow)
    {
        int low = (int)MathF.Floor(elevation);
        float t = elevation - low;
        var a = Elevation.Shadows(low, shadow);
        return t < 0.001f ? a : Lerps.BoxShadows(a, Elevation.Shadows(low + 1, shadow), t);
    }

    public override Widget Build(BuildContext context)
    {
        var radius = _radius!.Evaluate(Animation);
        Widget inner = Widget.Child ?? new SizedBox();
        if (!radius.IsZero) inner = new ClipRRect(radius, inner);
        return new DecoratedBox(new BoxDecoration(
            Color: _color?.Evaluate(Animation) ?? Colors.Transparent,
            BorderRadius: radius,
            BoxShadow: ShadowsFor(_elevation!.Evaluate(Animation), _shadow?.Evaluate(Animation) ?? Colors.Black)), inner);
    }
}
