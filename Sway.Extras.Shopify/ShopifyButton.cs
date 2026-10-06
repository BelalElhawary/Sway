using SkiaSharp;
using Sway.Widgets;

namespace Sway.Extras.Shopify;

/// <summary>
/// Keyboard focus ring: a 2px stroke drawn over the control. The layer is always present and only its colour changes, so gaining focus
/// never reshapes the tree (which would remount a focused text field).
/// </summary>
static class ShopifyFocus
{
    public static Widget Around(bool visible, ShopifyThemeData theme, Widget child, BorderRadius radius, float width = 2) =>
        new Stack([child, Positioned.Fill(new IgnorePointer(new DecoratedBox(
            new BoxDecoration(Border: Border.All(visible ? theme.Colors.Focus : Colors.Transparent, width), BorderRadius: radius))))], clip: false);
}

public enum ShopifyButtonKind { Primary, Outline, Aloe }

/// <summary>Button heights in logical pixels. Both clear the 44px touch target.</summary>
public enum ShopifyButtonSize { Medium = 44, Large = 52 }

/// <summary>
/// The pill button, the only button shape in the system. <see cref="ShopifyButtonKind.Primary"/> is the solid pill (black on the light track),
/// <see cref="ShopifyButtonKind.Outline"/> is the stroked pill (1px ink on light, 2px white on the cinematic track) and
/// <see cref="ShopifyButtonKind.Aloe"/> is the mint pill for the featured action. <paramref name="fill"/> stretches it to the full width, as
/// primary actions are on phones. A null <c>onPressed</c> disables it.
/// </summary>
public sealed class ShopifyButton(Widget child, Action? onPressed = null, ShopifyButtonKind kind = ShopifyButtonKind.Primary,
    ShopifyButtonSize size = ShopifyButtonSize.Medium, IconData? icon = null, bool fill = false, Key? key = null) : StatelessWidget(key)
{
    /// <summary>A button with a plain text label.</summary>
    public ShopifyButton(string label, Action? onPressed = null, ShopifyButtonKind kind = ShopifyButtonKind.Primary,
        ShopifyButtonSize size = ShopifyButtonSize.Medium, IconData? icon = null, bool fill = false, Key? key = null)
        : this(new Text(label, softWrap: false, overflow: TextOverflow.Ellipsis, maxLines: 1), onPressed, kind, size, icon, fill, key) { }

    public override Widget Build(BuildContext context)
    {
        var theme = ShopifyTheme.Of(context);
        var c = theme.Colors;
        bool enabled = onPressed is not null, dark = c.Brightness == Brightness.Dark;
        var radius = BorderRadius.Circular(999);

        return new Interactive((ctx, st) =>
        {
            SKColor bg, fg;
            Border? border = null;
            switch (kind)
            {
                case ShopifyButtonKind.Outline:
                    bg = st.Pressed ? c.Ink.WithOpacity(0.12f) : st.Hover ? c.Ink.WithOpacity(0.06f) : Colors.Transparent;
                    fg = c.Ink; border = Border.All(c.Ink, dark ? 2 : 1); break;
                case ShopifyButtonKind.Aloe:
                    bg = st.Pressed ? Mix(c.Aloe, Colors.Black, 0.12f) : st.Hover ? Mix(c.Aloe, Colors.Black, 0.06f) : c.Aloe;
                    fg = Colors.Black; break;
                default:
                    bg = st.Pressed ? c.PrimaryPressed : st.Hover ? Mix(c.Primary, c.OnPrimary, 0.12f) : c.Primary; fg = c.OnPrimary; break;
            }
            if (!enabled)
            {
                bg = kind == ShopifyButtonKind.Outline ? Colors.Transparent : c.Disabled; fg = c.OnDisabled;
                border = border is null ? null : Border.All(c.Disabled, border.Value.Top.Width);
            }

            Widget label = DefaultTextStyle.Merge(ctx, theme.Type.BodyMd.Merge(new TextStyle(Color: fg, FontWeight: 550)), child);
            Widget content = new Row(mainAxisSize: fill ? MainAxisSize.Max : MainAxisSize.Min, mainAxisAlignment: MainAxisAlignment.Center,
                crossAxisAlignment: CrossAxisAlignment.Center, spacing: 8, children:
            [
                ..icon is null ? Array.Empty<Widget>() : [new Icon(icon, 18, fg)],
                new Flexible(label),
            ]);
            Widget body = new Container(height: (float)size, padding: EdgeInsets.Symmetric(horizontal: 24),
                decoration: new BoxDecoration(Color: bg, Border: border, BorderRadius: radius), child: content);
            return ShopifyFocus.Around(st.FocusVisible, theme, body, radius);
        }, onPressed);
    }

    static SKColor Mix(SKColor a, SKColor b, float t) => new(
        (byte)(a.Red + (b.Red - a.Red) * t), (byte)(a.Green + (b.Green - a.Green) * t), (byte)(a.Blue + (b.Blue - a.Blue) * t), a.Alpha);
}

/// <summary>A round 44px icon button. <paramref name="badge"/> shows a count in the corner (hidden at zero), as on a cart icon.</summary>
public sealed class ShopifyIconButton(IconData icon, Action? onPressed = null, int badge = 0, bool filled = false, float iconSize = 22,
    Key? key = null) : StatelessWidget(key)
{
    public override Widget Build(BuildContext context)
    {
        var theme = ShopifyTheme.Of(context);
        var c = theme.Colors;
        bool enabled = onPressed is not null;
        var radius = BorderRadius.Circular(ShopifyBreakpoints.TouchTarget / 2);
        return new Interactive((ctx, st) =>
        {
            var bg = filled ? (st.Pressed ? c.PrimaryPressed : c.Primary) : st.Pressed ? c.Ink.WithOpacity(0.12f) : st.Hover ? c.Ink.WithOpacity(0.06f) : Colors.Transparent;
            var fg = !enabled ? c.OnDisabled : filled ? c.OnPrimary : c.Ink;
            Widget body = new Container(width: ShopifyBreakpoints.TouchTarget, height: ShopifyBreakpoints.TouchTarget,
                decoration: new BoxDecoration(Color: bg, BorderRadius: radius), child: new Center(new Icon(icon, iconSize, fg)));
            if (badge > 0)
                body = new Stack([body, new Positioned(new ShopifyCountBadge(badge), top: 2, right: 0)], clip: false);
            return ShopifyFocus.Around(st.FocusVisible, theme, body, radius);
        }, onPressed);
    }
}

/// <summary>A small count pill (aloe fill, ink text) for carts and notifications. Counts above 99 show as "99+".</summary>
public sealed class ShopifyCountBadge(int count, Key? key = null) : StatelessWidget(key)
{
    public override Widget Build(BuildContext context)
    {
        var theme = ShopifyTheme.Of(context);
        string text = count > 99 ? "99+" : count.ToString();
        return new Container(padding: EdgeInsets.Symmetric(horizontal: 5),
            constraints: new BoxConstraints(18, float.PositiveInfinity, 18, 18),
            decoration: new BoxDecoration(Color: theme.Colors.Aloe, BorderRadius: BorderRadius.Circular(9)),
            child: new Center(new Text(text, softWrap: false, maxLines: 1, style: theme.Type.Eyebrow.Merge(new TextStyle(Color: Colors.Black, FontWeight: 600, LetterSpacing: 0))), 1, 1));
    }
}

public enum ShopifyTagKind { Mint, Shade, Solid }

/// <summary>A small pill that labels something: mint for a feature or "new", shade for neutral facts, solid for "Sale". Optionally dismissible.</summary>
public sealed class ShopifyTag(string label, ShopifyTagKind kind = ShopifyTagKind.Mint, Action? onClose = null, Key? key = null) : StatelessWidget(key)
{
    public override Widget Build(BuildContext context)
    {
        var theme = ShopifyTheme.Of(context);
        var c = theme.Colors;
        var (bg, fg) = kind switch
        {
            ShopifyTagKind.Shade => (c.Shade, Colors.Black),
            ShopifyTagKind.Solid => (c.Primary, c.OnPrimary),
            _ => (c.Aloe, Colors.Black),
        };
        Widget text = new Text(label, softWrap: false, maxLines: 1, overflow: TextOverflow.Ellipsis, style: theme.Type.Eyebrow.Merge(new TextStyle(Color: fg)));
        Widget content = onClose is null ? text : new Row(mainAxisSize: MainAxisSize.Min, spacing: 6, children:
        [
            new Flexible(text),
            // The glyph is small, so its hit area is padded out to 28x32; a dismissible tag is 32px tall for that reason.
            new GestureDetector(onTap: onClose, behavior: HitTestBehavior.Opaque, child: new Padding(EdgeInsets.Symmetric(horizontal: 8, vertical: 10),
                new Icon(Icons.Close, 12, fg))),
        ]);
        return new Container(padding: EdgeInsets.Symmetric(horizontal: 12, vertical: onClose is null ? 4 : 0), constraints: onClose is null ? null : new BoxConstraints(0, float.PositiveInfinity, 32, float.PositiveInfinity),
            decoration: new BoxDecoration(Color: bg, BorderRadius: BorderRadius.Circular(999)), child: new Center(content, 1, 1));
    }
}
