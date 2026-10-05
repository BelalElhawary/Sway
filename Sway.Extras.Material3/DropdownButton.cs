using Sway.Widgets;

namespace Sway.Extras.Material3;

public sealed class DropdownButton<T>(IReadOnlyList<DropdownMenuItem<T>> items, T? value = default, Action<T>? onChanged = null,
    Widget? hint = null, string? label = null, double menuMaxHeight = 280, Key? key = null) : StatefulWidget(key) where T : notnull
{
    internal IReadOnlyList<DropdownMenuItem<T>> Items => items;
    internal T? Value => value;
    internal Action<T>? OnChanged => onChanged;
    internal Widget? Hint => hint;
    internal string? Label => label;
    internal float MenuMaxHeight => (float)menuMaxHeight;
    public override State CreateState() => new DropdownButtonState<T>();
}

sealed class DropdownButtonState<T> : State<DropdownButton<T>> where T : notnull
{
    const float ItemHeight = 48;
    OverlayEntry? _entry;
    readonly FocusNode _node = new() { DebugLabel = "Dropdown" };

    public override void Dispose() => Close(false);

    void Close(bool refocus = true)
    {
        _entry?.Remove();
        _entry = null;
        if (Mounted) SetState();
        if (refocus && Mounted) _node.RequestFocus();
    }

    void Open()
    {
        if (_entry is not null || Context.FindRenderObject() is not RenderBox box) return;
        var overlay = Overlay.MaybeOf(Context);
        if (overlay is null) return;

        var origin = box.LocalToGlobal(Offset.Zero);
        var size = box.Size;
        var window = WidgetsBinding.Instance.RenderView.WindowSize;
        float menuHeight = Math.Min(Widget.MenuMaxHeight, Widget.Items.Count * ItemHeight + 16);
        bool above = origin.Dy + size.Height + 4 + menuHeight > window.Height && origin.Dy - 4 - menuHeight > 0;
        float top = above ? origin.Dy - 4 - menuHeight : origin.Dy + size.Height + 4;
        var origin2 = Context;

        _entry = new OverlayEntry(ctx => Dialogs.Wrap(origin2, new Stack([
            Positioned.Fill(new GestureDetector(onTap: () => Close(), behavior: HitTestBehavior.Opaque)),
            new Positioned(new DropdownMenu<T>(Widget.Items, Widget.Value, v => { Close(); Widget.OnChanged?.Invoke(v); }, () => Close(), menuHeight, ItemHeight),
                left: origin.Dx, top: top, width: size.Width),
        ], fit: StackFit.Expand, clip: false)));
        overlay.Insert(_entry);
        SetState();
    }

    public override Widget Build(BuildContext context)
    {
        var theme = Theme.Of(context);
        var s = theme.ColorScheme;
        var selected = Widget.Items.FirstOrDefault(i => EqualityComparer<T>.Default.Equals(i.Value, Widget.Value));
        bool enabled = Widget.OnChanged is not null;
        bool open = _entry is not null;

        return new Interactive((ctx, st) =>
        {
            Widget content = selected is not null
                ? DefaultTextStyle.Merge(ctx, theme.TextTheme.BodyLarge.Merge(new TextStyle(Color: s.OnSurface)), selected.Child)
                : Widget.Hint is { } h ? DefaultTextStyle.Merge(ctx, theme.TextTheme.BodyLarge.Merge(new TextStyle(Color: s.OnSurfaceVariant)), h) : new SizedBox(height: 24);
            return new InputDecorator(
                new InputDecoration(LabelText: Widget.Label), focused: open || st.FocusVisible, hovered: st.Hover, hasContent: selected is not null || Widget.Hint is not null && Widget.Label is null,
                enabled: enabled,
                child: new Row(children: [new Expanded(content), new Icon(open ? Icons.ExpandLess : Icons.ExpandMore, 24, enabled ? s.OnSurfaceVariant : s.OnSurface.WithOpacity(0.38f))]));
        }, enabled ? () => { if (_entry is null) Open(); else Close(); } : null, focusNode: _node);
    }
}
