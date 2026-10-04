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

// ---- icon painters ----

sealed class CheckPainter(SKColor color, float strokeWidth = 2) : CustomPainter
{
    public override void Paint(SKCanvas canvas, Size size)
    {
        using var p = new SKPaint { Color = color, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = strokeWidth,
            StrokeCap = SKStrokeCap.Round, StrokeJoin = SKStrokeJoin.Round };
        using var path = new SKPath();
        path.MoveTo(size.Width * 0.22f, size.Height * 0.52f);
        path.LineTo(size.Width * 0.43f, size.Height * 0.72f);
        path.LineTo(size.Width * 0.78f, size.Height * 0.30f);
        canvas.DrawPath(path, p);
    }
    public override bool ShouldRepaint(CustomPainter old) => old is not CheckPainter c || c.GetHashCode() != GetHashCode();
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
    public static BoxDecoration Ring(bool visible, BorderRadius radius) =>
        new(Border: visible ? Border.All(Palette.Primary.WithOpacity(0.45f), 3) : null, BorderRadius: radius);

    /// <summary>Draws a translucent focus outline around <paramref name="child"/> without affecting layout.</summary>
    public static Widget Around(bool visible, BorderRadius radius, Widget child, float inset = -3) =>
        visible
            ? new Stack([child, Positioned.Fill(new IgnorePointer(new DecoratedBox(Ring(true, radius))), inset, inset, inset, inset)], clip: false)
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

// ---- checkbox / radio / switch ----

public sealed class Checkbox(bool value, Action<bool>? onChanged = null, SKColor? activeColor = null, Key? key = null) : StatelessWidget(key)
{
    static readonly TimeSpan Quick = TimeSpan.FromMilliseconds(120);

    public override Widget Build(BuildContext context)
    {
        var active = activeColor ?? Palette.Primary;
        var radius = BorderRadius.Circular(4);
        return new Interactive((ctx, s) =>
        {
            var fill = value ? active : s.Hover ? Colors.FromRgb(0xF3F4F6) : Colors.White;
            var border = value ? active : onChanged is null ? Palette.Border : Colors.Grey;
            Widget box = new AnimatedContainer(Quick, width: 20, height: 20,
                decoration: new BoxDecoration(Color: onChanged is null && !value ? Palette.Disabled : fill, BorderRadius: radius, Border: Border.All(border, 2)),
                child: value ? new CustomPaint(new CheckPainter(Colors.White), size: new Size(16, 16)) : null);
            return FocusRing.Around(s.FocusVisible, radius, box);
        }, onChanged is null ? null : () => onChanged(!value));
    }
}

public sealed class Radio<T>(T value, T? groupValue, Action<T>? onChanged = null, SKColor? activeColor = null, Key? key = null) : StatelessWidget(key)
    where T : notnull
{
    public override Widget Build(BuildContext context)
    {
        bool selected = EqualityComparer<T>.Default.Equals(value, groupValue);
        var active = activeColor ?? Palette.Primary;
        return new Interactive((ctx, s) =>
        {
            Widget dot = new AnimatedContainer(TimeSpan.FromMilliseconds(120), width: 20, height: 20,
                decoration: new BoxDecoration(Color: s.Hover ? Colors.FromRgb(0xF3F4F6) : Colors.White, Shape: BoxShape.Circle,
                    Border: Border.All(selected ? active : Colors.Grey, 2)),
                alignment: Alignment.Center,
                child: selected ? new Container(width: 10, height: 10, decoration: new BoxDecoration(Color: active, Shape: BoxShape.Circle)) : null);
            return FocusRing.Around(s.FocusVisible, BorderRadius.Circular(100), dot);
        }, onChanged is null ? null : () => onChanged(value));
    }
}

public sealed class Switch(bool value, Action<bool>? onChanged = null, SKColor? activeColor = null, Key? key = null) : StatelessWidget(key)
{
    public override Widget Build(BuildContext context)
    {
        var active = activeColor ?? Palette.Primary;
        return new Interactive((ctx, s) =>
        {
            var track = value ? active : s.Hover ? Colors.FromRgb(0xB8BEC8) : Colors.FromRgb(0xCBD0D8);
            Widget sw = new AnimatedContainer(TimeSpan.FromMilliseconds(160), width: 42, height: 24,
                padding: EdgeInsets.All(2),
                decoration: new BoxDecoration(Color: onChanged is null ? track.WithOpacity(0.5f) : track, BorderRadius: BorderRadius.Circular(12)),
                child: new AnimatedAlign(value ? Alignment.CenterRight : Alignment.CenterLeft, TimeSpan.FromMilliseconds(160),
                    new Container(width: 20, height: 20, decoration: new BoxDecoration(Color: Colors.White, Shape: BoxShape.Circle,
                        BoxShadow: [new BoxShadow(Colors.Black.WithOpacity(0.25f), new Offset(0, 1), 3)])), Curves.EaseOut));
            return FocusRing.Around(s.FocusVisible, BorderRadius.Circular(12), sw);
        }, onChanged is null ? null : () => onChanged(!value));
    }
}

// ---- buttons ----

public enum ButtonVariant { Elevated, Outlined, Text }

public sealed class Button(Widget child, Action? onPressed = null, ButtonVariant variant = ButtonVariant.Elevated,
    SKColor? color = null, EdgeInsets? padding = null, Key? key = null) : StatelessWidget(key)
{
    public override Widget Build(BuildContext context)
    {
        var main = color ?? Palette.Primary;
        var radius = BorderRadius.Circular(8);
        bool enabled = onPressed is not null;
        return new Interactive((ctx, s) =>
        {
            SKColor bg, fg;
            Border? border = null;
            switch (variant)
            {
                case ButtonVariant.Elevated:
                    bg = !enabled ? Palette.Border : s.Pressed ? Darken(main, 0.2f) : s.Hover ? Darken(main, 0.1f) : main;
                    fg = enabled ? Colors.White : Palette.Hint;
                    break;
                case ButtonVariant.Outlined:
                    bg = s.Pressed ? main.WithOpacity(0.16f) : s.Hover ? main.WithOpacity(0.08f) : Colors.Transparent;
                    fg = enabled ? main : Palette.Hint;
                    border = Border.All(enabled ? main : Palette.Border, 1);
                    break;
                default:
                    bg = s.Pressed ? main.WithOpacity(0.16f) : s.Hover ? main.WithOpacity(0.08f) : Colors.Transparent;
                    fg = enabled ? main : Palette.Hint;
                    break;
            }
            Widget body = new AnimatedContainer(TimeSpan.FromMilliseconds(120),
                padding: padding ?? EdgeInsets.Symmetric(18, 11),
                decoration: new BoxDecoration(Color: bg, BorderRadius: radius, Border: border,
                    BoxShadow: variant == ButtonVariant.Elevated && enabled && !s.Pressed
                        ? [new BoxShadow(Colors.Black.WithOpacity(s.Hover ? 0.28f : 0.18f), new Offset(0, s.Hover ? 3 : 1), s.Hover ? 8 : 3)] : null),
                child: new Center(DefaultTextStyle.Merge(ctx, new TextStyle(Color: fg, FontWeight: FontWeight.W600), child), 1, 1));
            return FocusRing.Around(s.FocusVisible, radius, body);
        }, onPressed);
    }

    static SKColor Darken(SKColor c, float amount) =>
        new((byte)(c.Red * (1 - amount)), (byte)(c.Green * (1 - amount)), (byte)(c.Blue * (1 - amount)), c.Alpha);
}

// ---- dropdown ----

public sealed record DropdownMenuItem<T>(T Value, Widget Child, bool Enabled = true);

public sealed class DropdownButton<T>(IReadOnlyList<DropdownMenuItem<T>> items, T? value = default, Action<T>? onChanged = null,
    Widget? hint = null, double menuMaxHeight = 240, Key? key = null) : StatefulWidget(key) where T : notnull
{
    internal IReadOnlyList<DropdownMenuItem<T>> Items => items;
    internal T? Value => value;
    internal Action<T>? OnChanged => onChanged;
    internal Widget? Hint => hint;
    internal float MenuMaxHeight => (float)menuMaxHeight;
    public override State CreateState() => new DropdownButtonState<T>();
}

sealed class DropdownButtonState<T> : State<DropdownButton<T>> where T : notnull
{
    OverlayEntry? _entry;
    readonly FocusNode _node = new() { DebugLabel = "Dropdown" };

    public override void Dispose() => Close(false);

    void Close(bool refocus = true)
    {
        _entry?.Remove();
        _entry = null;
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
        float menuHeight = Math.Min(Widget.MenuMaxHeight, Widget.Items.Count * 40f + 8);
        bool above = origin.Dy + size.Height + 4 + menuHeight > window.Height && origin.Dy - 4 - menuHeight > 0;
        float top = above ? origin.Dy - 4 - menuHeight : origin.Dy + size.Height + 4;

        _entry = new OverlayEntry(ctx => new Stack([
            Positioned.Fill(new GestureDetector(onTap: () => Close(), behavior: HitTestBehavior.Opaque)),
            new Positioned(new DropdownMenu<T>(Widget.Items, Widget.Value, v => { Close(); Widget.OnChanged?.Invoke(v); }, () => Close(), menuHeight),
                left: origin.Dx, top: top, width: size.Width),
        ], fit: StackFit.Expand, clip: false));
        overlay.Insert(_entry);
    }

    public override Widget Build(BuildContext context)
    {
        var selected = Widget.Items.FirstOrDefault(i => EqualityComparer<T>.Default.Equals(i.Value, Widget.Value));
        bool enabled = Widget.OnChanged is not null;
        var radius = BorderRadius.Circular(8);

        return new Interactive((ctx, s) =>
        {
            Widget label = selected is not null ? selected.Child : Widget.Hint is { } h
                ? DefaultTextStyle.Merge(ctx, new TextStyle(Color: Palette.Hint), h) : new SizedBox();
            Widget body = new Container(
                padding: EdgeInsets.Symmetric(12, 10),
                decoration: new BoxDecoration(Color: enabled ? Colors.White : Palette.Disabled, BorderRadius: radius,
                    Border: Border.All(_entry is not null || s.FocusVisible ? Palette.Primary : s.Hover ? Colors.Grey : Palette.Border, _entry is not null || s.FocusVisible ? 2 : 1)),
                child: new Row(children: [
                    new Expanded(label),
                    new CustomPaint(new ChevronPainter(Colors.Grey, _entry is not null), size: new Size(18, 18)),
                ]));
            return body;
        }, enabled ? () => { if (_entry is null) Open(); else Close(); } : null, focusNode: _node);
    }
}

sealed class DropdownMenu<T>(IReadOnlyList<DropdownMenuItem<T>> items, T? value, Action<T> onSelect, Action onClose, float height) : StatefulWidget
    where T : notnull
{
    internal IReadOnlyList<DropdownMenuItem<T>> Items => items;
    internal T? Value => value;
    internal Action<T> OnSelect => onSelect;
    internal Action OnClose => onClose;
    internal float Height => height;
    public override State CreateState() => new DropdownMenuState<T>();
}

sealed class DropdownMenuState<T> : State<DropdownMenu<T>> where T : notnull
{
    int _highlight = -1;
    readonly ScrollController _scroll = new();
    const float ItemHeight = 40;

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
        float top = 4 + index * ItemHeight, pos = _scroll.Offset;
        if (top < pos) _scroll.JumpTo(top - 4);
        else if (top + ItemHeight > pos + Widget.Height - 4) _scroll.JumpTo(top + ItemHeight - Widget.Height + 4);
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
        var rows = new List<Widget>();
        for (int i = 0; i < Widget.Items.Count; i++)
        {
            int index = i;
            var item = Widget.Items[i];
            bool selected = EqualityComparer<T>.Default.Equals(item.Value, Widget.Value);
            rows.Add(new SizedBox(height: ItemHeight, child: new Interactive((ctx, s) => new Container(
                    padding: EdgeInsets.Symmetric(12, 0),
                    alignment: Alignment.CenterLeft,
                    color: _highlight == index || s.Hover ? Palette.Primary.WithOpacity(0.10f) : Colors.Transparent,
                    child: DefaultTextStyle.Merge(ctx, new TextStyle(
                        Color: item.Enabled ? Palette.Text : Palette.Hint, FontWeight: selected ? FontWeight.W600 : FontWeight.Normal), item.Child)),
                item.Enabled ? () => Widget.OnSelect(item.Value) : null, focusable: false, cursor: MouseCursor.Click)));
        }

        return new Focus(autofocus: true, onKey: OnKey, child: new Container(
            decoration: new BoxDecoration(Color: Colors.White, BorderRadius: BorderRadius.Circular(8), Border: Border.All(Palette.Border),
                BoxShadow: [new BoxShadow(Colors.Black.WithOpacity(0.18f), new Offset(0, 6), 16)]),
            padding: EdgeInsets.Symmetric(vertical: 4),
            height: Widget.Height,
            child: new ClipRRect(BorderRadius.Circular(6), new SingleChildScrollView(
                new Column(rows, mainAxisSize: MainAxisSize.Min, crossAxisAlignment: CrossAxisAlignment.Stretch), controller: _scroll))));
    }
}
