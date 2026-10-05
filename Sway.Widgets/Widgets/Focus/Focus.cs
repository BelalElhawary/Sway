namespace Sway.Widgets;

/// <summary>Makes a subtree focusable and exposes its <see cref="FocusNode"/> to descendants.</summary>
public sealed class Focus(Widget child, FocusNode? focusNode = null, bool autofocus = false, Func<KeyEvent, bool>? onKey = null,
    Action<bool>? onFocusChange = null, bool canRequestFocus = true, bool skipTraversal = false, bool trapFocus = false, Key? key = null) : StatefulWidget(key)
{
    internal Widget Child => child;
    internal FocusNode? Node => focusNode;
    internal bool Autofocus => autofocus;
    internal Func<KeyEvent, bool>? OnKey => onKey;
    internal Action<bool>? OnFocusChange => onFocusChange;
    internal bool CanRequestFocus => canRequestFocus;
    internal bool SkipTraversal => skipTraversal;
    internal bool TrapFocus => trapFocus;

    public override State CreateState() => new FocusState();

    /// <summary>The nearest enclosing focus node (does not rebuild on focus changes).</summary>
    public static FocusNode? MaybeOf(BuildContext context) => context.Get<FocusScopeMarker>()?.Node;

    /// <summary>True if the nearest enclosing Focus (or a descendant of it) has focus; rebuilds when that changes.</summary>
    public static bool IsFocused(BuildContext context) => context.DependOn<FocusScopeMarker>()?.HasFocus ?? false;
}
