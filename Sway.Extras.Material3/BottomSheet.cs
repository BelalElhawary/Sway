using Sway.Widgets;

namespace Sway.Extras.Material3;

/// <summary>The surface for <see cref="Panels.ShowBottomSheet"/>: rounded top corners and a drag handle.</summary>
public sealed class BottomSheet(Widget child, Key? key = null) : StatelessWidget(key)
{
    public override Widget Build(BuildContext context)
    {
        var s = Theme.Of(context).ColorScheme;
        var radius = new BorderRadius(new Radius(28, 28), new Radius(28, 28), new Radius(0, 0), new Radius(0, 0));
        return new ConstrainedBox(new BoxConstraints(0, 640, 0, float.PositiveInfinity),
            new DecoratedBox(new BoxDecoration(Color: s.SurfaceContainerLow, BorderRadius: radius, BoxShadow: Elevation.Shadows(1, s.Shadow)),
                new ClipRRect(radius, new Column(mainAxisSize: MainAxisSize.Min, crossAxisAlignment: CrossAxisAlignment.Stretch, children:
                [
                    new Padding(EdgeInsets.Symmetric(vertical: 16), new Center(new SizedBox(32, 4, new DecoratedBox(
                        new BoxDecoration(Color: s.OnSurfaceVariant.WithOpacity(0.4f), BorderRadius: BorderRadius.Circular(2)))))),
                    child,
                ]))));
    }
}
