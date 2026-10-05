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
