using System.Globalization;
using SkiaSharp;
using Sway.Widgets;

namespace Sway.Extras.Shopify;

/// <summary>Money formatting shared by the price, cart and summary components.</summary>
public static class ShopifyMoney
{
    /// <summary>Formats <paramref name="amount"/> with two decimals and a leading <paramref name="symbol"/>, e.g. <c>$1,299.00</c> or <c>-$5.00</c>.</summary>
    public static string Format(decimal amount, string symbol = "$") =>
        (amount < 0 ? "-" : "") + symbol + Math.Abs(amount).ToString("N2", CultureInfo.InvariantCulture);
}

/// <summary>
/// A price. Give <paramref name="compareAt"/> (the original, higher price) to show the current price beside it struck through.
/// <paramref name="large"/> uses the display face, for the product page.
/// </summary>
public sealed class ShopifyPrice(decimal amount, decimal? compareAt = null, string currency = "$", bool large = false, Key? key = null) : StatelessWidget(key)
{
    public override Widget Build(BuildContext context)
    {
        var theme = ShopifyTheme.Of(context);
        var c = theme.Colors;
        bool onSale = compareAt is { } was && was > amount;
        var main = (large ? theme.Type.HeadingXl : theme.Type.BodyStrong).Merge(new TextStyle(Color: c.Ink));
        var old = (large ? theme.Type.BodyLg : theme.Type.Caption).Merge(new TextStyle(Color: c.InkTertiary, Decoration: TextDecoration.LineThrough, FontWeight: 420));
        // Wrap rather than Row: in a narrow two-up phone grid the struck price drops to its own line instead of overflowing.
        return new Wrap([
            new Text(ShopifyMoney.Format(amount, currency), style: main),
            ..onSale ? [new Text(ShopifyMoney.Format(compareAt!.Value, currency), style: old)] : Array.Empty<Widget>(),
        ], spacing: 8, runSpacing: 2, crossAxisAlignment: WrapCrossAlignment.Center);
    }
}

/// <summary>Five stars for a rating out of five, with an optional review count: <c>★★★★½ 4.5 (128)</c>.</summary>
public sealed class ShopifyRating(double rating, int? reviewCount = null, float starSize = 14, Key? key = null) : StatelessWidget(key)
{
    public override Widget Build(BuildContext context)
    {
        var theme = ShopifyTheme.Of(context);
        var c = theme.Colors;
        double r = Math.Clamp(rating, 0, 5);
        var stars = new List<Widget>();
        for (int i = 1; i <= 5; i++)
        {
            var icon = r >= i - 0.25 ? Icons.Star : r >= i - 0.75 ? Icons.StarHalf : Icons.StarBorder;
            stars.Add(new Icon(icon, starSize, c.Ink));
        }
        return new Row(mainAxisSize: MainAxisSize.Min, crossAxisAlignment: CrossAxisAlignment.Center, spacing: 6, children:
        [
            new Row(mainAxisSize: MainAxisSize.Min, children: stars),
            new Text(reviewCount is { } n ? $"{r:0.0} ({n})" : $"{r:0.0}", style: theme.Type.Micro.Merge(new TextStyle(Color: c.InkSecondary))),
        ]);
    }
}

/// <summary>
/// A pill with minus and plus targets of 44px each. <paramref name="onChanged"/> receives the new quantity, already clamped to
/// [<paramref name="min"/>, <paramref name="max"/>]; the button that would leave the range is disabled.
/// </summary>
public sealed class ShopifyQuantityStepper(int value, Action<int>? onChanged, int min = 1, int max = 99, Key? key = null) : StatelessWidget(key)
{
    public override Widget Build(BuildContext context)
    {
        var theme = ShopifyTheme.Of(context);
        var c = theme.Colors;
        const float t = ShopifyBreakpoints.TouchTarget;

        Widget Step(IconData icon, bool enabled, int next) => new Interactive((ctx, st) => ShopifyFocus.Around(st.FocusVisible, theme,
            new Container(width: t, height: t, decoration: new BoxDecoration(
                Color: st.Pressed && enabled ? c.Ink.WithOpacity(0.12f) : st.Hover && enabled ? c.Ink.WithOpacity(0.06f) : Colors.Transparent,
                BorderRadius: BorderRadius.Circular(t / 2)),
                child: new Center(new Icon(icon, 18, enabled ? c.Ink : c.OnDisabled))), BorderRadius.Circular(t / 2)),
            enabled && onChanged is not null ? () => onChanged(Math.Clamp(next, min, max)) : null);

        // The 1px border sits inside the box, so the pill is 2px taller than its 44px targets.
        return new Container(height: t + 2, decoration: new BoxDecoration(Border: Border.All(c.Hairline), BorderRadius: BorderRadius.Circular(t / 2), Color: c.Surface),
            child: new Row(mainAxisSize: MainAxisSize.Min, children:
            [
                Step(Icons.Remove, value > min, value - 1),
                new SizedBox(width: 28, child: new Center(new Text(value.ToString(), style: theme.Type.BodyStrong.Merge(new TextStyle(Color: c.Ink))))),
                Step(Icons.Add, value < max, value + 1),
            ]));
    }
}

/// <summary>
/// A product tile: photo (a slot for your image widget; a hairline-grey block when none is given), optional tag, wishlist heart, title,
/// rating and price, with an optional pill "add to cart" button underneath. Tapping the tile calls <paramref name="onPressed"/>.
/// It takes the width its parent gives it, so it sits in a <see cref="ShopifyProductGrid"/> at any breakpoint.
/// </summary>
public sealed class ShopifyProductCard(string title, decimal price, Widget? image = null, decimal? compareAtPrice = null, string currency = "$",
    string? badge = null, double? rating = null, int reviewCount = 0, Action? onPressed = null, Action? onAddToCart = null,
    string addLabel = "Add to cart", bool? wishlisted = null, Action<bool>? onWishlistChanged = null, bool soldOut = false,
    float imageAspectRatio = 0.8f, Key? key = null) : StatelessWidget(key)
{
    public override Widget Build(BuildContext context)
    {
        var theme = ShopifyTheme.Of(context);
        var c = theme.Colors;
        var radius = BorderRadius.Circular(12);

        Widget heart(bool on) => new Interactive((ctx, st) => ShopifyFocus.Around(st.FocusVisible, theme,
            new Container(width: ShopifyBreakpoints.TouchTarget, height: ShopifyBreakpoints.TouchTarget, alignment: Alignment.Center,
                child: new Container(width: 36, height: 36, alignment: Alignment.Center,
                    decoration: new BoxDecoration(Color: c.Surface.WithOpacity(st.Hover ? 1f : 0.9f), BorderRadius: BorderRadius.Circular(18)),
                    child: new Icon(on ? Icons.Favorite : Icons.FavoriteBorder, 20, c.Ink))), BorderRadius.Circular(22)),
            () => onWishlistChanged?.Invoke(!on));

        string? tagText = soldOut ? "Sold out" : badge;
        Widget photo = new AspectRatio(imageAspectRatio, new ClipRRect(radius, new Stack([
            Positioned.Fill(image ?? new ColoredBox(c.Hairline)),
            ..soldOut ? [Positioned.Fill(new ColoredBox(c.Canvas.WithOpacity(0.55f)))] : Array.Empty<Widget>(),
            ..tagText is null ? Array.Empty<Widget>() : [new Positioned(new ShopifyTag(tagText, soldOut ? ShopifyTagKind.Shade : ShopifyTagKind.Mint), left: 8, top: 8)],
            ..wishlisted is { } w ? [new Positioned(heart(w), right: 0, top: 0)] : Array.Empty<Widget>(),
        ], fit: StackFit.Expand)));

        Widget tile = new Interactive((ctx, st) => ShopifyFocus.Around(st.FocusVisible, theme,
            new Column(crossAxisAlignment: CrossAxisAlignment.Stretch, mainAxisSize: MainAxisSize.Min, spacing: 10, children:
            [
                photo,
                new Column(crossAxisAlignment: CrossAxisAlignment.Start, mainAxisSize: MainAxisSize.Min, spacing: 4, children:
                [
                    new Text(title, maxLines: 2, overflow: TextOverflow.Ellipsis, style: theme.Type.BodyMd.Merge(new TextStyle(
                        FontWeight: 550, Color: soldOut ? c.InkTertiary : c.Ink, Decoration: st.Hover ? TextDecoration.Underline : TextDecoration.None))),
                    ..rating is { } r ? [new ShopifyRating(r, reviewCount > 0 ? reviewCount : null)] : Array.Empty<Widget>(),
                    new ShopifyPrice(price, compareAtPrice, currency),
                ]),
            ]), radius), onPressed);

        if (onAddToCart is null) return tile;
        return new Column(crossAxisAlignment: CrossAxisAlignment.Stretch, mainAxisSize: MainAxisSize.Min, spacing: 12, children:
        [
            tile,
            new ShopifyButton(soldOut ? "Sold out" : addLabel, soldOut ? null : onAddToCart, ShopifyButtonKind.Outline, fill: true),
        ]);
    }
}

/// <summary>
/// A non-scrolling grid of equal-width cells that reflows with the width it is given: 2 columns on phones, 3 on tablets, 4 on desktops
/// (or <paramref name="columns"/>). Put it inside a scroll view; its height is the height of its rows.
/// </summary>
public sealed class ShopifyProductGrid(IReadOnlyList<Widget> children, int? columns = null, float? spacing = null, Key? key = null) : StatelessWidget(key)
{
    public override Widget Build(BuildContext context) => new ShopifyResponsive((ctx, bp, width) =>
    {
        int cols = Math.Max(1, columns ?? ShopifyBreakpoints.GridColumns(bp));
        float gap = spacing ?? (bp == ShopifyBreakpoint.Mobile ? 12 : 24);
        // Floor so rounding never pushes the last cell of a row onto the next line.
        float cell = MathF.Floor((width - gap * (cols - 1)) / cols);
        return new Wrap(children.Select(ch => (Widget)new SizedBox(width: cell, child: ch)).ToList(), spacing: gap, runSpacing: gap * 1.5f);
    });
}

public sealed record ShopifyOption<T>(T Value, string Label, bool Available = true);

/// <summary>
/// A single-choice group of pills for sizes and other variants. The selected pill is solid; sold-out ones are struck through and can't be
/// chosen. Every pill is at least 44px tall and wide, and the group wraps instead of scrolling.
/// </summary>
public sealed class ShopifyOptionChips<T>(IReadOnlyList<ShopifyOption<T>> options, T? value, Action<T>? onChanged, string? label = null, Key? key = null)
    : StatelessWidget(key) where T : notnull
{
    public override Widget Build(BuildContext context)
    {
        var theme = ShopifyTheme.Of(context);
        var c = theme.Colors;
        var radius = BorderRadius.Circular(22);
        var selected = options.FirstOrDefault(o => EqualityComparer<T>.Default.Equals(o.Value, value));

        Widget Chip(ShopifyOption<T> o)
        {
            bool sel = EqualityComparer<T>.Default.Equals(o.Value, value);
            return new Interactive((ctx, st) => ShopifyFocus.Around(st.FocusVisible, theme, new Container(
                constraints: new BoxConstraints(ShopifyBreakpoints.TouchTarget + 12, float.PositiveInfinity, ShopifyBreakpoints.TouchTarget, ShopifyBreakpoints.TouchTarget),
                padding: EdgeInsets.Symmetric(horizontal: 16),
                decoration: new BoxDecoration(Color: sel ? c.Primary : Colors.Transparent, BorderRadius: radius,
                    Border: Border.All(sel ? c.Primary : st.Hover && o.Available ? c.Ink : c.Hairline, 1)),
                child: new Center(new Text(o.Label, softWrap: false, maxLines: 1, style: theme.Type.BodyMd.Merge(new TextStyle(
                    Color: sel ? c.OnPrimary : o.Available ? c.Ink : c.OnDisabled, FontWeight: sel ? 550 : 420,
                    Decoration: o.Available ? TextDecoration.None : TextDecoration.LineThrough))), 1, 1)), radius),
                o.Available && onChanged is not null ? () => onChanged(o.Value) : null);
        }

        return new Column(crossAxisAlignment: CrossAxisAlignment.Start, mainAxisSize: MainAxisSize.Min, spacing: 10, children:
        [
            ..label is null ? Array.Empty<Widget>() : [new Text(selected is null ? label : $"{label}: {selected.Label}", style: theme.Type.Caption.Merge(new TextStyle(Color: c.Ink)))],
            new Wrap(options.Select(Chip).ToList(), spacing: 8, runSpacing: 8),
        ]);
    }
}

public sealed record ShopifySwatch<T>(T Value, SKColor Color, string Name, bool Available = true);

/// <summary>A single-choice row of colour swatches. Each is a 32px dot inside a 44px target; the chosen one gets a ring.</summary>
public sealed class ShopifySwatchPicker<T>(IReadOnlyList<ShopifySwatch<T>> swatches, T? value, Action<T>? onChanged, string? label = null, Key? key = null)
    : StatelessWidget(key) where T : notnull
{
    public override Widget Build(BuildContext context)
    {
        var theme = ShopifyTheme.Of(context);
        var c = theme.Colors;
        var selected = swatches.FirstOrDefault(s => EqualityComparer<T>.Default.Equals(s.Value, value));

        Widget Dot(ShopifySwatch<T> s)
        {
            bool sel = EqualityComparer<T>.Default.Equals(s.Value, value);
            return new Interactive((ctx, st) => ShopifyFocus.Around(st.FocusVisible, theme, new Container(
                width: ShopifyBreakpoints.TouchTarget, height: ShopifyBreakpoints.TouchTarget, alignment: Alignment.Center,
                decoration: new BoxDecoration(BorderRadius: BorderRadius.Circular(22), Border: Border.All(sel ? c.Ink : Colors.Transparent, 2)),
                child: new Opacity(s.Available ? 1f : 0.35f, new Container(width: 32, height: 32,
                    decoration: new BoxDecoration(Color: s.Color, BorderRadius: BorderRadius.Circular(16), Border: Border.All(c.Hairline))))),
                BorderRadius.Circular(22)), s.Available && onChanged is not null ? () => onChanged(s.Value) : null);
        }

        return new Column(crossAxisAlignment: CrossAxisAlignment.Start, mainAxisSize: MainAxisSize.Min, spacing: 4, children:
        [
            ..label is null ? Array.Empty<Widget>() : [new Text(selected is null ? label : $"{label}: {selected.Name}", style: theme.Type.Caption.Merge(new TextStyle(Color: c.Ink)))],
            new Wrap(swatches.Select(Dot).ToList(), spacing: 4, runSpacing: 4),
        ]);
    }
}
