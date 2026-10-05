using SkiaSharp;

namespace Sway.Widgets;

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
