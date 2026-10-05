namespace Sway.Widgets;

public sealed class FocusNode
{
    /// <summary>The element that owns this node, once it is mounted.</summary>
    public Element? Element;

    public FocusNode? Parent { get; internal set; }
    public bool CanRequestFocus { get; set; } = true;
    public bool SkipTraversal { get; set; }
    public string? DebugLabel { get; set; }

    /// <summary>While focus is inside this node, Tab and Shift+Tab cycle only through its descendants (modal dialogs, menus).</summary>
    public bool TrapsFocus { get; set; }

    /// <summary>Explicit Tab position: lower comes first, ties and unset nodes (0) fall back to tree order. See <see cref="FocusTraversalOrder"/>.</summary>
    public float Order { get; set; }

    /// <summary>Called for key events while this node or a descendant has focus; return true to consume.</summary>
    public Func<KeyEvent, bool>? OnKey { get; set; }

    /// <summary>Receives typed characters when this node holds the primary focus.</summary>
    public Action<string>? OnTextInput { get; set; }

    public event Action? Changed;

    public bool HasPrimaryFocus => WidgetsBinding.Instance.Focus.Primary == this;

    /// <summary>True if this node or any descendant has primary focus.</summary>
    public bool HasFocus
    {
        get
        {
            for (var n = WidgetsBinding.Instance.Focus.Primary; n is not null; n = n.Parent)
                if (n == this) return true;
            return false;
        }
    }

    public void RequestFocus() => WidgetsBinding.Instance.Focus.Focus(this);
    public void Unfocus() { if (HasFocus) WidgetsBinding.Instance.Focus.Focus(null); }

    internal void NotifyChanged() => Changed?.Invoke();
}
