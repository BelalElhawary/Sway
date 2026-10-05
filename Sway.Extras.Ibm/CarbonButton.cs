using SkiaSharp;
using Sway.Widgets;

namespace Sway.Extras.Ibm;

/// <summary>
/// Carbon's focus indicator: a 2px border drawn inside the control, not around it. The border layer is always present and
/// only its colour changes, so gaining focus never changes the shape of the tree (which would remount a focused text field).
/// </summary>
static class CarbonFocus
{
    public static Widget Around(bool visible, CarbonThemeData theme, Widget child, SKColor? color = null, float width = 2) =>
        new Stack([child, Positioned.Fill(new IgnorePointer(new DecoratedBox(
            new BoxDecoration(Border: Border.All(visible ? color ?? theme.Colors.Focus : Colors.Transparent, width)))))], clip: false);
}

public enum CarbonButtonKind { Primary, Secondary, Tertiary, Ghost, Danger, DangerTertiary, DangerGhost, GhostOnColor }

/// <summary>Carbon's button heights in logical pixels.</summary>
public enum CarbonButtonSize { Small = 32, Medium = 40, Large = 48, ExtraLarge = 64 }

/// <summary>
/// A Carbon button. Square, flat and sharp-cornered; filled kinds leave room for a trailing icon, ghost kinds hug their label.
/// <see cref="CarbonButtonKind.GhostOnColor"/> is for sitting on a coloured bar such as the data table's batch bar.
/// A null <c>onPressed</c> disables it.
/// </summary>
public sealed class CarbonButton(Widget child, Action? onPressed = null, CarbonButtonKind kind = CarbonButtonKind.Primary,
    CarbonButtonSize size = CarbonButtonSize.Large, IconData? icon = null, Key? key = null) : StatelessWidget(key)
{
    public override Widget Build(BuildContext context)
    {
        var theme = CarbonTheme.Of(context);
        var c = theme.Colors;
        bool enabled = onPressed is not null;
        bool ghost = kind is CarbonButtonKind.Ghost or CarbonButtonKind.DangerGhost or CarbonButtonKind.GhostOnColor;
        bool dark = c.Brightness == Brightness.Dark;

        return new Interactive((ctx, st) =>
        {
            SKColor bg = Colors.Transparent, fg;
            SKColor? border = null;
            switch (kind)
            {
                case CarbonButtonKind.Primary:
                    bg = st.Pressed ? c.ButtonPrimaryActive : st.Hover ? c.ButtonPrimaryHover : c.ButtonPrimary; fg = c.TextOnColor; break;
                case CarbonButtonKind.Secondary:
                    bg = st.Pressed ? c.ButtonSecondaryActive : st.Hover ? c.ButtonSecondaryHover : c.ButtonSecondary; fg = c.TextOnColor; break;
                case CarbonButtonKind.Danger:
                    bg = st.Pressed ? c.ButtonDangerActive : st.Hover ? c.ButtonDangerHover : c.ButtonDangerPrimary; fg = c.TextOnColor; break;
                case CarbonButtonKind.Tertiary:
                    border = c.ButtonTertiary; fg = c.ButtonTertiary;
                    if (st.Pressed) { bg = c.ButtonTertiaryActive; fg = dark ? c.TextInverse : c.TextOnColor; }
                    else if (st.Hover) { bg = c.ButtonTertiaryHover; fg = dark ? c.TextInverse : c.TextOnColor; }
                    break;
                case CarbonButtonKind.DangerTertiary:
                    border = c.ButtonDangerPrimary; fg = dark ? c.SupportError : c.ButtonDangerPrimary;
                    if (st.Pressed) { bg = c.ButtonDangerActive; fg = c.TextOnColor; }
                    else if (st.Hover) { bg = c.ButtonDangerHover; fg = c.TextOnColor; }
                    break;
                case CarbonButtonKind.DangerGhost:
                    fg = dark ? c.SupportError : c.ButtonDangerPrimary;
                    if (st.Pressed) { bg = c.ButtonDangerActive; fg = c.TextOnColor; }
                    else if (st.Hover) { bg = c.ButtonDangerHover; fg = c.TextOnColor; }
                    break;
                case CarbonButtonKind.GhostOnColor:
                    fg = c.TextOnColor;
                    bg = st.Pressed ? c.ButtonPrimaryActive : st.Hover ? c.ButtonPrimaryHover : Colors.Transparent; break;
                default: // Ghost
                    fg = c.LinkPrimary;
                    bg = st.Pressed ? c.BackgroundActive : st.Hover ? c.BackgroundHover : Colors.Transparent; break;
            }

            if (!enabled)
            {
                bool filled = kind is CarbonButtonKind.Primary or CarbonButtonKind.Secondary or CarbonButtonKind.Danger;
                bg = filled ? c.ButtonDisabled : Colors.Transparent;
                fg = filled ? c.TextOnColorDisabled : c.TextDisabled;
                border = border is null ? null : c.ButtonDisabled;
            }

            float height = (float)size;
            Widget label = DefaultTextStyle.Merge(ctx, theme.Type.BodyCompact01.Merge(new TextStyle(Color: fg)), child);
            Widget content = new Row(mainAxisSize: MainAxisSize.Min, crossAxisAlignment: CrossAxisAlignment.Center, children:
            [
                label,
                // Filled buttons keep Carbon's wide trailing gutter; the icon, when there is one, sits at the end of it.
                new SizedBox(width: ghost ? (icon is null ? 0 : 8) : icon is null ? 47 : 32),
                ..icon is null ? Array.Empty<Widget>() : [new Icon(icon, 16, fg)],
            ]);

            Widget body = new Container(height: height, padding: EdgeInsets.Symmetric(horizontal: 16),
                decoration: new BoxDecoration(Color: bg, Border: border is { } b ? Border.All(b) : null), child: content);
            return CarbonFocus.Around(st.FocusVisible, theme, body, kind == CarbonButtonKind.GhostOnColor ? c.FocusInverse : c.Focus);
        }, onPressed);
    }
}

/// <summary>A square, borderless button holding just an icon, like Carbon's ghost icon button.</summary>
public sealed class CarbonIconButton(IconData icon, Action? onPressed = null, CarbonButtonSize size = CarbonButtonSize.Medium,
    bool onColor = false, Key? key = null) : StatelessWidget(key)
{
    public override Widget Build(BuildContext context)
    {
        var theme = CarbonTheme.Of(context);
        var c = theme.Colors;
        bool enabled = onPressed is not null;
        return new Interactive((ctx, st) =>
        {
            var bg = st.Pressed ? (onColor ? c.ButtonPrimaryActive : c.BackgroundActive)
                : st.Hover ? (onColor ? c.ButtonPrimaryHover : c.BackgroundHover) : Colors.Transparent;
            var fg = !enabled ? c.IconDisabled : onColor ? c.IconOnColor : c.IconPrimary;
            Widget body = new Container(width: (float)size, height: (float)size, color: bg, child: new Center(new Icon(icon, 16, fg)));
            return CarbonFocus.Around(st.FocusVisible, theme, body, onColor ? c.FocusInverse : c.Focus);
        }, onPressed);
    }
}
