using SkiaSharp;

using Sway.Widgets;

namespace Sway.Extras.Material3;

public sealed class Card(Widget? child = null, CardVariant variant = CardVariant.Elevated, EdgeInsets? margin = null, SKColor? color = null,
    Action? onTap = null, Key? key = null) : StatelessWidget(key)
{
    public override Widget Build(BuildContext context)
    {
        var s = Theme.Of(context).ColorScheme;
        var radius = BorderRadius.Circular(Shapes.Medium);
        var (bg, elev, border) = variant switch
        {
            CardVariant.Filled => (s.SurfaceContainerHighest, 0, (Border?)null),
            CardVariant.Outlined => (s.Surface, 0, Border.All(s.OutlineVariant)),
            _ => (s.SurfaceContainerLow, 1, null),
        };
        bg = color ?? bg;
        Widget card = onTap is null
            ? new Material(child, bg, elev, radius, border)
            : new Interactive((ctx, st) => new AnimatedContainer(TimeSpan.FromMilliseconds(120),
                    decoration: new BoxDecoration(Color: StateLayer.Blend(bg, s.OnSurface, StateLayer.Opacity(st)), BorderRadius: radius, Border: border,
                        BoxShadow: Elevation.Shadows(st.Hover && variant == CardVariant.Elevated ? 2 : elev, s.Shadow)),
                    child: new ClipRRect(radius, child ?? new SizedBox())), onTap);
        return new Padding(margin ?? EdgeInsets.All(4), card);
    }
}
