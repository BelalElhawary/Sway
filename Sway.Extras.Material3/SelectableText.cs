using Sway.Widgets;

namespace Sway.Extras.Material3;

/// <summary>Read-only text the user can select with the mouse or keyboard and copy.</summary>
public sealed class SelectableText(string text, TextStyle? style = null, Key? key = null) : StatefulWidget(key)
{
    internal string Text => text;
    internal TextStyle? Style => style;
    public override State CreateState() => new SelectableTextState();
}

sealed class SelectableTextState : State<SelectableText>
{
    readonly TextEditingController _controller = new();

    public override void InitState() => _controller.Text = Widget.Text;

    public override void DidUpdateWidget(SelectableText old)
    {
        if (old.Text != Widget.Text) _controller.Text = Widget.Text;
    }

    public override Widget Build(BuildContext context)
    {
        var theme = Theme.Of(context);
        var style = theme.TextTheme.BodyMedium.Merge(new TextStyle(Color: theme.ColorScheme.OnSurface)).Merge(Widget.Style ?? new TextStyle());
        return new EditableText(_controller, style: style, maxLines: null, readOnly: true,
            cursorColor: theme.ColorScheme.Primary, selectionColor: theme.ColorScheme.Primary.WithOpacity(0.35f));
    }
}
