using System.Globalization;
using SkiaSharp;

namespace Sway.Widgets;

// ---- menus ----

public abstract record MenuEntry<T>;

/// <summary>A selectable row in a popup menu.</summary>
public sealed record MenuItem<T>(T Value, Widget Child, bool Enabled = true, IconData? Icon = null) : MenuEntry<T>;

/// <summary>A thin rule between groups of menu items.</summary>
public sealed record MenuDivider<T> : MenuEntry<T>;

public static class Menus
{
    internal const float ItemHeight = 48, DividerHeight = 17, VerticalPadding = 8, MaxWidth = 280;

    /// <summary>
    /// Opens a menu next to <paramref name="anchor"/> (a rectangle in window coordinates), below it if there is room, otherwise above.
    /// Returns a function that closes it. Escape or a click outside closes it too.
    /// </summary>
    public static Action Show<T>(BuildContext context, Rect anchor, IReadOnlyList<MenuEntry<T>> items, Action<T> onSelected, float minWidth = 112)
    {
        var overlay = Overlay.Of(context);
        var window = WidgetsBinding.Instance.RenderView.WindowSize;
        var previousFocus = WidgetsBinding.Instance.Focus.Primary;
        float estimated = items.Sum(i => i is MenuDivider<T> ? DividerHeight : ItemHeight) + VerticalPadding * 2;
        bool above = anchor.Bottom + 4 + estimated > window.Height && anchor.Top - 4 - estimated > 0;
        bool alignEnd = anchor.Left + MaxWidth > window.Width && anchor.Right - MaxWidth >= 0;

        OverlayEntry? entry = null;
        void Close()
        {
            if (entry is null) return;
            entry.Remove();
            entry = null;
            if (previousFocus is { Element.Mounted: true }) previousFocus.RequestFocus();
        }

        entry = new OverlayEntry(ctx => Dialogs.Wrap(context, new Stack([
            Positioned.Fill(new GestureDetector(onTap: Close, behavior: HitTestBehavior.Opaque)),
            new Positioned(
                new PopupMenu<T>(items, v => { Close(); onSelected(v); }, Close, minWidth),
                left: alignEnd ? null : anchor.Left, right: alignEnd ? window.Width - anchor.Right : null,
                top: above ? null : anchor.Bottom + 4, bottom: above ? window.Height - anchor.Top + 4 : null),
        ], fit: StackFit.Expand, clip: false)));
        overlay.Insert(entry);
        return Close;
    }

    /// <summary>The window rectangle a context's widget occupies, for anchoring a menu to it.</summary>
    public static Rect AnchorOf(BuildContext context) =>
        context.FindRenderObject() is RenderBox { SizeOrNull: { } size } box
            ? new Rect(box.LocalToGlobal(Offset.Zero).Dx, box.LocalToGlobal(Offset.Zero).Dy, size.Width, size.Height)
            : new Rect(0, 0, 0, 0);
}

/// <summary>A button that opens a menu below itself; its child defaults to a three-dot icon button.</summary>
public sealed class PopupMenuButton<T>(IReadOnlyList<MenuEntry<T>> items, Action<T> onSelected, Widget? child = null, Key? key = null)
    : StatelessWidget(key)
{
    public override Widget Build(BuildContext context) => new Builder(ctx =>
    {
        void Open() => Menus.Show(ctx, Menus.AnchorOf(ctx), items, onSelected);
        return child is null
            ? new IconButton(new Icon(Icons.MoreVert), Open)
            : new GestureDetector(child, onTap: Open, behavior: HitTestBehavior.Opaque);
    });
}

sealed class PopupMenu<T>(IReadOnlyList<MenuEntry<T>> items, Action<T> onSelect, Action onClose, float minWidth) : StatefulWidget
{
    internal IReadOnlyList<MenuEntry<T>> Items => items;
    internal Action<T> OnSelect => onSelect;
    internal Action OnClose => onClose;
    internal float MinWidth => minWidth;
    public override State CreateState() => new PopupMenuState<T>();
}

sealed class PopupMenuState<T> : State<PopupMenu<T>>
{
    int _highlight = -1;

    bool IsEnabledItem(int i) => Widget.Items[i] is MenuItem<T> { Enabled: true };

    int Next(int from, int step)
    {
        for (int i = from + step; i >= 0 && i < Widget.Items.Count; i += step)
            if (IsEnabledItem(i)) return i;
        return from;
    }

    bool OnKey(KeyEvent e)
    {
        if (!e.IsDown) return true;
        switch (e.Key)
        {
            case "ArrowDown": SetState(() => _highlight = Next(_highlight < 0 ? -1 : _highlight, 1)); break;
            case "ArrowUp": SetState(() => _highlight = Next(_highlight < 0 ? Widget.Items.Count : _highlight, -1)); break;
            case "Home": SetState(() => _highlight = Next(-1, 1)); break;
            case "End": SetState(() => _highlight = Next(Widget.Items.Count, -1)); break;
            case "Enter" or " ":
                if (_highlight >= 0 && Widget.Items[_highlight] is MenuItem<T> item) Widget.OnSelect(item.Value);
                break;
            case "Escape" or "Tab": Widget.OnClose(); break;
        }
        return true; // modal: swallow keys
    }

    public override Widget Build(BuildContext context)
    {
        var theme = Theme.Of(context);
        var s = theme.ColorScheme;
        var rows = new List<Widget>();
        for (int i = 0; i < Widget.Items.Count; i++)
        {
            int index = i;
            if (Widget.Items[i] is MenuDivider<T>)
            {
                rows.Add(new SizedBox(height: Menus.DividerHeight, child: new Center(new SizedBox(height: 1, child: new ColoredBox(s.OutlineVariant)))));
                continue;
            }
            var item = (MenuItem<T>)Widget.Items[i];
            var fg = item.Enabled ? s.OnSurface : s.OnSurface.WithOpacity(0.38f);
            rows.Add(new SizedBox(height: Menus.ItemHeight, child: new Interactive((ctx, st) => new Container(
                    padding: EdgeInsets.Symmetric(horizontal: 12), alignment: Alignment.CenterLeft,
                    color: StateLayer.Blend(Colors.Transparent, s.OnSurface, _highlight == index ? 0.10f : StateLayer.Opacity(st)),
                    child: new Row(mainAxisSize: MainAxisSize.Min, spacing: 12, children:
                    [
                        ..item.Icon is null ? Array.Empty<Widget>() : [new Icon(item.Icon, 24, item.Enabled ? s.OnSurfaceVariant : fg)],
                        DefaultTextStyle.Merge(ctx, theme.TextTheme.LabelLarge.Merge(new TextStyle(Color: fg)), item.Child),
                    ])),
                item.Enabled ? () => Widget.OnSelect(item.Value) : null, focusable: false)));
        }

        return new Focus(autofocus: true, trapFocus: true, skipTraversal: true, onKey: OnKey, child: new ConstrainedBox(
            new BoxConstraints(Widget.MinWidth, Menus.MaxWidth, 0, float.PositiveInfinity),
            new DecoratedBox(new BoxDecoration(Color: s.SurfaceContainer, BorderRadius: BorderRadius.Circular(Shapes.ExtraSmall),
                BoxShadow: Elevation.Shadows(2, s.Shadow)),
                new ClipRRect(BorderRadius.Circular(Shapes.ExtraSmall), new IntrinsicWidth(new Padding(EdgeInsets.Symmetric(vertical: Menus.VerticalPadding),
                    new Column(rows, mainAxisSize: MainAxisSize.Min, crossAxisAlignment: CrossAxisAlignment.Stretch)))))));
    }
}

// ---- drawer and bottom sheet ----

public enum DrawerSide { Start, End }

/// <summary>A panel that slides in over a scrim: a navigation drawer from either side, or a sheet from the bottom.</summary>
public static class Panels
{
    /// <summary>Slides a drawer in from the start (or end) edge. The builder gets a function that slides it out and removes it.</summary>
    public static Action ShowDrawer(BuildContext context, Func<BuildContext, Action, Widget> builder, DrawerSide side = DrawerSide.Start,
        float width = 304, bool dismissible = true) =>
        Show(context, builder, side == DrawerSide.Start ? PanelEdge.Start : PanelEdge.End, width, dismissible);

    /// <summary>Slides a modal bottom sheet up. It can be dragged down to dismiss.</summary>
    public static Action ShowBottomSheet(BuildContext context, Func<BuildContext, Action, Widget> builder, bool dismissible = true) =>
        Show(context, builder, PanelEdge.Bottom, null, dismissible);

    static Action Show(BuildContext context, Func<BuildContext, Action, Widget> builder, PanelEdge edge, float? width, bool dismissible)
    {
        var overlay = Overlay.Of(context);
        var previousFocus = WidgetsBinding.Instance.Focus.Primary;
        OverlayEntry? entry = null;
        Action? requestClose = null;
        void Remove()
        {
            if (entry is null) return;
            entry.Remove();
            entry = null;
            if (previousFocus is { Element.Mounted: true }) previousFocus.RequestFocus();
        }
        entry = new OverlayEntry(ctx => Dialogs.Wrap(context, new ModalPanel(edge, width, dismissible, builder, Remove, close => requestClose = close)));
        overlay.Insert(entry);
        return () => (requestClose ?? Remove)();
    }
}

enum PanelEdge { Start, End, Bottom }

sealed class ModalPanel(PanelEdge edge, float? width, bool dismissible, Func<BuildContext, Action, Widget> builder, Action onRemoved,
    Action<Action> registerClose) : StatefulWidget
{
    internal PanelEdge Edge => edge;
    internal float? Width => width;
    internal bool Dismissible => dismissible;
    internal Func<BuildContext, Action, Widget> Builder => builder;
    internal Action OnRemoved => onRemoved;
    internal Action<Action> RegisterClose => registerClose;
    public override State CreateState() => new ModalPanelState();
}

sealed class ModalPanelState : TickerProviderState<ModalPanel>
{
    AnimationController _controller = null!;
    CurvedAnimation _curved = null!;
    float _drag;
    bool _closing;

    public override void InitState()
    {
        _controller = new AnimationController(this, TimeSpan.FromMilliseconds(260));
        _curved = new CurvedAnimation(_controller, Curves.EaseOutCubic, Curves.EaseInCubic);
        _controller.AddStatusListener(status =>
        {
            if (status == AnimationStatus.Dismissed && _closing) Widget.OnRemoved();
        });
        Widget.RegisterClose(Close);
        _controller.Forward();
    }

    public override void Dispose()
    {
        _curved.Dispose();
        _controller.Dispose();
        base.Dispose();
    }

    void Close()
    {
        if (_closing) return;
        _closing = true;
        _controller.Reverse();
    }

    void DismissIfAllowed()
    {
        if (Widget.Dismissible) Close();
    }

    bool OnKey(KeyEvent e)
    {
        if (e.IsDown && e.Key == "Escape" && Widget.Dismissible)
        {
            Close();
            return true;
        }
        return false;
    }

    public override Widget Build(BuildContext context)
    {
        var s = Theme.Of(context).ColorScheme;
        bool bottom = Widget.Edge == PanelEdge.Bottom;
        bool rtl = Directionality.Of(context) == TextDirection.Rtl;
        // Start means the left in left-to-right text and the right in right-to-left text.
        bool left = Widget.Edge == PanelEdge.Start != rtl;

        Widget panel = Widget.Builder(context, Close);
        if (bottom)
        {
            panel = new GestureDetector(
                onVerticalDragUpdate: d => SetState(() => _drag = Math.Max(0, _drag + d.Delta.Dy)),
                onVerticalDragEnd: d =>
                {
                    bool dismiss = Widget.Dismissible && (_drag > 120 || d.Velocity.Dy > 800);
                    SetState(() => _drag = 0);
                    if (dismiss) Close();
                },
                child: panel);
        }

        return new Focus(autofocus: true, trapFocus: true, skipTraversal: true, onKey: OnKey,
            child: new AnimatedBuilder(_curved, (ctx, _) =>
            {
                float t = _curved.Value;
                Widget slid = bottom
                    ? new FractionalTranslation(new Offset(0, 1 - t), new Transform(SKMatrix.CreateTranslation(0, _drag), panel))
                    : new FractionalTranslation(new Offset((left ? -1 : 1) * (1 - t), 0), panel);
                return new Stack([
                    Positioned.Fill(new GestureDetector(onTap: DismissIfAllowed, behavior: HitTestBehavior.Opaque,
                        child: new ColoredBox(s.Scrim.WithOpacity(0.32f * t)))),
                    Positioned.Fill(new Align(bottom ? Alignment.BottomCenter : left ? Alignment.CenterLeft : Alignment.CenterRight,
                        bottom ? slid : new SizedBox(width: Widget.Width, child: slid))),
                ], fit: StackFit.Expand, clip: false);
            }));
    }
}

/// <summary>The content of a Material 3 navigation drawer: an optional header and a list of destinations.</summary>
public sealed class NavigationDrawer(int selectedIndex, IReadOnlyList<NavigationDestination> destinations, Action<int>? onDestinationSelected = null,
    Widget? header = null, Key? key = null) : StatelessWidget(key)
{
    public override Widget Build(BuildContext context)
    {
        var theme = Theme.Of(context);
        var s = theme.ColorScheme;
        var radius = new BorderRadius(new Radius(0, 0), new Radius(16, 16), new Radius(16, 16), new Radius(0, 0));
        var rows = new List<Widget>();
        if (header is not null)
            rows.Add(new Padding(EdgeInsets.Symmetric(28, 16), DefaultTextStyle.Merge(context, theme.TextTheme.TitleSmall.Merge(new TextStyle(Color: s.OnSurfaceVariant)), header)));
        for (int i = 0; i < destinations.Count; i++)
        {
            int index = i;
            var d = destinations[i];
            bool selected = i == selectedIndex;
            rows.Add(new Padding(EdgeInsets.Symmetric(horizontal: 12), new Interactive((ctx, st) =>
            {
                var fg = selected ? s.OnSecondaryContainer : s.OnSurfaceVariant;
                return new AnimatedContainer(TimeSpan.FromMilliseconds(150), height: 56, padding: EdgeInsets.Symmetric(horizontal: 16),
                    decoration: new BoxDecoration(
                        Color: StateLayer.Blend(selected ? s.SecondaryContainer : Colors.Transparent, fg, StateLayer.Opacity(st)),
                        BorderRadius: BorderRadius.Circular(28)),
                    child: new Row(spacing: 12, children:
                    [
                        new Icon(selected ? d.SelectedIcon ?? d.Icon : d.Icon, 24, fg),
                        new Text(d.Label, style: theme.TextTheme.LabelLarge.Merge(new TextStyle(Color: fg))),
                    ]));
            }, onDestinationSelected is null ? null : () => onDestinationSelected(index))));
        }
        return new DecoratedBox(new BoxDecoration(Color: s.SurfaceContainerLow, BorderRadius: radius, BoxShadow: Elevation.Shadows(1, s.Shadow)),
            new ClipRRect(radius, new SingleChildScrollView(
                new Padding(EdgeInsets.Symmetric(vertical: 12), new Column(rows, mainAxisSize: MainAxisSize.Min, crossAxisAlignment: CrossAxisAlignment.Stretch)))));
    }
}

/// <summary>The surface for <see cref="Panels.ShowBottomSheet"/>: rounded top corners and a drag handle.</summary>
public sealed class BottomSheet(Widget child, Key? key = null) : StatelessWidget(key)
{
    public override Widget Build(BuildContext context)
    {
        var s = Theme.Of(context).ColorScheme;
        var radius = new BorderRadius(new Radius(28, 28), new Radius(28, 28), new Radius(0, 0), new Radius(0, 0));
        return new ConstrainedBox(new BoxConstraints(0, 640, 0, float.PositiveInfinity),
            new DecoratedBox(new BoxDecoration(Color: s.SurfaceContainerLow, BorderRadius: radius, BoxShadow: Elevation.Shadows(1, s.Shadow)),
                new ClipRRect(radius, new Column(mainAxisSize: MainAxisSize.Min, crossAxisAlignment: CrossAxisAlignment.Stretch, children:
                [
                    new Padding(EdgeInsets.Symmetric(vertical: 16), new Center(new SizedBox(32, 4, new DecoratedBox(
                        new BoxDecoration(Color: s.OnSurfaceVariant.WithOpacity(0.4f), BorderRadius: BorderRadius.Circular(2)))))),
                    child,
                ]))));
    }
}

// ---- date picker ----

public static class DatePicker
{
    /// <summary>Shows a Material 3 date picker dialog. <paramref name="onSelected"/> runs with the chosen date when OK is pressed.</summary>
    public static Action Show(BuildContext context, DateTime initialDate, DateTime firstDate, DateTime lastDate, Action<DateTime> onSelected) =>
        Dialogs.Show(context, (_, close) => new DatePickerDialog(initialDate.Date, firstDate.Date, lastDate.Date, close, d => { close(); onSelected(d); }));
}

public sealed class DatePickerDialog(DateTime initialDate, DateTime firstDate, DateTime lastDate, Action onCancel, Action<DateTime> onConfirm,
    Key? key = null) : StatefulWidget(key)
{
    internal DateTime Initial => initialDate;
    internal DateTime First => firstDate;
    internal DateTime Last => lastDate;
    internal Action OnCancel => onCancel;
    internal Action<DateTime> OnConfirm => onConfirm;
    public override State CreateState() => new DatePickerDialogState();
}

sealed class DatePickerDialogState : State<DatePickerDialog>
{
    static readonly CultureInfo Culture = CultureInfo.InvariantCulture;
    const float Cell = 40;

    DateTime _selected, _month;

    public override void InitState()
    {
        _selected = Clamp(Widget.Initial);
        _month = new DateTime(_selected.Year, _selected.Month, 1);
    }

    DateTime Clamp(DateTime d) => d < Widget.First ? Widget.First : d > Widget.Last ? Widget.Last : d;

    bool CanGoBack => new DateTime(_month.Year, _month.Month, 1) > new DateTime(Widget.First.Year, Widget.First.Month, 1);
    bool CanGoForward => new DateTime(_month.Year, _month.Month, 1) < new DateTime(Widget.Last.Year, Widget.Last.Month, 1);

    public override Widget Build(BuildContext context)
    {
        var theme = Theme.Of(context);
        var s = theme.ColorScheme;
        var today = DateTime.Today;
        int daysInMonth = DateTime.DaysInMonth(_month.Year, _month.Month);
        int leading = (int)_month.DayOfWeek; // weeks start on Sunday

        var weekdays = new[] { "S", "M", "T", "W", "T", "F", "S" }
            .Select(d => (Widget)new SizedBox(Cell, Cell, new Center(new Text(d, style: theme.TextTheme.BodyMedium.Merge(new TextStyle(Color: s.OnSurface)))))).ToList();

        var weeks = new List<Widget>();
        for (int start = 1 - leading; start <= daysInMonth; start += 7)
        {
            var days = new List<Widget>();
            for (int day = start; day < start + 7; day++)
            {
                if (day < 1 || day > daysInMonth)
                {
                    days.Add(new SizedBox(Cell, Cell));
                    continue;
                }
                var date = new DateTime(_month.Year, _month.Month, day);
                bool selected = date == _selected, isToday = date == today, enabled = date >= Widget.First && date <= Widget.Last;
                string label = day.ToString(Culture); // the builder below runs later, after the loop variable has moved on
                days.Add(new Interactive((ctx, st) =>
                {
                    var fg = !enabled ? s.OnSurface.WithOpacity(0.38f) : selected ? s.OnPrimary : isToday ? s.Primary : s.OnSurface;
                    return new AnimatedContainer(TimeSpan.FromMilliseconds(120), width: Cell, height: Cell, alignment: Alignment.Center,
                        decoration: new BoxDecoration(
                            Color: selected ? s.Primary : StateLayer.Blend(Colors.Transparent, s.OnSurface, enabled ? StateLayer.Opacity(st) : 0),
                            Shape: BoxShape.Circle, Border: isToday && !selected ? Border.All(s.Outline) : null),
                        child: new Text(label, style: theme.TextTheme.BodyMedium.Merge(new TextStyle(Color: fg))));
                }, enabled ? () => SetState(() => _selected = date) : null));
            }
            weeks.Add(new Row(days));
        }

        return new SizedBox(width: 328, child: new Material(new Column(mainAxisSize: MainAxisSize.Min, crossAxisAlignment: CrossAxisAlignment.Start, children:
        [
            new Padding(EdgeInsets.Only(left: 24, right: 12, top: 16, bottom: 12), new Column(crossAxisAlignment: CrossAxisAlignment.Start, spacing: 36, children:
            [
                new Text("Select date", style: theme.TextTheme.LabelLarge.Merge(new TextStyle(Color: s.OnSurfaceVariant))),
                new Text(_selected.ToString("ddd, MMM d", Culture), style: theme.TextTheme.HeadlineMedium.Merge(new TextStyle(Color: s.OnSurface))),
            ])),
            new SizedBox(height: 1, child: new ColoredBox(s.OutlineVariant)),
            new Padding(EdgeInsets.Symmetric(horizontal: 12), new Column(crossAxisAlignment: CrossAxisAlignment.Start, children:
            [
                new SizedBox(height: 48, child: new Row(children:
                [
                    new Expanded(new Padding(EdgeInsets.Only(left: 12), new Text(_month.ToString("MMMM yyyy", Culture),
                        style: theme.TextTheme.LabelLarge.Merge(new TextStyle(Color: s.OnSurfaceVariant))))),
                    new IconButton(new Icon(Icons.ChevronLeft), CanGoBack ? () => SetState(() => _month = _month.AddMonths(-1)) : null),
                    new IconButton(new Icon(Icons.ChevronRight), CanGoForward ? () => SetState(() => _month = _month.AddMonths(1)) : null),
                ])),
                new Row(weekdays),
                ..weeks,
            ])),
            new Padding(EdgeInsets.Only(left: 12, right: 12, top: 8, bottom: 8), new Align(Alignment.CenterRight, new Row(mainAxisSize: MainAxisSize.Min, spacing: 8, children:
            [
                new TextButton(new Text("Cancel"), Widget.OnCancel),
                new TextButton(new Text("OK"), () => Widget.OnConfirm(_selected)),
            ]))),
        ]), s.SurfaceContainerHigh, 3, BorderRadius.Circular(Shapes.ExtraLarge)));
    }
}
