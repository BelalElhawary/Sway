using SkiaSharp;

namespace Sway.Widgets;

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
