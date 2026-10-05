using SkiaSharp;

using Sway.Widgets;

namespace Sway.Extras.Material3;

public sealed class Radio<T>(T value, T? groupValue, Action<T>? onChanged = null, SKColor? activeColor = null, Key? key = null) : StatelessWidget(key)
    where T : notnull
{
    public override Widget Build(BuildContext context)
    {
        var s = Theme.Of(context).ColorScheme;
        bool selected = EqualityComparer<T>.Default.Equals(value, groupValue);
        var active = activeColor ?? s.Primary;
        bool enabled = onChanged is not null;
        return new Interactive((ctx, st) =>
        {
            var off = s.OnSurface.WithOpacity(0.38f);
            var ring = !enabled ? off : selected ? active : s.OnSurfaceVariant;
            Widget mark = new AnimatedContainer(TimeSpan.FromMilliseconds(120), width: 20, height: 20, alignment: Alignment.Center,
                decoration: new BoxDecoration(Shape: BoxShape.Circle, Border: Border.All(ring, 2)),
                child: new AnimatedContainer(TimeSpan.FromMilliseconds(120), width: selected ? 10 : 0, height: selected ? 10 : 0,
                    decoration: new BoxDecoration(Color: enabled ? active : off, Shape: BoxShape.Circle)));
            return Halo.Wrap(st, selected ? active : s.OnSurface, enabled, mark);
        }, enabled ? () => onChanged!(value) : null);
    }
}
