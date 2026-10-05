using SkiaSharp;

using Sway.Widgets;

namespace Sway.Extras.Material3;

public sealed class Switch(bool value, Action<bool>? onChanged = null, SKColor? activeColor = null, Key? key = null) : StatelessWidget(key)
{
    public override Widget Build(BuildContext context)
    {
        var s = Theme.Of(context).ColorScheme;
        bool enabled = onChanged is not null;
        var ms = TimeSpan.FromMilliseconds(180);
        return new Interactive((ctx, st) =>
        {
            var trackOn = activeColor ?? s.Primary;
            var track = value ? trackOn : s.SurfaceContainerHighest;
            var outline = value ? trackOn : s.Outline;
            var handle = value ? s.OnPrimary : s.Outline;
            if (!enabled)
            {
                track = value ? s.OnSurface.WithOpacity(0.12f) : s.SurfaceContainerHighest.WithOpacity(0.12f);
                outline = s.OnSurface.WithOpacity(0.12f);
                handle = value ? s.Surface : s.OnSurface.WithOpacity(0.38f);
            }
            float size = st.Pressed ? 28 : value ? 24 : 16;

            // The 40px halo is positioned around the handle without enlarging the 24px slot it lives in.
            Widget thumb = new SizedBox(24, 24, new Stack([
                Positioned.Fill(new Center(new AnimatedContainer(ms, width: size, height: size, curve: Curves.EaseOutCubic,
                    decoration: new BoxDecoration(Color: handle, Shape: BoxShape.Circle)))),
                new Positioned(new IgnorePointer(new AnimatedContainer(TimeSpan.FromMilliseconds(100), decoration: new BoxDecoration(
                    Color: (value ? trackOn : s.OnSurface).WithOpacity(enabled ? StateLayer.Opacity(st) : 0), Shape: BoxShape.Circle))),
                    left: -8, top: -8, width: 40, height: 40),
            ], alignment: Alignment.Center, clip: false));

            // The track is 32px tall with a 2px border, leaving 28px for the 24px handle (28px when pressed) without vertical padding.
            return new AnimatedContainer(ms, width: 52, height: 32, padding: EdgeInsets.Symmetric(horizontal: 4, vertical: 0), curve: Curves.EaseOutCubic,
                decoration: new BoxDecoration(Color: track, BorderRadius: BorderRadius.Circular(16), Border: Border.All(outline, 2)),
                child: new AnimatedAlign(value ? Alignment.CenterRight : Alignment.CenterLeft, ms, thumb, Curves.EaseOutCubic));
        }, enabled ? () => onChanged!(!value) : null);
    }
}
