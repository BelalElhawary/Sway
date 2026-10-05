using Sway.Widgets;

namespace Sway.Extras.Material3;

/// <summary>A Material 3 search bar: a rounded surface with a search icon, the text input and an optional trailing widget.</summary>
public sealed class SearchBar(TextEditingController? controller = null, string? hintText = null, Action<string>? onChanged = null,
    Action<string>? onSubmitted = null, Widget? trailing = null, Key? key = null) : StatefulWidget(key)
{
    internal TextEditingController? Controller => controller;
    internal string? Hint => hintText;
    internal Action<string>? OnChanged => onChanged;
    internal Action<string>? OnSubmitted => onSubmitted;
    internal Widget? Trailing => trailing;
    public override State CreateState() => new SearchBarState();
}

sealed class SearchBarState : State<SearchBar>
{
    TextEditingController? _owned;
    TextEditingController Controller => Widget.Controller ?? (_owned ??= new TextEditingController());

    public override Widget Build(BuildContext context)
    {
        var theme = Theme.Of(context);
        var s = theme.ColorScheme;
        var hint = Widget.Hint ?? MaterialLocalizations.Of(context).SearchHint;
        return new DecoratedBox(new BoxDecoration(Color: s.SurfaceContainerHigh, BorderRadius: BorderRadius.Circular(28),
                BoxShadow: Elevation.Shadows(1, s.Shadow)),
            new ConstrainedBox(new BoxConstraints(360, 720, 56, 56), new Padding(EdgeInsets.Symmetric(horizontal: 16),
                new Row(spacing: 16, children:
                [
                    new Icon(Icons.Search, 24, s.OnSurface),
                    new Expanded(new EditableText(Controller, style: theme.TextTheme.BodyLarge.Merge(new TextStyle(Color: s.OnSurface)),
                        hintStyle: theme.TextTheme.BodyLarge.Merge(new TextStyle(Color: s.OnSurfaceVariant)), hintText: hint,
                        cursorColor: s.Primary, selectionColor: s.Primary.WithOpacity(0.35f),
                        onChanged: Widget.OnChanged, onSubmitted: Widget.OnSubmitted)),
                    ..Widget.Trailing is null ? Array.Empty<Widget>() : [Widget.Trailing],
                ]))));
    }
}
