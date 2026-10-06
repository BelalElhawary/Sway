using SkiaSharp;
using Sway.Widgets;

namespace Sway.Extras.Shopify;

/// <summary>
/// One line of a cart: photo, title, variant, quantity stepper, line total and a remove button. The title and variant take whatever width
/// is left, so it holds together at 320px; the price drops under the title instead of squeezing it.
/// </summary>
public sealed class ShopifyCartLine(string title, decimal unitPrice, int quantity, Action<int>? onQuantityChanged = null, Action? onRemove = null,
    string? variant = null, Widget? image = null, string currency = "$", int maxQuantity = 99, Key? key = null) : StatelessWidget(key)
{
    public override Widget Build(BuildContext context)
    {
        var theme = ShopifyTheme.Of(context);
        var c = theme.Colors;
        const float photo = 88;

        Widget thumb = new SizedBox(photo, photo, new ClipRRect(BorderRadius.Circular(8), image ?? new ColoredBox(c.Hairline)));
        Widget remove = new Interactive((ctx, st) => ShopifyFocus.Around(st.FocusVisible, theme, new Container(
            width: ShopifyBreakpoints.TouchTarget, height: ShopifyBreakpoints.TouchTarget, alignment: Alignment.Center,
            child: new Icon(Icons.Delete, 20, st.Hover ? c.Ink : c.InkSecondary)), BorderRadius.Circular(22)), onRemove);

        return new Container(padding: EdgeInsets.Symmetric(vertical: 16), decoration: new BoxDecoration(Border: Border.Only(bottom: new BorderSide(c.Hairline, 1))),
            child: new Row(crossAxisAlignment: CrossAxisAlignment.Start, spacing: 16, children:
            [
                thumb,
                new Expanded(new Column(crossAxisAlignment: CrossAxisAlignment.Stretch, mainAxisSize: MainAxisSize.Min, spacing: 4, children:
                [
                    new Text(title, maxLines: 2, overflow: TextOverflow.Ellipsis, style: theme.Type.BodyMd.Merge(new TextStyle(FontWeight: 550, Color: c.Ink))),
                    ..variant is null ? Array.Empty<Widget>() : [new Text(variant, maxLines: 1, overflow: TextOverflow.Ellipsis, style: theme.Type.Caption.Merge(new TextStyle(Color: c.InkSecondary)))],
                    new Text(ShopifyMoney.Format(unitPrice * quantity, currency), style: theme.Type.BodyStrong.Merge(new TextStyle(Color: c.Ink))),
                    new SizedBox(height: 4),
                    new Row(spacing: 4, children:
                    [
                        new ShopifyQuantityStepper(quantity, onQuantityChanged, 1, maxQuantity),
                        new Expanded(new Align(Alignment.CenterRight, remove)),
                    ]),
                ])),
            ]));
    }
}

public sealed record ShopifySummaryLine(string Label, string Value, bool Emphasis = false);

/// <summary>
/// The order summary card: label/value rows, a rule, and the total, with a full-width checkout pill. White card, 12px radius, hairline
/// border. On the light track the featured variant fills the card with aloe.
/// </summary>
public sealed class ShopifyOrderSummary(IReadOnlyList<ShopifySummaryLine> lines, string total, Action? onCheckout = null,
    string title = "Order summary", string checkoutLabel = "Checkout", bool featured = false, Key? key = null) : StatelessWidget(key)
{
    public override Widget Build(BuildContext context)
    {
        var theme = ShopifyTheme.Of(context);
        var c = theme.Colors;
        // On aloe the secondary greys lose contrast, so everything uses the on-aloe colour.
        var ink = featured ? c.OnAloe : c.Ink;
        var muted = featured ? c.OnAloe : c.InkSecondary;

        Widget Row_(string label, string value, TextStyle style, SKColor color) => new Row(crossAxisAlignment: CrossAxisAlignment.Center, spacing: 12, children:
        [
            new Expanded(new Text(label, style: style.Merge(new TextStyle(Color: color)))),
            new Text(value, style: style.Merge(new TextStyle(Color: ink))),
        ]);

        return new Container(padding: EdgeInsets.All(24), decoration: new BoxDecoration(Color: featured ? c.Aloe : c.Surface, BorderRadius: BorderRadius.Circular(12),
            Border: featured ? null : Border.All(c.Hairline)),
            child: new Column(crossAxisAlignment: CrossAxisAlignment.Stretch, mainAxisSize: MainAxisSize.Min, spacing: 12, children:
            [
                new Text(title, style: theme.Type.HeadingMd.Merge(new TextStyle(Color: ink))),
                ..lines.Select(l => Row_(l.Label, l.Value, l.Emphasis ? theme.Type.BodyStrong : theme.Type.BodyMd, muted)),
                new Container(height: 1, color: featured ? c.OnAloe.WithOpacity(0.15f) : c.Hairline),
                Row_("Total", total, theme.Type.HeadingMd, ink),
                new SizedBox(height: 4),
                new ShopifyButton(checkoutLabel, onCheckout, ShopifyButtonKind.Primary, ShopifyButtonSize.Large, Icons.Lock, fill: true),
            ]));
    }
}

/// <summary>"You're $12.00 away from free shipping" with a progress bar that turns into a confirmation once the threshold is met.</summary>
public sealed class ShopifyFreeShippingBar(decimal subtotal, decimal threshold, string currency = "$", Key? key = null) : StatelessWidget(key)
{
    public override Widget Build(BuildContext context)
    {
        var theme = ShopifyTheme.Of(context);
        var c = theme.Colors;
        bool done = subtotal >= threshold;
        float progress = threshold <= 0 ? 1 : (float)Math.Clamp(subtotal / threshold, 0, 1);
        string text = done ? "You've unlocked free shipping" : $"You're {ShopifyMoney.Format(threshold - subtotal, currency)} away from free shipping";
        return new Container(padding: EdgeInsets.All(16), decoration: new BoxDecoration(Color: c.Pistachio, BorderRadius: BorderRadius.Circular(12)),
            child: new Column(crossAxisAlignment: CrossAxisAlignment.Stretch, mainAxisSize: MainAxisSize.Min, spacing: 10, children:
            [
                new Row(spacing: 8, children:
                [
                    new Icon(done ? Icons.CheckCircle : Icons.LocalShipping, 20, c.OnPistachio),
                    new Expanded(new Text(text, style: theme.Type.Caption.Merge(new TextStyle(Color: c.OnPistachio)))),
                ]),
                new SizedBox(height: 6, child: new ClipRRect(BorderRadius.Circular(3), new Stack([
                    Positioned.Fill(new ColoredBox(c.OnPistachio.WithOpacity(0.18f))),
                    Positioned.Fill(new FractionallySizedBox(widthFactor: progress, heightFactor: 1, alignment: Alignment.CenterLeft, child: new ColoredBox(c.OnPistachio))),
                ], fit: StackFit.Expand))),
            ]));
    }
}

/// <summary>An empty cart, wishlist or search result: an icon, a headline, a sentence, and an optional pill button.</summary>
public sealed class ShopifyEmptyState(IconData icon, string title, string? message = null, string? actionLabel = null, Action? onAction = null, Key? key = null)
    : StatelessWidget(key)
{
    public override Widget Build(BuildContext context)
    {
        var theme = ShopifyTheme.Of(context);
        var c = theme.Colors;
        return new Center(new Padding(EdgeInsets.All(32), new ConstrainedBox(new BoxConstraints(0, 360, 0, float.PositiveInfinity),
            new Column(mainAxisSize: MainAxisSize.Min, spacing: 12, children:
            [
                new Container(width: 72, height: 72, alignment: Alignment.Center,
                    decoration: new BoxDecoration(Color: c.Pistachio, BorderRadius: BorderRadius.Circular(36)), child: new Icon(icon, 32, c.OnPistachio)),
                new Text(title, textAlign: TextAlign.Center, style: theme.Type.HeadingLg.Merge(new TextStyle(Color: c.Ink))),
                ..message is null ? Array.Empty<Widget>() : [new Text(message, textAlign: TextAlign.Center, style: theme.Type.BodyMd.Merge(new TextStyle(Color: c.InkSecondary)))],
                ..actionLabel is null ? Array.Empty<Widget>() : [new SizedBox(height: 4), new ShopifyButton(actionLabel, onAction, size: ShopifyButtonSize.Large)],
            ]))));
    }
}

/// <summary>
/// The checkout progress indicator. On desktops it is a row of numbered steps joined by a rule; on phones it collapses to
/// "Step 2 of 3 · Shipping" over a segmented bar, so the labels never have to fit side by side.
/// </summary>
public sealed class ShopifyCheckoutSteps(IReadOnlyList<string> steps, int current, Key? key = null) : StatelessWidget(key)
{
    public override Widget Build(BuildContext context)
    {
        var theme = ShopifyTheme.Of(context);
        var c = theme.Colors;
        int at = Math.Clamp(current, 0, Math.Max(0, steps.Count - 1));

        return new ShopifyResponsive((ctx, bp, width) =>
        {
            if (bp == ShopifyBreakpoint.Mobile)
                return new Column(crossAxisAlignment: CrossAxisAlignment.Stretch, mainAxisSize: MainAxisSize.Min, spacing: 8, children:
                [
                    new Text($"Step {at + 1} of {steps.Count} · {steps[at]}", style: theme.Type.Caption.Merge(new TextStyle(Color: c.Ink))),
                    new Row(spacing: 4, children: steps.Select((_, i) => (Widget)new Expanded(new Container(height: 4,
                        decoration: new BoxDecoration(Color: i <= at ? c.Primary : c.Hairline, BorderRadius: BorderRadius.Circular(2))))).ToList()),
                ]);

            var parts = new List<Widget>();
            for (int i = 0; i < steps.Count; i++)
            {
                bool done = i < at, active = i == at;
                if (i > 0) parts.Add(new Expanded(new Container(height: 1, color: i <= at ? c.Primary : c.Hairline)));
                parts.Add(new Row(mainAxisSize: MainAxisSize.Min, spacing: 8, children:
                [
                    new Container(width: 28, height: 28, alignment: Alignment.Center,
                        decoration: new BoxDecoration(Color: done || active ? c.Primary : Colors.Transparent, BorderRadius: BorderRadius.Circular(14),
                            Border: done || active ? null : Border.All(c.Hairline)),
                        child: done ? new Icon(Icons.Check, 16, c.OnPrimary)
                            : new Text((i + 1).ToString(), style: theme.Type.Micro.Merge(new TextStyle(Color: active ? c.OnPrimary : c.InkSecondary)))),
                    new Text(steps[i], style: theme.Type.Caption.Merge(new TextStyle(Color: active || done ? c.Ink : c.InkSecondary))),
                ]));
            }
            return new Row(spacing: 12, children: parts);
        });
    }
}
