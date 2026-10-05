using SkiaSharp;
using Sway.Widgets;

namespace Sway.Extras.Ibm;

/// <summary>Opens a click-away layer with a panel placed against an anchor widget. Shared by the popover, menu, combo box and date picker.</summary>
static class CarbonPopup
{
    /// <param name="alignEnd">Line the panel's right edge up with the anchor's instead of its left edge.</param>
    /// <param name="width">The panel's width, or null to match the anchor.</param>
    /// <param name="height">The panel's height, used only to decide whether it fits below the anchor.</param>
    /// <param name="takeFocus">Whether the layer grabs keyboard focus and closes on Escape. Combo boxes keep focus in their field instead.</param>
    public static OverlayEntry? Open(BuildContext owner, RenderBox anchor, Func<Widget> panel, float height, Action dismiss,
        float? width = null, bool alignEnd = false, bool takeFocus = true)
    {
        var overlay = Overlay.MaybeOf(owner);
        if (overlay is null) return null;
        var origin = anchor.LocalToGlobal(Offset.Zero);
        var window = WidgetsBinding.Instance.RenderView.WindowSize;
        float w = width ?? anchor.Size.Width;
        bool above = origin.Dy + anchor.Size.Height + height > window.Height && origin.Dy - height > 0;
        float top = above ? origin.Dy - height : origin.Dy + anchor.Size.Height;
        float left = alignEnd ? origin.Dx + anchor.Size.Width - w : origin.Dx;
        left = Math.Clamp(left, 0, Math.Max(0, window.Width - w));

        var entry = new OverlayEntry(ctx =>
        {
            Widget layer = new Stack([
                Positioned.Fill(new GestureDetector(onTap: dismiss, behavior: HitTestBehavior.Opaque)),
                new Positioned(panel(), left: left, top: top, width: width is null ? w : null),
            ], fit: StackFit.Expand, clip: false);
            if (takeFocus) layer = new Focus(autofocus: true, onKey: e => { if (e.IsDown && e.Key == "Escape") { dismiss(); return true; } return false; }, child: layer);
            return CarbonTheme.Wrap(owner, layer);
        });
        overlay.Insert(entry);
        return entry;
    }

    public static Widget Shadowed(CarbonThemeData theme, Widget child) => new Container(
        decoration: new BoxDecoration(Color: theme.Colors.Layer01, Border: Border.All(theme.Colors.BorderSubtle01),
            BoxShadow: [new BoxShadow(Colors.Black.WithOpacity(0.3f), new Offset(0, 2), 6)]), child: child);
}

public enum CarbonTooltipSide { Bottom, Top }

/// <summary>A Carbon tooltip: a short dark label that appears next to <paramref name="child"/> while the pointer is over it.</summary>
public sealed class CarbonTooltip(string label, Widget child, CarbonTooltipSide side = CarbonTooltipSide.Bottom, Key? key = null) : StatefulWidget(key)
{
    internal string Label => label;
    internal Widget Child => child;
    internal CarbonTooltipSide Side => side;
    public override State CreateState() => new CarbonTooltipState();
}

sealed class CarbonTooltipState : State<CarbonTooltip>
{
    OverlayEntry? _entry;

    public override void Dispose() => Hide();

    void Hide()
    {
        _entry?.Remove();
        _entry = null;
    }

    void Show()
    {
        if (_entry is not null || Context.FindRenderObject() is not RenderBox box || Overlay.MaybeOf(Context) is not { } overlay) return;
        var origin = box.LocalToGlobal(Offset.Zero);
        float cx = origin.Dx + box.Size.Width / 2;
        bool top = Widget.Side == CarbonTooltipSide.Top;
        var owner = Context;
        var label = Widget.Label;
        _entry = new OverlayEntry(ctx =>
        {
            var theme = CarbonTheme.Of(owner);
            var c = theme.Colors;
            Widget tip = new ConstrainedBox(new BoxConstraints(0, 208, 0, float.PositiveInfinity), new Container(
                padding: EdgeInsets.Symmetric(horizontal: 16, vertical: 8), color: c.BackgroundInverse,
                child: new Text(label, style: theme.Type.Label01.Merge(new TextStyle(Color: c.TextInverse)))));
            // Centre on the anchor: pull the tip back by half its own width, and up by its whole height when it sits above.
            tip = new FractionalTranslation(new Offset(-0.5f, top ? -1f : 0f), tip);
            return CarbonTheme.Wrap(owner, new IgnorePointer(new Stack([new Positioned(tip, left: cx, top: top ? origin.Dy - 4 : origin.Dy + box.Size.Height + 4)],
                fit: StackFit.Expand, clip: false)));
        });
        overlay.Insert(_entry);
    }

    public override Widget Build(BuildContext context) =>
        new MouseRegion(onEnter: _ => Show(), onExit: _ => Hide(), opaque: false, child: Widget.Child);
}

/// <summary>
/// A Carbon popover: <paramref name="trigger"/> that toggles a panel of <paramref name="content"/> beside it. Click away or press Escape to close.
/// <paramref name="highContrast"/> uses the inverse (dark on light) colours.
/// </summary>
public sealed class CarbonPopover(Widget trigger, Widget content, float width = 288, bool highContrast = false, bool alignEnd = false, Key? key = null) : StatefulWidget(key)
{
    internal Widget Trigger => trigger;
    internal Widget Content => content;
    internal float PanelWidth => width;
    internal bool HighContrast => highContrast;
    internal bool AlignEnd => alignEnd;
    public override State CreateState() => new CarbonPopoverState();
}

sealed class CarbonPopoverState : State<CarbonPopover>
{
    OverlayEntry? _entry;

    public override void Dispose() => Close();

    void Close()
    {
        _entry?.Remove();
        _entry = null;
        if (Mounted) SetState();
    }

    void Toggle()
    {
        if (_entry is not null) { Close(); return; }
        if (Context.FindRenderObject() is not RenderBox box) return;
        var owner = Context;
        _entry = CarbonPopup.Open(owner, box, () =>
        {
            var theme = CarbonTheme.Of(owner);
            var c = theme.Colors;
            var fore = Widget.HighContrast ? c.TextInverse : c.TextPrimary;
            return new Container(padding: EdgeInsets.All(16), decoration: new BoxDecoration(Color: Widget.HighContrast ? c.BackgroundInverse : c.Layer01,
                BoxShadow: [new BoxShadow(Colors.Black.WithOpacity(0.3f), new Offset(0, 2), 6)]),
                child: DefaultTextStyle.Merge(owner, theme.Type.Body01.Merge(new TextStyle(Color: fore)), Widget.Content));
        }, 120, Close, Widget.PanelWidth, Widget.AlignEnd);
        SetState();
    }

    public override Widget Build(BuildContext context) =>
        new GestureDetector(onTap: Toggle, behavior: HitTestBehavior.Opaque, child: Widget.Trigger);
}

/// <summary>A Carbon toggletip: an info button that opens a small explanatory <see cref="CarbonPopover"/>.</summary>
public sealed class CarbonToggletip(Widget content, Widget? trigger = null, Key? key = null) : StatelessWidget(key)
{
    public override Widget Build(BuildContext context)
    {
        var c = CarbonTheme.Of(context).Colors;
        return new CarbonPopover(trigger ?? new Interactive((ctx, st) => CarbonFocus.Around(st.FocusVisible, CarbonTheme.Of(ctx),
            new Icon(Icons.Info, 16, c.IconSecondary)), null, focusable: false), content);
    }
}

public sealed record CarbonMenuItem(string Label, Action? OnPressed = null, bool Danger = false, bool Disabled = false, bool DividerBefore = false, IconData? Icon = null);

sealed class CarbonMenuPanel(IReadOnlyList<CarbonMenuItem> items, Action onClose, float itemHeight) : StatefulWidget
{
    internal IReadOnlyList<CarbonMenuItem> Items => items;
    internal Action OnClose => onClose;
    internal float ItemHeight => itemHeight;
    public override State CreateState() => new CarbonMenuPanelState();
}

sealed class CarbonMenuPanelState : State<CarbonMenuPanel>
{
    int _highlight = -1;

    int Next(int from, int step)
    {
        for (int i = from + step; i >= 0 && i < Widget.Items.Count; i += step)
            if (!Widget.Items[i].Disabled) return i;
        return from;
    }

    void Choose(CarbonMenuItem item)
    {
        Widget.OnClose();
        item.OnPressed?.Invoke();
    }

    bool OnKey(KeyEvent e)
    {
        if (!e.IsDown) return true;
        switch (e.Key)
        {
            case "ArrowDown": SetState(() => _highlight = Next(_highlight, 1)); break;
            case "ArrowUp": SetState(() => _highlight = Next(_highlight < 0 ? Widget.Items.Count : _highlight, -1)); break;
            case "Enter" or " ": if (_highlight >= 0) Choose(Widget.Items[_highlight]); break;
            case "Escape" or "Tab": Widget.OnClose(); break;
        }
        return true;
    }

    public override Widget Build(BuildContext context)
    {
        var theme = CarbonTheme.Of(context);
        var c = theme.Colors;
        var rows = new List<Widget>();
        for (int n = 0; n < Widget.Items.Count; n++)
        {
            int i = n;
            var item = Widget.Items[n];
            if (item.DividerBefore) rows.Add(new Container(height: 1, color: c.BorderSubtle01));
            bool lit = _highlight == i;
            rows.Add(new SizedBox(height: Widget.ItemHeight, child: new Interactive((ctx, st) =>
            {
                bool on = (lit || st.Hover) && !item.Disabled;
                var back = on ? (item.Danger ? c.ButtonDangerPrimary : c.LayerHover01) : Colors.Transparent;
                var fore = item.Disabled ? c.TextDisabled : on && item.Danger ? c.TextOnColor : c.TextSecondary;
                return new Container(color: back, padding: EdgeInsets.Symmetric(horizontal: 16), child: new Row(crossAxisAlignment: CrossAxisAlignment.Center, spacing: 8, children:
                [
                    ..item.Icon is null ? Array.Empty<Widget>() : [new Icon(item.Icon, 16, fore)],
                    new Expanded(new Text(item.Label, softWrap: false, overflow: TextOverflow.Ellipsis, maxLines: 1, style: theme.Type.BodyCompact01.Merge(new TextStyle(Color: fore)))),
                ]));
            }, item.Disabled ? null : () => Choose(item), focusable: false)));
        }
        return new Focus(autofocus: true, onKey: OnKey, child: CarbonPopup.Shadowed(theme, new Column(rows, mainAxisSize: MainAxisSize.Min, crossAxisAlignment: CrossAxisAlignment.Stretch)));
    }
}

/// <summary>A Carbon overflow menu: a kebab button that opens a short list of actions. A <c>Danger</c> item turns red when hovered.</summary>
public sealed class CarbonOverflowMenu(IReadOnlyList<CarbonMenuItem> items, CarbonButtonSize size = CarbonButtonSize.Medium, float menuWidth = 160, Key? key = null) : StatefulWidget(key)
{
    internal IReadOnlyList<CarbonMenuItem> Items => items;
    internal CarbonButtonSize Size => size;
    internal float MenuWidth => menuWidth;
    public override State CreateState() => new CarbonOverflowMenuState();
}

sealed class CarbonOverflowMenuState : State<CarbonOverflowMenu>
{
    OverlayEntry? _entry;
    readonly FocusNode _node = new() { DebugLabel = "CarbonOverflowMenu" };

    public override void Dispose() => Close(false);

    void Close(bool refocus = true)
    {
        _entry?.Remove();
        _entry = null;
        if (Mounted) SetState();
        if (refocus && Mounted) _node.RequestFocus();
    }

    void Toggle()
    {
        if (_entry is not null) { Close(); return; }
        if (Context.FindRenderObject() is not RenderBox box) return;
        float item = 40;
        float height = Widget.Items.Count * item + Widget.Items.Count(i => i.DividerBefore);
        _entry = CarbonPopup.Open(Context, box, () => new CarbonMenuPanel(Widget.Items, () => Close(), item), height, () => Close(), Widget.MenuWidth, alignEnd: true);
        SetState();
    }

    public override Widget Build(BuildContext context)
    {
        var theme = CarbonTheme.Of(context);
        var c = theme.Colors;
        float s = (float)Widget.Size;
        bool open = _entry is not null;
        return new Interactive((ctx, st) => CarbonFocus.Around(st.FocusVisible, theme, new Container(width: s, height: s,
            color: open ? c.Layer01 : st.Hover ? c.BackgroundHover : Colors.Transparent, child: new Center(new Icon(Icons.MoreVert, 16, c.IconPrimary)))), Toggle, focusNode: _node);
    }
}

public enum CarbonModalSize { Small, Medium, Large }

/// <summary>Carbon modals. <see cref="Show"/> puts a dialog above the app with a scrim; Escape or the close button dismiss it.</summary>
public static class CarbonModal
{
    /// <summary>
    /// Shows a modal and returns a function that closes it. With a <paramref name="primaryLabel"/> it gets a footer of full-width buttons (the secondary
    /// one first); without, it is a passive modal that only has the close button. The primary action closes the modal after running unless it returns false.
    /// </summary>
    public static Action Show(BuildContext context, string title, Widget body, string? label = null, string? primaryLabel = null, Func<bool>? onPrimary = null,
        string? secondaryLabel = null, Action? onSecondary = null, bool danger = false, CarbonModalSize size = CarbonModalSize.Medium, Action? onClosed = null)
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
            onClosed?.Invoke();
        }

        entry = new OverlayEntry(ctx =>
        {
            var theme = CarbonTheme.Of(context);
            var c = theme.Colors;
            float width = size switch { CarbonModalSize.Small => 400f, CarbonModalSize.Large => 800f, _ => 600f };
            Widget footer = primaryLabel is null ? new SizedBox() : new Row(children:
            [
                ..secondaryLabel is null ? Array.Empty<Widget>() : [new Expanded(new CarbonButton(new Text(secondaryLabel), () => { onSecondary?.Invoke(); Close(); },
                    CarbonButtonKind.Secondary, CarbonButtonSize.ExtraLarge, fill: true))],
                new Expanded(new CarbonButton(new Text(primaryLabel), () => { if (onPrimary?.Invoke() ?? true) Close(); },
                    danger ? CarbonButtonKind.Danger : CarbonButtonKind.Primary, CarbonButtonSize.ExtraLarge, fill: true)),
            ]);
            Widget box = new ConstrainedBox(new BoxConstraints(0, width, 0, float.PositiveInfinity), new Container(
                color: c.Layer01, child: new Column(crossAxisAlignment: CrossAxisAlignment.Stretch, mainAxisSize: MainAxisSize.Min, children:
                [
                    new Stack([
                        new Padding(EdgeInsets.Only(left: 16, top: 16, right: 64, bottom: 24), new Column(crossAxisAlignment: CrossAxisAlignment.Start, mainAxisSize: MainAxisSize.Min, spacing: 4, children:
                        [
                            ..label is null ? Array.Empty<Widget>() : [new Text(label, style: theme.Type.Label01.Merge(new TextStyle(Color: c.TextSecondary)))],
                            new Text(title, style: theme.Type.Heading03.Merge(new TextStyle(Color: c.TextPrimary))),
                        ])),
                        new Positioned(new Interactive((cx, st) => CarbonFocus.Around(st.FocusVisible, theme, new Container(width: 48, height: 48,
                            color: st.Hover ? c.LayerHover01 : Colors.Transparent, child: new Center(new Icon(Icons.Close, 20, c.IconPrimary)))), Close), top: 0, right: 0),
                    ], clip: false),
                    new Padding(EdgeInsets.Only(left: 16, right: 16, bottom: 32), DefaultTextStyle.Merge(context, theme.Type.Body01.Merge(new TextStyle(Color: c.TextPrimary)), body)),
                    footer,
                ])));
            return CarbonTheme.Wrap(context, new Focus(autofocus: true, trapFocus: true, skipTraversal: true,
                onKey: e => { if (e.IsDown && e.Key == "Escape") { Close(); return true; } return false; },
                child: new Stack([
                    Positioned.Fill(new GestureDetector(onTap: Close, behavior: HitTestBehavior.Opaque, child: new ColoredBox(c.Overlay))),
                    new Center(new Padding(EdgeInsets.All(16), box)),
                ], fit: StackFit.Expand, clip: false)));
        });
        overlay.Insert(entry);
        return Close;
    }
}

/// <summary>Carbon toasts: high-contrast notifications that stack down the top-right corner and dismiss themselves.</summary>
public static class CarbonToast
{
    sealed record Toast(CarbonNotificationKind Kind, string Title, string? Subtitle, Action Close);

    static readonly List<Toast> Open = new();
    static OverlayEntry? _entry;

    /// <summary>Shows a toast and returns a function that dismisses it. It goes away on its own after <paramref name="timeout"/>; pass a zero timeout to keep it until dismissed.</summary>
    public static Action Show(BuildContext context, CarbonNotificationKind kind, string title, string? subtitle = null, TimeSpan? timeout = null)
    {
        var overlay = Overlay.Of(context);
        Toast? toast = null;
        void Close()
        {
            if (toast is null || !Open.Remove(toast)) return;
            if (Open.Count == 0) { _entry?.Remove(); _entry = null; }
            else _entry?.MarkNeedsBuild();
        }
        toast = new Toast(kind, title, subtitle, Close);
        Open.Add(toast);

        if (_entry is null)
        {
            _entry = new OverlayEntry(ctx => CarbonTheme.Wrap(context, new Stack([new Positioned(new Column(mainAxisSize: MainAxisSize.Min, spacing: 8,
                children: Open.Select(t => (Widget)new CarbonNotification(t.Kind, t.Title, t.Subtitle, t.Close, toast: true)).ToList()), top: 56, right: 16)],
                fit: StackFit.Expand, clip: false)));
            overlay.Insert(_entry);
        }
        else _entry.MarkNeedsBuild();

        var after = timeout ?? TimeSpan.FromSeconds(5);
        if (after > TimeSpan.Zero) WidgetsBinding.Instance.ScheduleTimer(after, Close);
        return Close;
    }
}
