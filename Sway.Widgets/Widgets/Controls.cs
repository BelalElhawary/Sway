using SkiaSharp;

namespace Sway.Widgets;

public readonly record struct InteractionState(bool Hover, bool Pressed, bool Focused, bool FocusVisible);

/// <summary>
/// Hover, press and keyboard-focus tracking for custom controls. Space and Enter activate it.
/// Pass a null <c>onTap</c> to disable it.
/// </summary>
public sealed class Interactive(Func<BuildContext, InteractionState, Widget> builder, Action? onTap = null,
    MouseCursor cursor = MouseCursor.Click, bool focusable = true, FocusNode? focusNode = null, bool autofocus = false, Key? key = null) : StatefulWidget(key)
{
    internal Func<BuildContext, InteractionState, Widget> Builder => builder;
    internal Action? OnTap => onTap;
    internal MouseCursor Cursor => cursor;
    internal bool Focusable => focusable;
    internal FocusNode? FocusNode => focusNode;
    internal bool Autofocus => autofocus;
    public override State CreateState() => new InteractiveState();
}

sealed class InteractiveState : State<Interactive>
{
    bool _hover, _pressed, _focused;

    void Set(Action a) { if (Mounted) SetState(a); }

    bool OnKey(KeyEvent e)
    {
        if (!e.IsDown || Widget.OnTap is null) return false;
        if (e.Key is " " or "Enter" && !e.Command) { Widget.OnTap(); return true; }
        return false;
    }

    public override Widget Build(BuildContext context)
    {
        bool enabled = Widget.OnTap is not null;
        Widget child = new Builder(ctx => Widget.Builder(ctx,
            new InteractionState(_hover && enabled, _pressed && enabled, _focused, _focused && WidgetsBinding.Instance.Focus.FocusVisible)));

        child = new GestureDetector(
            onTap: Widget.OnTap,
            onTapDown: enabled ? _ => Set(() => _pressed = true) : null,
            onTapUp: enabled ? _ => Set(() => _pressed = false) : null,
            onTapCancel: enabled ? () => Set(() => _pressed = false) : null,
            behavior: HitTestBehavior.Opaque,
            child: child);

        child = new MouseRegion(
            cursor: enabled ? Widget.Cursor : MouseCursor.Default,
            onEnter: _ => Set(() => _hover = true),
            onExit: _ => Set(() => { _hover = false; _pressed = false; }),
            opaque: false,
            child: child);

        if (Widget.Focusable)
            child = new Focus(child, Widget.FocusNode, Widget.Autofocus, onKey: OnKey, onFocusChange: f => Set(() => _focused = f),
                canRequestFocus: enabled);
        return child;
    }
}

// ---- painters and helpers ----

sealed class CheckPainter(SKColor color, float strokeWidth = 2) : CustomPainter
{
    public override void Paint(SKCanvas canvas, Size size)
    {
        using var p = new SKPaint { Color = color, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = strokeWidth,
            StrokeCap = SKStrokeCap.Round, StrokeJoin = SKStrokeJoin.Round };
        using var path = new SKPath();
        path.MoveTo(size.Width * 0.20f, size.Height * 0.52f);
        path.LineTo(size.Width * 0.42f, size.Height * 0.74f);
        path.LineTo(size.Width * 0.80f, size.Height * 0.28f);
        canvas.DrawPath(path, p);
    }
    public override bool ShouldRepaint(CustomPainter old) => true;
}

sealed class ChevronPainter(SKColor color, bool up = false) : CustomPainter
{
    public override void Paint(SKCanvas canvas, Size size)
    {
        using var p = new SKPaint { Color = color, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1.8f,
            StrokeCap = SKStrokeCap.Round, StrokeJoin = SKStrokeJoin.Round };
        float cx = size.Width / 2, cy = size.Height / 2, w = size.Width * 0.28f, h = size.Height * 0.14f;
        using var path = new SKPath();
        float s = up ? -1 : 1;
        path.MoveTo(cx - w, cy - h * s);
        path.LineTo(cx, cy + h * s);
        path.LineTo(cx + w, cy - h * s);
        canvas.DrawPath(path, p);
    }
    public override bool ShouldRepaint(CustomPainter old) => true;
}

static class FocusRing
{
    /// <summary>Draws a translucent focus outline around <paramref name="child"/> without affecting layout.</summary>
    public static Widget Around(bool visible, BorderRadius radius, Widget child, SKColor color, float inset = -3) =>
        visible
            ? new Stack([child, Positioned.Fill(new IgnorePointer(new DecoratedBox(
                new BoxDecoration(Border: Border.All(color.WithOpacity(0.5f), 3), BorderRadius: radius))), inset, inset, inset, inset)], clip: false)
            : child;
}

/// <summary>Makes its subtree invisible to hit testing.</summary>
public sealed class IgnorePointer(Widget? child = null, bool ignoring = true, Key? key = null) : SingleChildRenderObjectWidget(child, key)
{
    public override RenderObject CreateRenderObject(BuildContext context) => new RenderIgnorePointer(ignoring);
    public override void UpdateRenderObject(BuildContext context, RenderObject ro) => ((RenderIgnorePointer)ro).Ignoring = ignoring;
}

sealed class RenderIgnorePointer(bool ignoring) : RenderProxyBox
{
    public bool Ignoring { get; set; } = ignoring;
    public override bool HitTest(HitTestResult result, Offset position) => !Ignoring && base.HitTest(result, position);
}

// ---- checkbox / radio / switch (Material 3) ----

/// <summary>The 40px round hover/press/focus halo behind a toggle's mark.</summary>
static class Halo
{
    public static Widget Wrap(InteractionState s, SKColor color, bool enabled, Widget mark, ThemeData theme, BorderRadius markRadius, float size = 40)
    {
        // Themes without a halo mark keyboard focus with a border around the mark instead.
        if (!theme.Shape.ControlHalo)
            return new SizedBox(size, size, new Center(FocusRing.Around(s.FocusVisible, markRadius, mark, theme.ColorScheme.Primary, -2)));
        return new AnimatedContainer(TimeSpan.FromMilliseconds(100), width: size, height: size, alignment: Alignment.Center,
            decoration: new BoxDecoration(Color: color.WithOpacity(enabled ? StateLayer.Opacity(s) : 0), Shape: BoxShape.Circle), child: mark);
    }
}

public sealed class Checkbox(bool value, Action<bool>? onChanged = null, SKColor? activeColor = null, Key? key = null) : StatelessWidget(key)
{
    public override Widget Build(BuildContext context)
    {
        var theme = Theme.Of(context);
        var s = theme.ColorScheme;
        var radius = BorderRadius.Circular(theme.Shape.CheckboxRadius);
        var active = activeColor ?? s.Primary;
        bool enabled = onChanged is not null;
        return new Interactive((ctx, st) =>
        {
            var off = s.OnSurface.WithOpacity(0.38f);
            var boxColor = value ? (enabled ? active : off) : Colors.Transparent;
            var border = value ? boxColor : enabled ? s.OnSurfaceVariant : off;
            Widget box = new AnimatedContainer(TimeSpan.FromMilliseconds(120), width: 18, height: 18,
                decoration: new BoxDecoration(Color: boxColor, BorderRadius: radius, Border: Border.All(border, 2)),
                child: value ? new CustomPaint(new CheckPainter(s.OnPrimary), size: new Size(14, 14)) : null);
            return Halo.Wrap(st, value ? active : s.OnSurface, enabled, box, theme, radius);
        }, enabled ? () => onChanged!(!value) : null);
    }
}

public sealed class Radio<T>(T value, T? groupValue, Action<T>? onChanged = null, SKColor? activeColor = null, Key? key = null) : StatelessWidget(key)
    where T : notnull
{
    public override Widget Build(BuildContext context)
    {
        var theme = Theme.Of(context);
        var s = theme.ColorScheme;
        bool selected = EqualityComparer<T>.Default.Equals(value, groupValue);
        var active = activeColor ?? s.Primary;
        bool enabled = onChanged is not null;
        return new Interactive((ctx, st) =>
        {
            var off = s.OnSurface.WithOpacity(0.38f);
            var ring = !enabled ? off : selected ? active : s.OnSurfaceVariant;
            Widget mark = new AnimatedContainer(TimeSpan.FromMilliseconds(120), width: 20, height: 20, alignment: Alignment.Center,
                decoration: new BoxDecoration(Shape: BoxShape.Circle, Border: Border.All(ring, 2)),
                child: new AnimatedContainer(TimeSpan.FromMilliseconds(120), width: selected ? 10 : 0, height: selected ? 10 : 0,
                    decoration: new BoxDecoration(Color: enabled ? active : off, Shape: BoxShape.Circle)));
            return Halo.Wrap(st, selected ? active : s.OnSurface, enabled, mark, theme, BorderRadius.Circular(10));
        }, enabled ? () => onChanged!(value) : null);
    }
}

public sealed class Switch(bool value, Action<bool>? onChanged = null, SKColor? activeColor = null, Key? key = null) : StatelessWidget(key)
{
    public override Widget Build(BuildContext context)
    {
        var theme = Theme.Of(context);
        var s = theme.ColorScheme;
        bool enabled = onChanged is not null;
        var ms = TimeSpan.FromMilliseconds(180);
        if (theme.Shape.CompactSwitch) return Compact(s, enabled, ms);
        return new Interactive((ctx, st) =>
        {
            var trackOn = activeColor ?? s.Primary;
            var track = value ? trackOn : s.SurfaceContainerHighest;
            var outline = value ? trackOn : s.Outline;
            var handle = value ? s.OnPrimary : s.Outline;
            if (!enabled)
            {
                track = value ? s.OnSurface.WithOpacity(0.12f) : s.SurfaceContainerHighest.WithOpacity(0.12f);
                outline = s.OnSurface.WithOpacity(0.12f);
                handle = value ? s.Surface : s.OnSurface.WithOpacity(0.38f);
            }
            float size = st.Pressed ? 28 : value ? 24 : 16;

            // The 40px halo is positioned around the handle without enlarging the 24px slot it lives in.
            Widget thumb = new SizedBox(24, 24, new Stack([
                Positioned.Fill(new Center(new AnimatedContainer(ms, width: size, height: size, curve: Curves.EaseOutCubic,
                    decoration: new BoxDecoration(Color: handle, Shape: BoxShape.Circle)))),
                new Positioned(new IgnorePointer(new AnimatedContainer(TimeSpan.FromMilliseconds(100), decoration: new BoxDecoration(
                    Color: (value ? trackOn : s.OnSurface).WithOpacity(enabled ? StateLayer.Opacity(st) : 0), Shape: BoxShape.Circle))),
                    left: -8, top: -8, width: 40, height: 40),
            ], alignment: Alignment.Center, clip: false));

            // The track is 32px tall with a 2px border, leaving 28px for the 24px handle (28px when pressed) without vertical padding.
            return new AnimatedContainer(ms, width: 52, height: 32, padding: EdgeInsets.Symmetric(horizontal: 4, vertical: 0), curve: Curves.EaseOutCubic,
                decoration: new BoxDecoration(Color: track, BorderRadius: BorderRadius.Circular(16), Border: Border.All(outline, 2)),
                child: new AnimatedAlign(value ? Alignment.CenterRight : Alignment.CenterLeft, ms, thumb, Curves.EaseOutCubic));
        }, enabled ? () => onChanged!(!value) : null);
    }

    // A flat 48x24 track with an 18px thumb that slides inside it, as in Carbon.
    Widget Compact(ColorScheme s, bool enabled, TimeSpan ms) => new Interactive((ctx, st) =>
    {
        var track = !enabled ? s.OnSurface.WithOpacity(0.12f) : value ? activeColor ?? s.Primary : s.Outline;
        var thumb = !enabled ? s.OnSurface.WithOpacity(0.38f) : s.OnPrimary;
        var radius = BorderRadius.Circular(12);
        Widget body = new AnimatedContainer(ms, width: 48, height: 24, padding: EdgeInsets.Symmetric(horizontal: 3, vertical: 0), curve: Curves.EaseOutCubic,
            decoration: new BoxDecoration(Color: StateLayer.Blend(track, s.OnSurface, enabled ? StateLayer.Opacity(st) : 0), BorderRadius: radius),
            child: new AnimatedAlign(value ? Alignment.CenterRight : Alignment.CenterLeft, ms,
                new SizedBox(18, 18, new DecoratedBox(new BoxDecoration(Color: thumb, Shape: BoxShape.Circle))), Curves.EaseOutCubic));
        return FocusRing.Around(st.FocusVisible, radius, body, s.Primary, -2);
    }, enabled ? () => onChanged!(!value) : null);
}

// ---- buttons (Material 3) ----

public enum ButtonVariant { Filled, Tonal, Elevated, Outlined, Text }

public sealed class Button(Widget child, Action? onPressed = null, ButtonVariant variant = ButtonVariant.Filled, SKColor? color = null,
    IconData? icon = null, EdgeInsets? padding = null, Key? key = null) : StatelessWidget(key)
{
    public override Widget Build(BuildContext context)
    {
        var theme = Theme.Of(context);
        var s = theme.ColorScheme;
        var accent = color ?? s.AccentColor;
        bool enabled = onPressed is not null;
        var radius = BorderRadius.Circular(theme.Shape.Button);

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
            float wide = theme.Shape.ButtonPadding;
            var pad = padding ?? EdgeInsets.Only(left: icon is not null ? (text ? 12 : wide - 8) : (text ? 12 : wide), right: text ? 12 : wide);
            var layered = StateLayer.Blend(bg, fg, enabled ? StateLayer.Opacity(st) : 0);

            Widget label = DefaultTextStyle.Merge(ctx, theme.TextTheme.LabelLarge.Merge(new TextStyle(Color: fg)), child);
            Widget content = icon is null ? label : new Row(mainAxisSize: MainAxisSize.Min, spacing: 8, children: [new Icon(icon, 18, fg), label]);

            return new AnimatedContainer(TimeSpan.FromMilliseconds(120),
                constraints: new BoxConstraints(64, float.PositiveInfinity, theme.Shape.ControlHeight, theme.Shape.ControlHeight), padding: pad,
                decoration: new BoxDecoration(Color: layered, BorderRadius: radius, Border: border, BoxShadow: theme.Shape.Shadows(elevation, s.Shadow)),
                child: new Row(mainAxisSize: MainAxisSize.Min, mainAxisAlignment: MainAxisAlignment.Center, children: [content]));
        }, onPressed);
    }
}

public sealed class FilledButton(Widget child, Action? onPressed = null, IconData? icon = null, SKColor? color = null, Key? key = null) : StatelessWidget(key)
{
    public override Widget Build(BuildContext context) => new Button(child, onPressed, ButtonVariant.Filled, color, icon);
}

public sealed class FilledTonalButton(Widget child, Action? onPressed = null, IconData? icon = null, Key? key = null) : StatelessWidget(key)
{
    public override Widget Build(BuildContext context) => new Button(child, onPressed, ButtonVariant.Tonal, null, icon);
}

public sealed class ElevatedButton(Widget child, Action? onPressed = null, IconData? icon = null, Key? key = null) : StatelessWidget(key)
{
    public override Widget Build(BuildContext context) => new Button(child, onPressed, ButtonVariant.Elevated, null, icon);
}

public sealed class OutlinedButton(Widget child, Action? onPressed = null, IconData? icon = null, Key? key = null) : StatelessWidget(key)
{
    public override Widget Build(BuildContext context) => new Button(child, onPressed, ButtonVariant.Outlined, null, icon);
}

public sealed class TextButton(Widget child, Action? onPressed = null, IconData? icon = null, SKColor? color = null, Key? key = null) : StatelessWidget(key)
{
    public override Widget Build(BuildContext context) => new Button(child, onPressed, ButtonVariant.Text, color, icon);
}

// ---- dropdown ----

public sealed record DropdownMenuItem<T>(T Value, Widget Child, bool Enabled = true);

public sealed class DropdownButton<T>(IReadOnlyList<DropdownMenuItem<T>> items, T? value = default, Action<T>? onChanged = null,
    Widget? hint = null, string? label = null, double menuMaxHeight = 280, Key? key = null) : StatefulWidget(key) where T : notnull
{
    internal IReadOnlyList<DropdownMenuItem<T>> Items => items;
    internal T? Value => value;
    internal Action<T>? OnChanged => onChanged;
    internal Widget? Hint => hint;
    internal string? Label => label;
    internal float MenuMaxHeight => (float)menuMaxHeight;
    public override State CreateState() => new DropdownButtonState<T>();
}

sealed class DropdownButtonState<T> : State<DropdownButton<T>> where T : notnull
{
    const float ItemHeight = 48;
    OverlayEntry? _entry;
    readonly FocusNode _node = new() { DebugLabel = "Dropdown" };

    public override void Dispose() => Close(false);

    void Close(bool refocus = true)
    {
        _entry?.Remove();
        _entry = null;
        if (Mounted) SetState();
        if (refocus && Mounted) _node.RequestFocus();
    }

    void Open()
    {
        if (_entry is not null || Context.FindRenderObject() is not RenderBox box) return;
        var overlay = Overlay.MaybeOf(Context);
        if (overlay is null) return;

        var origin = box.LocalToGlobal(Offset.Zero);
        var size = box.Size;
        var window = WidgetsBinding.Instance.RenderView.WindowSize;
        float menuHeight = Math.Min(Widget.MenuMaxHeight, Widget.Items.Count * ItemHeight + 16);
        bool above = origin.Dy + size.Height + 4 + menuHeight > window.Height && origin.Dy - 4 - menuHeight > 0;
        float top = above ? origin.Dy - 4 - menuHeight : origin.Dy + size.Height + 4;
        var origin2 = Context;

        _entry = new OverlayEntry(ctx => Dialogs.Wrap(origin2, new Stack([
            Positioned.Fill(new GestureDetector(onTap: () => Close(), behavior: HitTestBehavior.Opaque)),
            new Positioned(new DropdownMenu<T>(Widget.Items, Widget.Value, v => { Close(); Widget.OnChanged?.Invoke(v); }, () => Close(), menuHeight, ItemHeight),
                left: origin.Dx, top: top, width: size.Width),
        ], fit: StackFit.Expand, clip: false)));
        overlay.Insert(_entry);
        SetState();
    }

    public override Widget Build(BuildContext context)
    {
        var theme = Theme.Of(context);
        var s = theme.ColorScheme;
        var selected = Widget.Items.FirstOrDefault(i => EqualityComparer<T>.Default.Equals(i.Value, Widget.Value));
        bool enabled = Widget.OnChanged is not null;
        bool open = _entry is not null;

        return new Interactive((ctx, st) =>
        {
            Widget content = selected is not null
                ? DefaultTextStyle.Merge(ctx, theme.TextTheme.BodyLarge.Merge(new TextStyle(Color: s.OnSurface)), selected.Child)
                : Widget.Hint is { } h ? DefaultTextStyle.Merge(ctx, theme.TextTheme.BodyLarge.Merge(new TextStyle(Color: s.OnSurfaceVariant)), h) : new SizedBox(height: 24);
            return new InputDecorator(
                new InputDecoration(LabelText: Widget.Label), focused: open || st.FocusVisible, hovered: st.Hover, hasContent: selected is not null || Widget.Hint is not null && Widget.Label is null,
                enabled: enabled,
                child: new Row(children: [new Expanded(content), new Icon(open ? Icons.ExpandLess : Icons.ExpandMore, 24, enabled ? s.OnSurfaceVariant : s.OnSurface.WithOpacity(0.38f))]));
        }, enabled ? () => { if (_entry is null) Open(); else Close(); } : null, focusNode: _node);
    }
}

sealed class DropdownMenu<T>(IReadOnlyList<DropdownMenuItem<T>> items, T? value, Action<T> onSelect, Action onClose, float height, float itemHeight) : StatefulWidget
    where T : notnull
{
    internal IReadOnlyList<DropdownMenuItem<T>> Items => items;
    internal T? Value => value;
    internal Action<T> OnSelect => onSelect;
    internal Action OnClose => onClose;
    internal float Height => height;
    internal float ItemHeight => itemHeight;
    public override State CreateState() => new DropdownMenuState<T>();
}

sealed class DropdownMenuState<T> : State<DropdownMenu<T>> where T : notnull
{
    int _highlight = -1;
    readonly ScrollController _scroll = new();

    public override void InitState()
    {
        _highlight = Widget.Items.ToList().FindIndex(i => EqualityComparer<T>.Default.Equals(i.Value, Widget.Value));
        if (_highlight < 0) _highlight = Next(-1, 1);
    }

    int Next(int from, int step)
    {
        for (int i = from + step; i >= 0 && i < Widget.Items.Count; i += step)
            if (Widget.Items[i].Enabled) return i;
        return from;
    }

    void Highlight(int index)
    {
        if (index < 0) return;
        SetState(() => _highlight = index);
        float ih = Widget.ItemHeight, top = 8 + index * ih, pos = _scroll.Offset;
        if (top < pos) _scroll.JumpTo(top - 8);
        else if (top + ih > pos + Widget.Height - 8) _scroll.JumpTo(top + ih - Widget.Height + 8);
    }

    bool OnKey(KeyEvent e)
    {
        if (!e.IsDown) return true;
        switch (e.Key)
        {
            case "ArrowDown": Highlight(Next(_highlight, 1)); break;
            case "ArrowUp": Highlight(Next(_highlight, -1)); break;
            case "Home": Highlight(Next(-1, 1)); break;
            case "End": Highlight(Next(Widget.Items.Count, -1)); break;
            case "Enter" or " ":
                if (_highlight >= 0) Widget.OnSelect(Widget.Items[_highlight].Value);
                break;
            case "Escape" or "Tab": Widget.OnClose(); break;
        }
        return true; // the menu is modal: swallow keys
    }

    public override Widget Build(BuildContext context)
    {
        var theme = Theme.Of(context);
        var s = theme.ColorScheme;
        var rows = new List<Widget>();
        for (int i = 0; i < Widget.Items.Count; i++)
        {
            int index = i;
            var item = Widget.Items[i];
            bool selected = EqualityComparer<T>.Default.Equals(item.Value, Widget.Value);
            rows.Add(new SizedBox(height: Widget.ItemHeight, child: new Interactive((ctx, st) => new Container(
                    padding: EdgeInsets.Symmetric(12, 0), alignment: Alignment.CenterLeft,
                    color: StateLayer.Blend(selected ? s.SecondaryContainer : Colors.Transparent, s.OnSurface, _highlight == index ? 0.10f : StateLayer.Opacity(st)),
                    child: DefaultTextStyle.Merge(ctx, theme.TextTheme.BodyLarge.Merge(new TextStyle(Color: item.Enabled ? s.OnSurface : s.OnSurface.WithOpacity(0.38f))), item.Child)),
                item.Enabled ? () => Widget.OnSelect(item.Value) : null, focusable: false)));
        }

        return new Focus(autofocus: true, onKey: OnKey, child: new Container(
            height: Widget.Height,
            padding: EdgeInsets.Symmetric(vertical: 8),
            decoration: new BoxDecoration(Color: s.SurfaceContainer, BorderRadius: BorderRadius.Circular(theme.Shape.ExtraSmall),
                BoxShadow: theme.Shape.Shadows(2, s.Shadow)),
            child: new ClipRRect(BorderRadius.Circular(theme.Shape.ExtraSmall), new SingleChildScrollView(
                new Column(rows, mainAxisSize: MainAxisSize.Min, crossAxisAlignment: CrossAxisAlignment.Stretch), controller: _scroll))));
    }
}
