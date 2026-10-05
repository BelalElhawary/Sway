using SkiaSharp;
using Sway.Widgets;

namespace Sway.Extras.Ibm;

/// <summary>Carbon's field heights in logical pixels.</summary>
public enum CarbonFieldSize { Small = 32, Medium = 40, Large = 48 }

/// <summary>The shared look of Carbon text fields: a filled box with a strong bottom border and a 2px focus or error outline.</summary>
static class CarbonField
{
    public static Widget Frame(CarbonThemeData theme, float height, bool focused, bool hovered, bool error, bool enabled, SKColor? fill, Widget content)
    {
        var c = theme.Colors;
        var back = !enabled ? c.Field01 : hovered ? c.FieldHover01 : fill ?? c.Field01;
        var border = error ? c.SupportError : !enabled ? c.BorderDisabled : c.BorderStrong01;
        Widget box = new Container(height: height, color: back, child: new Column(crossAxisAlignment: CrossAxisAlignment.Stretch, children:
        [
            new Expanded(content),
            new Container(height: 1, color: border),
        ]));
        return CarbonFocus.Around(focused || error, theme, box, error && !focused ? c.SupportError : c.Focus);
    }
}

/// <summary>
/// A Carbon text input: a label above a filled field, with helper or error text below. Typing and selection come from the
/// core <see cref="EditableText"/>; this only draws Carbon around it.
/// </summary>
public sealed class CarbonTextInput(TextEditingController? controller = null, FocusNode? focusNode = null, string? label = null,
    string? placeholder = null, string? helperText = null, string? errorText = null, CarbonFieldSize size = CarbonFieldSize.Medium,
    bool obscureText = false, bool readOnly = false, bool enabled = true, Action<string>? onChanged = null, Action<string>? onSubmitted = null,
    bool autofocus = false, bool onLayer = false, Key? key = null) : StatefulWidget(key)
{
    internal bool OnLayer => onLayer;
    internal TextEditingController? Controller => controller;
    internal FocusNode? FocusNode => focusNode;
    internal string? Label => label;
    internal string? Placeholder => placeholder;
    internal string? HelperText => helperText;
    internal string? ErrorText => errorText;
    internal CarbonFieldSize Size => size;
    internal bool Obscure => obscureText;
    internal bool ReadOnly => readOnly;
    internal bool Enabled => enabled;
    internal Action<string>? OnChanged => onChanged;
    internal Action<string>? OnSubmitted => onSubmitted;
    internal bool Autofocus => autofocus;
    public override State CreateState() => new CarbonTextInputState();
}

sealed class CarbonTextInputState : State<CarbonTextInput>
{
    TextEditingController? _ownedController;
    FocusNode? _ownedNode;
    bool _hover;

    TextEditingController Controller => Widget.Controller ?? (_ownedController ??= new TextEditingController());
    FocusNode Node => Widget.FocusNode ?? (_ownedNode ??= new FocusNode { DebugLabel = "CarbonTextInput" });

    public override void InitState()
    {
        Node.Changed += OnChange;
        if (Widget.Autofocus)
            WidgetsBinding.Instance.ScheduleFrameCallback(_ => { if (Mounted) Node.RequestFocus(); });
    }

    void OnChange() { if (Mounted) SetState(); }
    public override void Dispose() => Node.Changed -= OnChange;

    public override Widget Build(BuildContext context)
    {
        var theme = CarbonTheme.Of(context);
        var c = theme.Colors;
        bool error = Widget.ErrorText is not null, enabled = Widget.Enabled;
        var text = theme.Type.BodyCompact01.Merge(new TextStyle(Color: enabled ? c.TextPrimary : c.TextDisabled));

        Widget field = new EditableText(Controller, Node, text, text.Merge(new TextStyle(Color: c.TextPlaceholder)), Widget.Placeholder,
            Widget.Obscure, 1, null, Widget.ReadOnly || !enabled, c.TextPrimary, c.Highlight, Widget.OnChanged, Widget.OnSubmitted);

        Widget frame = CarbonField.Frame(theme, (float)Widget.Size, Node.HasFocus, _hover && enabled, error, enabled, Widget.OnLayer ? c.Field02 : null,
            new GestureDetector(onTap: enabled ? () => Node.RequestFocus() : null, behavior: HitTestBehavior.Opaque,
                child: new Padding(EdgeInsets.Symmetric(horizontal: 16), new Align(AlignmentDirectional.CenterStart, field))));

        return new MouseRegion(onEnter: _ => SetState(() => _hover = true), onExit: _ => SetState(() => _hover = false), opaque: false,
            child: new Column(crossAxisAlignment: CrossAxisAlignment.Stretch, mainAxisSize: MainAxisSize.Min, spacing: 8, children:
            [
                ..Widget.Label is null ? Array.Empty<Widget>() :
                [new Text(Widget.Label, style: theme.Type.Label01.Merge(new TextStyle(Color: enabled ? c.TextSecondary : c.TextDisabled)))],
                frame,
                ..Widget.ErrorText is { } e ? [new Text(e, style: theme.Type.HelperText01.Merge(new TextStyle(Color: c.TextError)))]
                    : Widget.HelperText is { } h ? [new Text(h, style: theme.Type.HelperText01.Merge(new TextStyle(Color: c.TextHelper)))]
                    : Array.Empty<Widget>(),
            ]));
    }
}

/// <summary>A Carbon search field: a magnifier, the text, and a clear button once there is something to clear.</summary>
public sealed class CarbonSearch(TextEditingController? controller = null, FocusNode? focusNode = null, string placeholder = "Search",
    CarbonFieldSize size = CarbonFieldSize.Medium, bool onLayer = false, Action<string>? onChanged = null, Action<string>? onSubmitted = null,
    Key? key = null) : StatefulWidget(key)
{
    internal TextEditingController? Controller => controller;
    internal FocusNode? FocusNode => focusNode;
    internal string Placeholder => placeholder;
    internal CarbonFieldSize Size => size;
    internal bool OnLayer => onLayer;
    internal Action<string>? OnChanged => onChanged;
    internal Action<string>? OnSubmitted => onSubmitted;
    public override State CreateState() => new CarbonSearchState();
}

sealed class CarbonSearchState : State<CarbonSearch>
{
    TextEditingController? _ownedController;
    FocusNode? _ownedNode;
    bool _hover;

    TextEditingController Controller => Widget.Controller ?? (_ownedController ??= new TextEditingController());
    FocusNode Node => Widget.FocusNode ?? (_ownedNode ??= new FocusNode { DebugLabel = "CarbonSearch" });

    public override void InitState()
    {
        Node.Changed += OnChange;
        Controller.Changed += OnChange;
    }

    void OnChange() { if (Mounted) SetState(); }

    public override void Dispose()
    {
        Node.Changed -= OnChange;
        Controller.Changed -= OnChange;
    }

    void Clear()
    {
        Controller.Text = "";
        Widget.OnChanged?.Invoke("");
        Node.RequestFocus();
    }

    public override Widget Build(BuildContext context)
    {
        var theme = CarbonTheme.Of(context);
        var c = theme.Colors;
        var text = theme.Type.BodyCompact01.Merge(new TextStyle(Color: c.TextPrimary));
        float height = (float)Widget.Size;

        Widget field = new EditableText(Controller, Node, text, text.Merge(new TextStyle(Color: c.TextPlaceholder)), Widget.Placeholder,
            false, 1, null, false, c.TextPrimary, c.Highlight, Widget.OnChanged, Widget.OnSubmitted);

        Widget content = new GestureDetector(onTap: () => Node.RequestFocus(), behavior: HitTestBehavior.Opaque, child: new Row(
            crossAxisAlignment: CrossAxisAlignment.Center, children:
        [
            new SizedBox(width: height, height: height, child: new Center(new Icon(Icons.Search, 16, c.IconSecondary))),
            new Expanded(new Align(AlignmentDirectional.CenterStart, field)),
            ..Controller.Text.Length > 0
                ? [new GestureDetector(onTap: Clear, behavior: HitTestBehavior.Opaque,
                    child: new SizedBox(width: height, height: height, child: new Center(new Icon(Icons.Close, 16, c.IconPrimary))))]
                : Array.Empty<Widget>(),
        ]));

        return new MouseRegion(onEnter: _ => SetState(() => _hover = true), onExit: _ => SetState(() => _hover = false), opaque: false,
            child: CarbonField.Frame(theme, height, Node.HasFocus, _hover, false, true, Widget.OnLayer ? c.Field02 : null, content));
    }
}
