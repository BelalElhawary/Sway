using SkiaSharp;

using Sway.Widgets;

namespace Sway.Extras.Material3;

/// <summary>A surface with colour, elevation (shadow), shape and optional border.</summary>
public sealed class Material(Widget? child = null, SKColor? color = null, int elevation = 0, BorderRadius? borderRadius = null,
    Border? border = null, bool clip = true, Key? key = null) : StatelessWidget(key)
{
    public override Widget Build(BuildContext context)
    {
        var scheme = Theme.Of(context).ColorScheme;
        var fill = color ?? scheme.Surface;
        Widget inner = child ?? new SizedBox();
        if (clip && borderRadius is { IsZero: false } r) inner = new ClipRRect(r, inner);
        return new DecoratedBox(new BoxDecoration(Color: fill, BorderRadius: borderRadius, Border: border, BoxShadow: Elevation.Shadows(elevation, scheme.Shadow)), inner);
    }
}
