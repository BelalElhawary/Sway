using Sway.Widgets;

namespace Sway.Extras.Material3;

/// <summary>A list tile that expands to reveal <paramref name="children"/> beneath it.</summary>
public sealed class ExpansionTile(Widget title, IReadOnlyList<Widget> children, Widget? subtitle = null, Widget? leading = null,
    bool initiallyExpanded = false, Action<bool>? onExpansionChanged = null, EdgeInsets? childrenPadding = null, Key? key = null) : StatefulWidget(key)
{
    internal Widget Title => title;
    internal IReadOnlyList<Widget> Children => children;
    internal Widget? Subtitle => subtitle;
    internal Widget? Leading => leading;
    internal bool InitiallyExpanded => initiallyExpanded;
    internal Action<bool>? OnExpansionChanged => onExpansionChanged;
    internal EdgeInsets ChildrenPadding => childrenPadding ?? EdgeInsets.Zero;
    public override State CreateState() => new ExpansionTileState();
}

sealed class ExpansionTileState : State<ExpansionTile>
{
    bool _expanded;

    public override void InitState() => _expanded = Widget.InitiallyExpanded;

    void Toggle()
    {
        SetState(() => _expanded = !_expanded);
        Widget.OnExpansionChanged?.Invoke(_expanded);
    }

    public override Widget Build(BuildContext context)
    {
        var theme = Theme.Of(context);
        var s = theme.ColorScheme;
        var ms = TimeSpan.FromMilliseconds(200);
        var titleColor = _expanded ? s.Primary : s.OnSurface;

        Widget header = new Interactive((ctx, st) => new Container(
            constraints: new BoxConstraints(0, float.PositiveInfinity, Widget.Subtitle is null ? 56 : 72, float.PositiveInfinity),
            padding: EdgeInsets.Symmetric(horizontal: 16), alignment: Alignment.CenterLeft,
            color: StateLayer.Blend(Colors.Transparent, s.OnSurface, StateLayer.Opacity(st)),
            child: new Row(spacing: 16, children:
            [
                ..Widget.Leading is null ? Array.Empty<Widget>() : [new IconTheme(s.OnSurfaceVariant, 24, Widget.Leading)],
                new Expanded(new Column(mainAxisSize: MainAxisSize.Min, crossAxisAlignment: CrossAxisAlignment.Start, children:
                [
                    DefaultTextStyle.Merge(ctx, theme.TextTheme.BodyLarge.Merge(new TextStyle(Color: titleColor)), Widget.Title),
                    ..Widget.Subtitle is null ? Array.Empty<Widget>() : [DefaultTextStyle.Merge(ctx,
                        theme.TextTheme.BodyMedium.Merge(new TextStyle(Color: s.OnSurfaceVariant)), Widget.Subtitle)],
                ])),
                new AnimatedRotation(_expanded ? 0.5f : 0, ms, new Icon(Icons.ExpandMore, 24, _expanded ? s.Primary : s.OnSurfaceVariant)),
            ])), Toggle);

        Widget body = _expanded
            ? new Padding(Widget.ChildrenPadding, new Column(mainAxisSize: MainAxisSize.Min, crossAxisAlignment: CrossAxisAlignment.Stretch, children: Widget.Children))
            : new SizedBox(height: 0);

        return new Column(mainAxisSize: MainAxisSize.Min, crossAxisAlignment: CrossAxisAlignment.Stretch, children:
        [
            header,
            new ClipRect(new AnimatedSize(ms, body, Alignment.TopCenter, Curves.EaseInOut)),
        ]);
    }
}
