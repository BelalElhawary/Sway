using Sway.Widgets;

namespace Sway.Extras.Shopify;

/// <summary>
/// A modal panel for filters, variant pickers, the cart drawer's quick view and similar. On phones it slides up from the bottom edge as a sheet
/// (rounded top corners, drag-handle affordance, at most 90% of the screen high); from tablet width it is a centred 520px dialog instead.
/// Its content scrolls when it is taller than the space allows. Tapping the scrim, the close button or pressing Escape dismisses it.
/// </summary>
public static class ShopifySheet
{
    /// <summary>
    /// Shows the sheet above the app. The builder gets a function that closes it. Returns that same function.
    /// <paramref name="bottomInset"/> is the device's bottom safe-area height, so the last row clears the home indicator.
    /// </summary>
    public static Action Show(BuildContext context, Func<BuildContext, Action, Widget> builder, string? title = null, bool dismissible = true,
        float bottomInset = 0)
    {
        var overlay = Overlay.Of(context);
        var previousFocus = WidgetsBinding.Instance.Focus.Primary;
        OverlayEntry? entry = null;
        void Close()
        {
            if (entry is null) return;
            entry.Remove();
            entry = null;
            if (previousFocus is { Element.Mounted: true }) previousFocus.RequestFocus();
        }
        entry = new OverlayEntry(ctx => ShopifyTheme.Wrap(context, new ShopifySheetLayer(builder, title, dismissible, bottomInset, Close)));
        overlay.Insert(entry);
        return Close;
    }
}

sealed class ShopifySheetLayer(Func<BuildContext, Action, Widget> builder, string? title, bool dismissible, float bottomInset, Action close) : StatelessWidget
{
    public override Widget Build(BuildContext context)
    {
        var theme = ShopifyTheme.Of(context);
        var c = theme.Colors;

        return new Focus(autofocus: true, trapFocus: true, skipTraversal: true, onKey: e => { if (dismissible && e.IsDown && e.Key == "Escape") { close(); return true; } return false; },
            child: new LayoutBuilder((ctx, box) =>
            {
                float width = box.HasBoundedWidth ? box.MaxWidth : WidgetsBinding.Instance.RenderView.WindowSize.Width;
                float height = box.HasBoundedHeight ? box.MaxHeight : WidgetsBinding.Instance.RenderView.WindowSize.Height;
                bool phone = ShopifyBreakpoints.For(width) == ShopifyBreakpoint.Mobile;
                var radius = phone ? BorderRadius.Only(topLeft: 20, topRight: 20) : BorderRadius.Circular(20);

                Widget panel = new GestureDetector(onTap: () => { }, behavior: HitTestBehavior.Opaque, child: new ConstrainedBox(
                    new BoxConstraints(0, phone ? float.PositiveInfinity : 520, 0, height * (phone ? 0.9f : 0.85f)),
                    new DecoratedBox(new BoxDecoration(Color: c.Surface, BorderRadius: radius,
                        BoxShadow: [new BoxShadow(Colors.Black.WithOpacity(0.25f), new Offset(0, 25), 50, -12)]),
                        new ClipRRect(radius, new Column(mainAxisSize: MainAxisSize.Min, crossAxisAlignment: CrossAxisAlignment.Stretch, children:
                        [
                            ..phone ? [new Padding(EdgeInsets.Only(top: 10), new Center(new SizedBox(36, 4, new DecoratedBox(
                                new BoxDecoration(Color: c.Shade, BorderRadius: BorderRadius.Circular(2))))))] : Array.Empty<Widget>(),
                            new Padding(EdgeInsets.Only(left: 24, right: 8, top: phone ? 4 : 12), new Row(crossAxisAlignment: CrossAxisAlignment.Center, children:
                            [
                                new Expanded(title is null ? new SizedBox() : new Text(title, maxLines: 1, overflow: TextOverflow.Ellipsis,
                                    style: theme.Type.HeadingMd.Merge(new TextStyle(Color: c.Ink)))),
                                new ShopifyIconButton(Icons.Close, close),
                            ])),
                            new Flexible(new SingleChildScrollView(new Padding(EdgeInsets.Only(left: 24, right: 24, top: 8, bottom: 24 + bottomInset),
                                builder(ctx, close)), shrinkWrap: true)),
                        ])))));

                return new Stack([
                    Positioned.Fill(new GestureDetector(onTap: dismissible ? close : null, behavior: HitTestBehavior.Opaque, child: new ColoredBox(c.Scrim))),
                    phone ? new Positioned(panel, left: 0, right: 0, bottom: 0) : Positioned.Fill(new Center(panel)),
                ], fit: StackFit.Expand, clip: false);
            }));
    }
}
