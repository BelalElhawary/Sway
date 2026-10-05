using Sway.Widgets;

namespace Sway.Extras.Material3;

/// <summary>A Material 3 text input with floating label, hover/focus states and supporting text.</summary>
public sealed class TextField(TextEditingController? controller = null, FocusNode? focusNode = null, InputDecoration? decoration = null,
    TextStyle? style = null, bool obscureText = false, int? maxLines = 1, int? minLines = null, bool readOnly = false, bool enabled = true,
    Action<string>? onChanged = null, Action<string>? onSubmitted = null, bool autofocus = false, Key? key = null) : StatefulWidget(key)
{
    internal TextEditingController? Controller => controller;
    internal FocusNode? FocusNode => focusNode;
    internal InputDecoration? Decoration => decoration;
    internal TextStyle? Style => style;
    internal bool Obscure => obscureText;
    internal int? MaxLines => maxLines;
    internal int? MinLines => minLines;
    internal bool ReadOnly => readOnly;
    internal bool Enabled => enabled;
    internal Action<string>? OnChanged => onChanged;
    internal Action<string>? OnSubmitted => onSubmitted;
    internal bool Autofocus => autofocus;

    public override State CreateState() => new TextFieldState();
}

sealed class TextFieldState : State<TextField>
{
    TextEditingController? _ownedController;
    FocusNode? _ownedNode;
    bool _hover;

    TextEditingController Controller => Widget.Controller ?? (_ownedController ??= new TextEditingController());
    FocusNode Node => Widget.FocusNode ?? (_ownedNode ??= new FocusNode { DebugLabel = "TextField" });

    public override void InitState()
    {
        Node.Changed += OnChange;
        Controller.Changed += OnChange;
        if (Widget.Autofocus)
            WidgetsBinding.Instance.ScheduleFrameCallback(_ => { if (Mounted) Node.RequestFocus(); });
    }

    public override void DidUpdateWidget(TextField old)
    {
        if (!ReferenceEquals(old.Controller, Widget.Controller))
        {
            (old.Controller ?? _ownedController)!.Changed -= OnChange;
            Controller.Changed += OnChange;
        }
    }

    void OnChange() { if (Mounted) SetState(); }

    public override void Dispose()
    {
        Node.Changed -= OnChange;
        Controller.Changed -= OnChange;
    }

    public override Widget Build(BuildContext context)
    {
        var deco = Widget.Decoration ?? new InputDecoration();
        bool focused = Node.HasFocus;
        bool hasText = Controller.Text.Length > 0;
        bool showHint = deco.LabelText is null || focused;

        var theme = Theme.Of(Context);
        var scheme = theme.ColorScheme;
        var style = theme.TextTheme.BodyLarge.Merge(new TextStyle(Color: scheme.OnSurface)).Merge(Widget.Style);
        var hintStyle = style.Merge(new TextStyle(Color: scheme.OnSurfaceVariant));

        Widget field = new EditableText(Controller, Node, style, hintStyle, showHint ? deco.HintText : null, Widget.Obscure, Widget.MaxLines, Widget.MinLines,
            Widget.ReadOnly || !Widget.Enabled, scheme.Primary, scheme.Primary.WithOpacity(0.35f), Widget.OnChanged, Widget.OnSubmitted);

        return new MouseRegion(onEnter: _ => SetState(() => _hover = true), onExit: _ => SetState(() => _hover = false), opaque: false,
            child: new InputDecorator(deco, field, focused, _hover, hasText, Widget.Enabled));
    }
}
