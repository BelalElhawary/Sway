using SkiaSharp;
using Sway.Widgets;

namespace Sway.Extras.Shopify;

/// <summary>The shared look of text fields: an 8px-rounded box with a hairline that turns into a 2px ink outline on focus or a critical outline on error.</summary>
static class ShopifyField
{
    public const float Height = 48;

    public static Widget Frame(ShopifyThemeData theme, bool focused, bool error, bool enabled, Widget content)
    {
        var c = theme.Colors;
        var radius = BorderRadius.Circular(8);
        Widget box = new Container(height: Height, decoration: new BoxDecoration(Color: enabled ? c.Surface : c.Disabled, BorderRadius: radius,
            Border: Border.All(c.Hairline)), child: content);
        // The 2px focus or error outline is a separate layer over the hairline, so gaining focus never shifts the content.
        return new Stack([box, Positioned.Fill(new IgnorePointer(new DecoratedBox(new BoxDecoration(
            Border: Border.All(error ? c.Critical : focused ? c.Focus : Colors.Transparent, 2), BorderRadius: radius))))], clip: false);
    }

    public static Widget Caption(ShopifyThemeData theme, string? label, bool enabled, Widget frame, string? helper, string? error)
    {
        var c = theme.Colors;
        return new Column(crossAxisAlignment: CrossAxisAlignment.Stretch, mainAxisSize: MainAxisSize.Min, spacing: 6, children:
        [
            ..label is null ? Array.Empty<Widget>() : [new Text(label, style: theme.Type.Caption.Merge(new TextStyle(Color: enabled ? c.Ink : c.OnDisabled)))],
            frame,
            ..error is not null ? [new Text(error, style: theme.Type.Micro.Merge(new TextStyle(Color: c.Critical)))]
                : helper is not null ? [new Text(helper, style: theme.Type.Micro.Merge(new TextStyle(Color: c.InkSecondary)))]
                : Array.Empty<Widget>(),
        ]);
    }
}

/// <summary>
/// A text field: a label above a 48px box, with helper or error text below. Typing and selection come from the core
/// <see cref="EditableText"/>. Tapping anywhere in the box focuses it, which also raises the soft keyboard on phones.
/// </summary>
public sealed class ShopifyTextField(TextEditingController? controller = null, FocusNode? focusNode = null, string? label = null,
    string? placeholder = null, string? helperText = null, string? errorText = null, IconData? leadingIcon = null, bool obscureText = false,
    bool readOnly = false, bool enabled = true, Action<string>? onChanged = null, Action<string>? onSubmitted = null,
    bool autofocus = false, Key? key = null) : StatefulWidget(key)
{
    internal TextEditingController? Controller => controller;
    internal FocusNode? FocusNode => focusNode;
    internal string? Label => label;
    internal string? Placeholder => placeholder;
    internal string? HelperText => helperText;
    internal string? ErrorText => errorText;
    internal IconData? LeadingIcon => leadingIcon;
    internal bool Obscure => obscureText;
    internal bool ReadOnly => readOnly;
    internal bool Enabled => enabled;
    internal Action<string>? OnChanged => onChanged;
    internal Action<string>? OnSubmitted => onSubmitted;
    internal bool Autofocus => autofocus;
    public override State CreateState() => new ShopifyTextFieldState();
}

sealed class ShopifyTextFieldState : State<ShopifyTextField>
{
    TextEditingController? _ownedController;
    FocusNode? _ownedNode;

    TextEditingController Controller => Widget.Controller ?? (_ownedController ??= new TextEditingController());
    FocusNode Node => Widget.FocusNode ?? (_ownedNode ??= new FocusNode { DebugLabel = "ShopifyTextField" });

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
        var theme = ShopifyTheme.Of(context);
        var c = theme.Colors;
        bool enabled = Widget.Enabled;
        var text = theme.Type.BodyMd.Merge(new TextStyle(Color: enabled ? c.Ink : c.OnDisabled));

        Widget field = new EditableText(Controller, Node, text, text.Merge(new TextStyle(Color: c.InkTertiary)), Widget.Placeholder,
            Widget.Obscure, 1, null, Widget.ReadOnly || !enabled, c.Ink, c.Aloe, Widget.OnChanged, Widget.OnSubmitted);

        Widget frame = ShopifyField.Frame(theme, Node.HasFocus, Widget.ErrorText is not null, enabled,
            new GestureDetector(onTap: enabled ? () => Node.RequestFocus() : null, behavior: HitTestBehavior.Opaque, child: new Row(
                crossAxisAlignment: CrossAxisAlignment.Center, spacing: 8, children:
            [
                new SizedBox(width: 12),
                ..Widget.LeadingIcon is { } icon ? [new Icon(icon, 20, c.InkSecondary)] : Array.Empty<Widget>(),
                new Expanded(new Align(AlignmentDirectional.CenterStart, field)),
                new SizedBox(width: 4),
            ])));
        return ShopifyField.Caption(theme, Widget.Label, enabled, frame, Widget.HelperText, Widget.ErrorText);
    }
}

/// <summary>A search field with a magnifier and a clear button once there is text to clear. The clear target is a full 44px square.</summary>
public sealed class ShopifySearchField(TextEditingController? controller = null, FocusNode? focusNode = null, string placeholder = "Search",
    Action<string>? onChanged = null, Action<string>? onSubmitted = null, Key? key = null) : StatefulWidget(key)
{
    internal TextEditingController? Controller => controller;
    internal FocusNode? FocusNode => focusNode;
    internal string Placeholder => placeholder;
    internal Action<string>? OnChanged => onChanged;
    internal Action<string>? OnSubmitted => onSubmitted;
    public override State CreateState() => new ShopifySearchFieldState();
}

sealed class ShopifySearchFieldState : State<ShopifySearchField>
{
    TextEditingController? _ownedController;
    FocusNode? _ownedNode;

    TextEditingController Controller => Widget.Controller ?? (_ownedController ??= new TextEditingController());
    FocusNode Node => Widget.FocusNode ?? (_ownedNode ??= new FocusNode { DebugLabel = "ShopifySearchField" });

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
        var theme = ShopifyTheme.Of(context);
        var c = theme.Colors;
        var text = theme.Type.BodyMd.Merge(new TextStyle(Color: c.Ink));
        Widget field = new EditableText(Controller, Node, text, text.Merge(new TextStyle(Color: c.InkTertiary)), Widget.Placeholder,
            false, 1, null, false, c.Ink, c.Aloe, Widget.OnChanged, Widget.OnSubmitted);

        // A pill, like the rest of the controls, so a search field reads as an action rather than a form input.
        var radius = BorderRadius.Circular(ShopifyField.Height / 2);
        Widget content = new GestureDetector(onTap: () => Node.RequestFocus(), behavior: HitTestBehavior.Opaque, child: new Row(
            crossAxisAlignment: CrossAxisAlignment.Center, children:
        [
            new SizedBox(width: 16),
            new Icon(Icons.Search, 20, c.InkSecondary),
            new SizedBox(width: 8),
            new Expanded(new Align(AlignmentDirectional.CenterStart, field)),
            ..Controller.Text.Length > 0
                ? [new GestureDetector(onTap: Clear, behavior: HitTestBehavior.Opaque,
                    child: new SizedBox(width: ShopifyBreakpoints.TouchTarget, height: ShopifyField.Height, child: new Center(new Icon(Icons.Close, 18, c.Ink))))]
                : new Widget[] { new SizedBox(width: 16) },
        ]));
        return new Stack([
            new Container(height: ShopifyField.Height, decoration: new BoxDecoration(Color: c.Surface, BorderRadius: radius, Border: Border.All(c.Hairline)), child: content),
            Positioned.Fill(new IgnorePointer(new DecoratedBox(new BoxDecoration(
                Border: Border.All(Node.HasFocus ? c.Focus : Colors.Transparent, 2), BorderRadius: radius)))),
        ], clip: false);
    }
}
