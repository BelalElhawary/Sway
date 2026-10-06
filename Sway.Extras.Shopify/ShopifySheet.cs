using Sway.Widgets;

namespace Sway.Extras.Shopify;

/// <summary>
/// A modal panel for filters, variant pickers, the cart drawer's quick view and similar. On phones it slides up from the bottom edge as a sheet
/// (rounded top corners, at most 90% of the screen high, and it can be dragged down by its handle or title to dismiss); from tablet width it is a centred 520px dialog instead.
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

sealed class ShopifySheetLayer(Func<BuildContext, Action, Widget> builder, string? title, bool dismissible, float bottomInset, Action close) : StatefulWidget
{
    internal Func<BuildContext, Action, Widget> Builder => builder;
    internal string? Title => title;
    internal bool Dismissible => dismissible;
    internal float BottomInset => bottomInset;
    internal Action Close => close;
    public override State CreateState() => new ShopifySheetLayerState();
}

sealed class ShopifySheetLayerState : State<ShopifySheetLayer>
{
    // How far the phone sheet has been dragged down, and the distance or speed past which letting go dismisses it.
    float _drag;
    const float DismissDistance = 100, DismissVelocity = 700;

    void DragUpdate(DragDetails d) => SetState(() => _drag = Math.Max(0, _drag + d.Delta.Dy));

    void DragEnd(DragDetails d)
    {
        if (_drag > DismissDistance || d.Velocity.Dy > DismissVelocity) Widget.Close();
        else SetState(() => _drag = 0);
    }

    public override Widget Build(BuildContext context)
    {
        var theme = ShopifyTheme.Of(context);
        var c = theme.Colors;
        var close = Widget.Close;
        bool dismissible = Widget.Dismissible;

        return new Focus(autofocus: true, trapFocus: true, skipTraversal: true, onKey: e => { if (dismissible && e.IsDown && e.Key == "Escape") { close(); return true; } return false; },
            child: new LayoutBuilder((ctx, box) =>
            {
                float width = box.HasBoundedWidth ? box.MaxWidth : WidgetsBinding.Instance.RenderView.WindowSize.Width;
                float height = box.HasBoundedHeight ? box.MaxHeight : WidgetsBinding.Instance.RenderView.WindowSize.Height;
                bool phone = ShopifyBreakpoints.For(width) == ShopifyBreakpoint.Mobile;
                var radius = phone ? BorderRadius.Only(topLeft: 20, topRight: 20) : BorderRadius.Circular(20);

                Widget top = new Column(crossAxisAlignment: CrossAxisAlignment.Stretch, mainAxisSize: MainAxisSize.Min, children:
                [
                    ..phone ? [new Padding(EdgeInsets.Only(top: 10), new Center(new SizedBox(36, 4, new DecoratedBox(
                        new BoxDecoration(Color: c.Shade, BorderRadius: BorderRadius.Circular(2))))))] : Array.Empty<Widget>(),
                    new Padding(EdgeInsets.Only(left: 24, right: 8, top: phone ? 4 : 12), new Row(crossAxisAlignment: CrossAxisAlignment.Center, children:
                    [
                        new Expanded(Widget.Title is null ? new SizedBox() : new Text(Widget.Title, maxLines: 1, overflow: TextOverflow.Ellipsis,
                            style: theme.Type.HeadingMd.Merge(new TextStyle(Color: c.Ink)))),
                        new ShopifyIconButton(Icons.Close, close),
                    ])),
                ]);
                // Only the handle and title row drag the sheet, so scrolling its content never fights the dismiss gesture.
                if (phone && dismissible)
                    top = new GestureDetector(top, behavior: HitTestBehavior.Opaque, onVerticalDragUpdate: DragUpdate, onVerticalDragEnd: DragEnd);

                Widget panel = new GestureDetector(onTap: () => { }, behavior: HitTestBehavior.Opaque, child: new ConstrainedBox(
                    new BoxConstraints(0, phone ? float.PositiveInfinity : 520, 0, height * (phone ? 0.9f : 0.85f)),
                    new DecoratedBox(new BoxDecoration(Color: c.Surface, BorderRadius: radius,
                        BoxShadow: [new BoxShadow(Colors.Black.WithOpacity(0.25f), new Offset(0, 25), 50, -12)]),
                        new ClipRRect(radius, new Column(mainAxisSize: MainAxisSize.Min, crossAxisAlignment: CrossAxisAlignment.Stretch, children:
                        [
                            top,
                            new Flexible(new SingleChildScrollView(new Padding(EdgeInsets.Only(left: 24, right: 24, top: 8, bottom: 24 + Widget.BottomInset),
                                Widget.Builder(ctx, close)), shrinkWrap: true)),
                        ])))));

                return new Stack([
                    Positioned.Fill(new GestureDetector(onTap: dismissible ? close : null, behavior: HitTestBehavior.Opaque,
                        child: new ColoredBox(c.Scrim.WithOpacity(c.Scrim.Alpha / 255f * (1 - Math.Clamp(_drag / (height * 0.6f), 0, 0.6f)))))),
                    phone ? new Positioned(panel, left: 0, right: 0, bottom: -_drag) : Positioned.Fill(new Center(panel)),
                ], fit: StackFit.Expand, clip: false);
            }));
    }
}
