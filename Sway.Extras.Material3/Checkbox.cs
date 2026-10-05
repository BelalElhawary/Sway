using SkiaSharp;

using Sway.Widgets;

namespace Sway.Extras.Material3;

public sealed class Checkbox(bool value, Action<bool>? onChanged = null, SKColor? activeColor = null, Key? key = null) : StatelessWidget(key)
{
    public override Widget Build(BuildContext context)
    {
        var s = Theme.Of(context).ColorScheme;
        var active = activeColor ?? s.Primary;
        bool enabled = onChanged is not null;
        return new Interactive((ctx, st) =>
        {
            var off = s.OnSurface.WithOpacity(0.38f);
            var boxColor = value ? (enabled ? active : off) : Colors.Transparent;
            var border = value ? boxColor : enabled ? s.OnSurfaceVariant : off;
            Widget box = new AnimatedContainer(TimeSpan.FromMilliseconds(120), width: 18, height: 18,
                decoration: new BoxDecoration(Color: boxColor, BorderRadius: BorderRadius.Circular(2), Border: Border.All(border, 2)),
                child: value ? new CustomPaint(new CheckPainter(s.OnPrimary), size: new Size(14, 14)) : null);
            return Halo.Wrap(st, value ? active : s.OnSurface, enabled, box);
        }, enabled ? () => onChanged!(!value) : null);
    }
}
