using SkiaSharp;

using Sway.Widgets;

namespace Sway.Extras.Material3;

public sealed class IconButton(Widget icon, Action? onPressed = null, IconButtonVariant variant = IconButtonVariant.Standard,
    bool selected = false, Key? key = null) : StatelessWidget(key)
{
    public override Widget Build(BuildContext context)
    {
        var s = Theme.Of(context).ColorScheme;
        bool enabled = onPressed is not null;
        return new Interactive((ctx, st) =>
        {
            SKColor bg = Colors.Transparent, fg = selected ? s.Primary : s.OnSurfaceVariant;
            Border? border = null;
            switch (variant)
            {
                case IconButtonVariant.Filled:
                    bg = selected || variant == IconButtonVariant.Filled ? s.Primary : s.SurfaceContainerHighest; fg = s.OnPrimary; break;
                case IconButtonVariant.Tonal:
                    bg = s.SecondaryContainer; fg = s.OnSecondaryContainer; break;
                case IconButtonVariant.Outlined:
                    border = Border.All(s.Outline); bg = selected ? s.InverseSurface : Colors.Transparent; fg = selected ? s.OnInverseSurface : s.OnSurfaceVariant; break;
            }
            if (!enabled) { fg = s.OnSurface.WithOpacity(0.38f); bg = bg.Alpha == 0 ? bg : s.OnSurface.WithOpacity(0.12f); }
            var layered = StateLayer.Blend(bg, fg, enabled ? StateLayer.Opacity(st) : 0);
            Widget body = new AnimatedContainer(TimeSpan.FromMilliseconds(120), width: 40, height: 40, alignment: Alignment.Center,
                decoration: new BoxDecoration(Color: layered, Shape: BoxShape.Circle, Border: border), child: new IconTheme(fg, 24, icon));
            return FocusRing.Around(st.FocusVisible, BorderRadius.Circular(100), body, s.Primary);
        }, onPressed);
    }
}
