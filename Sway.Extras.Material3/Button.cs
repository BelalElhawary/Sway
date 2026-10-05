using SkiaSharp;

using Sway.Widgets;

namespace Sway.Extras.Material3;

public sealed class Button(Widget child, Action? onPressed = null, ButtonVariant variant = ButtonVariant.Filled, SKColor? color = null,
    IconData? icon = null, EdgeInsets? padding = null, Key? key = null) : StatelessWidget(key)
{
    public override Widget Build(BuildContext context)
    {
        var theme = Theme.Of(context);
        var s = theme.ColorScheme;
        var accent = color ?? s.Primary;
        bool enabled = onPressed is not null;
        var radius = BorderRadius.Circular(20);

        return new Interactive((ctx, st) =>
        {
            SKColor bg, fg;
            Border? border = null;
            int elevation = 0;
            switch (variant)
            {
                case ButtonVariant.Filled:
                    bg = color ?? s.Primary; fg = s.OnPrimary; elevation = st.Hover && !st.Pressed ? 1 : 0; break;
                case ButtonVariant.Tonal:
                    bg = s.SecondaryContainer; fg = s.OnSecondaryContainer; elevation = st.Hover && !st.Pressed ? 1 : 0; break;
                case ButtonVariant.Elevated:
                    bg = s.SurfaceContainerLow; fg = accent; elevation = st.Hover && !st.Pressed ? 2 : 1; break;
                case ButtonVariant.Outlined:
                    bg = Colors.Transparent; fg = accent; border = Border.All(st.FocusVisible ? accent : s.Outline); break;
                default:
                    bg = Colors.Transparent; fg = accent; break;
            }

            if (!enabled)
            {
                bool container = variant is ButtonVariant.Filled or ButtonVariant.Tonal or ButtonVariant.Elevated;
                bg = container ? s.OnSurface.WithOpacity(0.12f) : Colors.Transparent;
                fg = s.OnSurface.WithOpacity(0.38f);
                if (border is not null) border = Border.All(s.OnSurface.WithOpacity(0.12f));
                elevation = 0;
            }

            bool text = variant == ButtonVariant.Text;
            var pad = padding ?? EdgeInsets.Only(left: icon is not null ? (text ? 12 : 16) : (text ? 12 : 24), right: text ? 12 : 24);
            var layered = StateLayer.Blend(bg, fg, enabled ? StateLayer.Opacity(st) : 0);

            Widget label = DefaultTextStyle.Merge(ctx, theme.TextTheme.LabelLarge.Merge(new TextStyle(Color: fg)), child);
            Widget content = icon is null ? label : new Row(mainAxisSize: MainAxisSize.Min, spacing: 8, children: [new Icon(icon, 18, fg), label]);

            return new AnimatedContainer(TimeSpan.FromMilliseconds(120),
                constraints: new BoxConstraints(64, float.PositiveInfinity, 40, 40), padding: pad,
                decoration: new BoxDecoration(Color: layered, BorderRadius: radius, Border: border, BoxShadow: Elevation.Shadows(elevation, s.Shadow)),
                child: new Row(mainAxisSize: MainAxisSize.Min, mainAxisAlignment: MainAxisAlignment.Center, children: [content]));
        }, onPressed);
    }
}
