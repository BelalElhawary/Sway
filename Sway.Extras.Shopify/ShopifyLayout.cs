using SkiaSharp;
using Sway.Widgets;

namespace Sway.Extras.Shopify;

/// <summary>A thin black strip above the header for shipping offers and launches. The optional close target is a full 44px square.</summary>
public sealed class ShopifyAnnouncementBar(string text, string? actionLabel = null, Action? onAction = null, Action? onClose = null, Key? key = null) : StatelessWidget(key)
{
    public override Widget Build(BuildContext context)
    {
        var theme = ShopifyTheme.Of(context);
        var c = theme.Colors;
        var style = theme.Type.Caption.Merge(new TextStyle(Color: c.OnPrimary));
        Widget message = actionLabel is null
            ? new Text(text, textAlign: TextAlign.Center, style: style)
            : new Wrap([
                new Text(text, style: style),
                new Interactive((ctx, st) => new Text(actionLabel, style: style.Merge(new TextStyle(Decoration: TextDecoration.Underline, FontWeight: 600))), onAction),
            ], alignment: WrapAlignment.Center, spacing: 8, runSpacing: 0);
        return new Container(color: c.Primary, constraints: new BoxConstraints(0, float.PositiveInfinity, 40, float.PositiveInfinity),
            padding: EdgeInsets.Symmetric(horizontal: onClose is null ? 16 : 0, vertical: 10), child: new Row(crossAxisAlignment: CrossAxisAlignment.Center, children:
            [
                ..onClose is null ? Array.Empty<Widget>() : [new SizedBox(width: ShopifyBreakpoints.TouchTarget)],
                new Expanded(new Center(message, heightFactor: 1)),
                ..onClose is null ? Array.Empty<Widget>() : [new GestureDetector(onTap: onClose, behavior: HitTestBehavior.Opaque,
                    child: new SizedBox(width: ShopifyBreakpoints.TouchTarget, height: ShopifyBreakpoints.TouchTarget, child: new Center(new Icon(Icons.Close, 16, c.OnPrimary))))],
            ]));
    }
}

public sealed record ShopifyNavLink(string Label, Action? OnPressed = null);

/// <summary>
/// The top bar. On phones: menu button, centred title, search and cart. From tablet width up: title on the left, links in the middle, actions on
/// the right. The cart shows a count badge. Pass <paramref name="onMenu"/> to get the hamburger when links don't fit.
/// </summary>
public sealed class ShopifyHeader(Widget title, IReadOnlyList<ShopifyNavLink>? links = null, Action? onMenu = null, Action? onSearch = null,
    Action? onCart = null, int cartCount = 0, Action? onAccount = null, Key? key = null) : StatelessWidget(key)
{
    public override Widget Build(BuildContext context)
    {
        var theme = ShopifyTheme.Of(context);
        var c = theme.Colors;

        return new ShopifyResponsive((ctx, bp, width) =>
        {
            bool compact = bp == ShopifyBreakpoint.Mobile;
            Widget brand = DefaultTextStyle.Merge(ctx, theme.Type.HeadingMd.Merge(new TextStyle(Color: c.Ink)), title);
            var actions = new List<Widget>();
            if (onSearch is not null) actions.Add(new ShopifyIconButton(Icons.Search, onSearch));
            if (onAccount is not null && !compact) actions.Add(new ShopifyIconButton(Icons.Person, onAccount));
            if (onCart is not null) actions.Add(new ShopifyIconButton(Icons.ShoppingBag, onCart, cartCount));

            Widget body;
            if (compact)
            {
                body = new Row(crossAxisAlignment: CrossAxisAlignment.Center, children:
                [
                    onMenu is not null ? new ShopifyIconButton(Icons.Menu, onMenu) : new SizedBox(width: 0),
                    new Expanded(new Align(onMenu is not null ? Alignment.Center : Alignment.CenterLeft, brand)),
                    ..actions,
                ]);
            }
            else
            {
                body = new Row(crossAxisAlignment: CrossAxisAlignment.Center, spacing: 24, children:
                [
                    brand,
                    new Expanded(new Wrap((links ?? []).Select(l => LinkItem(theme, l)).ToList(), spacing: 4, runSpacing: 0)),
                    new Row(mainAxisSize: MainAxisSize.Min, children: actions),
                ]);
            }
            return new Container(height: compact ? 56 : 72, padding: EdgeInsets.Symmetric(horizontal: ShopifyBreakpoints.Gutter(bp) - (compact ? 8 : 0)),
                decoration: new BoxDecoration(Color: c.Canvas, Border: Border.Only(bottom: new BorderSide(c.Hairline, 1))), child: body);
        });
    }

    static Widget LinkItem(ShopifyThemeData theme, ShopifyNavLink link) => new Interactive((ctx, st) => ShopifyFocus.Around(st.FocusVisible, theme,
        new Container(height: ShopifyBreakpoints.TouchTarget, padding: EdgeInsets.Symmetric(horizontal: 12),
            child: new Center(new Text(link.Label, softWrap: false, maxLines: 1, style: theme.Type.BodyMd.Merge(new TextStyle(
                Color: theme.Colors.Ink, FontWeight: st.Hover ? 550 : 420, Decoration: st.Hover ? TextDecoration.Underline : TextDecoration.None))), 1, 1)),
        BorderRadius.Circular(22)), link.OnPressed);
}

public sealed record ShopifyNavDestination(IconData Icon, string Label, IconData? SelectedIcon = null, int Badge = 0);

/// <summary>
/// A phone's bottom tab bar: icon over label, one equal-width 64px target per destination, a count badge where there is one.
/// Give <paramref name="bottomInset"/> the device's bottom safe-area (home indicator) height so the targets clear it.
/// </summary>
public sealed class ShopifyBottomNavBar(IReadOnlyList<ShopifyNavDestination> destinations, int selectedIndex, Action<int>? onSelected = null,
    float bottomInset = 0, Key? key = null) : StatelessWidget(key)
{
    public override Widget Build(BuildContext context)
    {
        var theme = ShopifyTheme.Of(context);
        var c = theme.Colors;
        bool dark = c.Brightness == Brightness.Dark;

        Widget Item(int i, ShopifyNavDestination d)
        {
            bool sel = i == selectedIndex;
            var fg = sel ? c.Ink : c.InkSecondary;
            return new Expanded(new Interactive((ctx, st) => ShopifyFocus.Around(st.FocusVisible, theme,
                new Container(height: 64, alignment: Alignment.Center, color: st.Pressed ? c.Ink.WithOpacity(0.06f) : Colors.Transparent, child: new Column(
                    mainAxisSize: MainAxisSize.Min, spacing: 2, children:
                [
                    new Stack([
                        new Container(width: 56, height: 28, alignment: Alignment.Center,
                            decoration: new BoxDecoration(Color: sel ? (dark ? c.Hairline : c.Aloe) : Colors.Transparent, BorderRadius: BorderRadius.Circular(14)),
                            child: new Icon(sel ? d.SelectedIcon ?? d.Icon : d.Icon, 22, sel && !dark ? Colors.Black : fg)),
                        ..d.Badge > 0 ? [new Positioned(new ShopifyCountBadge(d.Badge), top: -4, right: 0)] : Array.Empty<Widget>(),
                    ], clip: false),
                    new Text(d.Label, softWrap: false, maxLines: 1, overflow: TextOverflow.Ellipsis,
                        style: theme.Type.Eyebrow.Merge(new TextStyle(Color: fg, FontWeight: sel ? 600 : 500, LetterSpacing: 0))),
                ])), BorderRadius.Circular(8)), () => onSelected?.Invoke(i)));
        }

        return new Container(padding: EdgeInsets.Only(bottom: bottomInset), decoration: new BoxDecoration(Color: c.Surface,
            Border: Border.Only(top: new BorderSide(c.Hairline, 1))), child: new Row(children: destinations.Select((d, i) => Item(i, d)).ToList()));
    }
}

/// <summary>A section title with an optional eyebrow above it and a "View all" link on the end. The link is a 44px-tall target.</summary>
public sealed class ShopifySectionHeader(string title, string? eyebrow = null, string? actionLabel = null, Action? onAction = null, Key? key = null) : StatelessWidget(key)
{
    public override Widget Build(BuildContext context)
    {
        var theme = ShopifyTheme.Of(context);
        var c = theme.Colors;
        return new ShopifyResponsive((ctx, bp, width) => new Row(crossAxisAlignment: CrossAxisAlignment.End, spacing: 16, children:
        [
            new Expanded(new Column(crossAxisAlignment: CrossAxisAlignment.Start, mainAxisSize: MainAxisSize.Min, spacing: 6, children:
            [
                ..eyebrow is null ? Array.Empty<Widget>() : [new Text(eyebrow.ToUpperInvariant(), style: theme.Type.Eyebrow.Merge(new TextStyle(Color: c.InkSecondary)))],
                new Text(title, style: theme.Type.Responsive(theme.Type.DisplayMd, bp).Merge(new TextStyle(Color: c.Ink, FontSize: bp == ShopifyBreakpoint.Mobile ? 28 : null))),
            ])),
            ..actionLabel is null ? Array.Empty<Widget>() : [new Interactive((ictx, st) => ShopifyFocus.Around(st.FocusVisible, theme,
                new Container(height: ShopifyBreakpoints.TouchTarget, padding: EdgeInsets.Symmetric(horizontal: 4),
                    child: new Row(mainAxisSize: MainAxisSize.Min, spacing: 4, children:
                    [
                        new Text(actionLabel, softWrap: false, maxLines: 1, style: theme.Type.BodyStrong.Merge(new TextStyle(Color: c.Ink,
                            Decoration: st.Hover ? TextDecoration.Underline : TextDecoration.None))),
                        new Icon(Icons.ArrowForward, 16, c.Ink),
                    ])), BorderRadius.Circular(8)), onAction)],
        ]));
    }
}

/// <summary>
/// The cinematic hero: a black band, a thin display headline, one outline pill, and full-bleed imagery. Phones stack the text over the image and
/// stretch the buttons; wider layouts put them side by side. The headline steps down from 70px to 44px on phones (96 to 56 when
/// <paramref name="large"/>). The band always uses the night colours, whatever theme the app is in.
/// </summary>
public sealed class ShopifyHero(string headline, string? body = null, string? ctaLabel = null, Action? onCta = null, string? secondaryLabel = null,
    Action? onSecondary = null, string? eyebrow = null, Widget? image = null, bool large = false, Key? key = null) : StatelessWidget(key)
{
    public override Widget Build(BuildContext context)
    {
        var type = ShopifyTheme.Of(context).Type;
        var night = new ShopifyThemeData(ShopifyColors.Night, type);
        var c = night.Colors;

        return ShopifyTheme.Scope(night, new ColoredBox(c.Canvas, new ShopifyResponsive((ctx, bp, width) =>
        {
            bool stacked = bp != ShopifyBreakpoint.Desktop;
            bool phone = bp == ShopifyBreakpoint.Mobile;
            float gutter = ShopifyBreakpoints.Gutter(bp);

            Widget text = new Padding(EdgeInsets.Symmetric(horizontal: gutter, vertical: phone ? 40 : 64),
                new Column(crossAxisAlignment: CrossAxisAlignment.Stretch, mainAxisSize: MainAxisSize.Min, spacing: 20, children:
                [
                    ..eyebrow is null ? Array.Empty<Widget>() : [new Text(eyebrow.ToUpperInvariant(), style: type.Eyebrow.Merge(new TextStyle(Color: c.InkTertiary)))],
                    new Text(headline, style: type.Responsive(large ? type.DisplayXxl : type.DisplayXl, bp).Merge(new TextStyle(Color: c.Ink))),
                    ..body is null ? Array.Empty<Widget>() : [new Text(body, style: type.BodyLg.Merge(new TextStyle(Color: c.InkSecondary, FontWeight: 420)))],
                    ..ctaLabel is null && secondaryLabel is null ? Array.Empty<Widget>() :
                    [
                        new SizedBox(height: 4),
                        new Wrap([
                            ..ctaLabel is null ? Array.Empty<Widget>() : [Cta(ctaLabel, onCta, phone, ShopifyButtonKind.Outline)],
                            ..secondaryLabel is null ? Array.Empty<Widget>() : [Cta(secondaryLabel, onSecondary, phone, ShopifyButtonKind.Primary)],
                        ], spacing: 12, runSpacing: 12),
                    ],
                ]));

            // Full-bleed: the photo runs to the edges of the band, with no radius and no inner padding.
            if (image is null) return text;
            Widget photo = new AspectRatio(stacked ? (phone ? 1f : 1.6f) : 0.9f, image);
            return stacked
                ? new Column(crossAxisAlignment: CrossAxisAlignment.Stretch, mainAxisSize: MainAxisSize.Min, children: [text, photo])
                : new Row(crossAxisAlignment: CrossAxisAlignment.Center, children: [new Expanded(text), new Expanded(photo)]);
        })));
    }

    // A full-width pill on phones needs a width to fill; a SizedBox with the parent's width does that without making the Wrap stretch its siblings.
    static Widget Cta(string label, Action? onPressed, bool fill, ShopifyButtonKind kind) =>
        fill ? new SizedBox(width: float.PositiveInfinity, child: new ShopifyButton(label, onPressed, kind, ShopifyButtonSize.Large, fill: true))
             : new ShopifyButton(label, onPressed, kind, ShopifyButtonSize.Large);
}

public sealed record ShopifyAccordionItem(string Title, Widget Content);

/// <summary>Collapsible panels for product details, shipping and returns. Rows are 56px tall; hairlines separate them.</summary>
public sealed class ShopifyAccordion(IReadOnlyList<ShopifyAccordionItem> items, bool allowMultiple = false, IReadOnlyCollection<int>? initiallyOpen = null, Key? key = null)
    : StatefulWidget(key)
{
    internal IReadOnlyList<ShopifyAccordionItem> Items => items;
    internal bool AllowMultiple => allowMultiple;
    internal IReadOnlyCollection<int>? InitiallyOpen => initiallyOpen;
    public override State CreateState() => new ShopifyAccordionState();
}

sealed class ShopifyAccordionState : State<ShopifyAccordion>
{
    readonly HashSet<int> _open = new();

    public override void InitState()
    {
        foreach (var i in Widget.InitiallyOpen ?? []) _open.Add(i);
    }

    void Toggle(int i) => SetState(() =>
    {
        if (_open.Remove(i)) return;
        if (!Widget.AllowMultiple) _open.Clear();
        _open.Add(i);
    });

    public override Widget Build(BuildContext context)
    {
        var theme = ShopifyTheme.Of(context);
        var c = theme.Colors;
        var rows = new List<Widget> { new Container(height: 1, color: c.Hairline) };
        for (int n = 0; n < Widget.Items.Count; n++)
        {
            int i = n;
            var item = Widget.Items[n];
            bool open = _open.Contains(i);
            rows.Add(new Interactive((ctx, st) => ShopifyFocus.Around(st.FocusVisible, theme, new Container(height: 56, color: st.Hover ? c.Ink.WithOpacity(0.04f) : Colors.Transparent,
                child: new Row(crossAxisAlignment: CrossAxisAlignment.Center, spacing: 12, children:
                [
                    new Expanded(new Text(item.Title, maxLines: 1, overflow: TextOverflow.Ellipsis, style: theme.Type.BodyStrong.Merge(new TextStyle(Color: c.Ink)))),
                    new Icon(open ? Icons.ExpandLess : Icons.ExpandMore, 22, c.Ink),
                ])), BorderRadius.Circular(4)), () => Toggle(i)));
            if (open) rows.Add(new Padding(EdgeInsets.Only(bottom: 20),
                DefaultTextStyle.Merge(context, theme.Type.BodyMd.Merge(new TextStyle(Color: c.InkSecondary)), item.Content)));
            rows.Add(new Container(height: 1, color: c.Hairline));
        }
        return new Column(crossAxisAlignment: CrossAxisAlignment.Stretch, mainAxisSize: MainAxisSize.Min, children: rows);
    }
}
