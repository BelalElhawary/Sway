using SkiaSharp;
using Sway.Widgets;

namespace Sway.Extras.Ibm;

/// <summary>A Carbon radio button: a 16px ring with a dot when selected. Use <see cref="CarbonRadioGroup{T}"/> for a set.</summary>
public sealed class CarbonRadioButton(bool selected, Action? onSelected = null, Widget? label = null, Key? key = null) : StatelessWidget(key)
{
    public override Widget Build(BuildContext context)
    {
        var theme = CarbonTheme.Of(context);
        var c = theme.Colors;
        bool enabled = onSelected is not null;
        return new Interactive((ctx, st) =>
        {
            var edge = enabled ? c.IconPrimary : c.IconDisabled;
            Widget ring = new Container(width: 16, height: 16, decoration: new BoxDecoration(Border: Border.All(edge, 1), BorderRadius: BorderRadius.Circular(8)),
                child: selected ? new Center(new Container(width: 8, height: 8, decoration: new BoxDecoration(Color: edge, BorderRadius: BorderRadius.Circular(4)))) : null);
            // The focus outline hugs the ring.
            ring = new Stack([ring, Positioned.Fill(new IgnorePointer(new DecoratedBox(new BoxDecoration(
                Border: Border.All(st.FocusVisible ? c.Focus : Colors.Transparent, 2), BorderRadius: BorderRadius.Circular(8)))))], clip: false);
            return label is null ? ring : new Row(mainAxisSize: MainAxisSize.Min, crossAxisAlignment: CrossAxisAlignment.Center, spacing: 8, children:
            [
                ring,
                DefaultTextStyle.Merge(ctx, theme.Type.BodyCompact01.Merge(new TextStyle(Color: enabled ? c.TextPrimary : c.TextDisabled)), label),
            ]);
        }, enabled && !selected ? onSelected : enabled ? () => { } : null);
    }
}

public sealed record CarbonRadioItem<T>(T Value, string Label, bool Enabled = true);

/// <summary>A labelled set of radio buttons where exactly one value is chosen. Arrow keys are not remapped; Tab moves between buttons.</summary>
public sealed class CarbonRadioGroup<T>(IReadOnlyList<CarbonRadioItem<T>> items, T? value, Action<T>? onChanged = null, string? legend = null,
    Axis direction = Axis.Vertical, Key? key = null) : StatelessWidget(key) where T : notnull
{
    public override Widget Build(BuildContext context)
    {
        var theme = CarbonTheme.Of(context);
        var c = theme.Colors;
        var radios = items.Select(i => (Widget)new CarbonRadioButton(EqualityComparer<T>.Default.Equals(i.Value, value),
            onChanged is not null && i.Enabled ? () => onChanged(i.Value) : null, new Text(i.Label))).ToList();
        Widget set = new Flex(direction, radios, MainAxisAlignment.Start, MainAxisSize.Min,
            direction == Axis.Vertical ? CrossAxisAlignment.Start : CrossAxisAlignment.Center, spacing: direction == Axis.Vertical ? 12 : 24);
        return legend is null ? set : new Column(crossAxisAlignment: CrossAxisAlignment.Start, mainAxisSize: MainAxisSize.Min, spacing: 12, children:
        [
            new Text(legend, style: theme.Type.Label01.Merge(new TextStyle(Color: c.TextSecondary))),
            set,
        ]);
    }
}

/// <summary>A Carbon toggle: a switch that applies its change immediately. <paramref name="small"/> is the 32x16 variant without a state label.</summary>
public sealed class CarbonToggle(bool toggled, Action<bool>? onChanged = null, string? label = null, string labelOn = "On", string labelOff = "Off",
    bool small = false, Key? key = null) : StatelessWidget(key)
{
    public override Widget Build(BuildContext context)
    {
        var theme = CarbonTheme.Of(context);
        var c = theme.Colors;
        bool enabled = onChanged is not null;
        float w = small ? 32 : 48, h = small ? 16 : 24, knob = small ? 10 : 18, inset = (h - knob) / 2;
        var on = c.SupportSuccess;
        var off = c.BorderStrong01;

        return new Column(crossAxisAlignment: CrossAxisAlignment.Start, mainAxisSize: MainAxisSize.Min, spacing: 16, children:
        [
            ..label is null ? Array.Empty<Widget>() : [new Text(label, style: theme.Type.Label01.Merge(new TextStyle(Color: enabled ? c.TextSecondary : c.TextDisabled)))],
            new Interactive((ctx, st) =>
            {
                var track = !enabled ? c.BorderDisabled : toggled ? on : off;
                Widget body = new Container(width: w, height: h, decoration: new BoxDecoration(Color: track, BorderRadius: BorderRadius.Circular(h / 2)),
                    child: new Align(toggled ? Alignment.CenterRight : Alignment.CenterLeft, new Padding(EdgeInsets.Symmetric(horizontal: inset),
                        new Container(width: knob, height: knob, decoration: new BoxDecoration(Color: Colors.White, BorderRadius: BorderRadius.Circular(knob / 2))))));
                body = new Stack([body, Positioned.Fill(new IgnorePointer(new DecoratedBox(new BoxDecoration(
                    Border: Border.All(st.FocusVisible ? c.Focus : Colors.Transparent, 2), BorderRadius: BorderRadius.Circular(h / 2)))))], clip: false);
                return new Row(mainAxisSize: MainAxisSize.Min, crossAxisAlignment: CrossAxisAlignment.Center, spacing: 8, children:
                [
                    body,
                    new Text(toggled ? labelOn : labelOff, style: theme.Type.BodyCompact01.Merge(new TextStyle(Color: enabled ? c.TextPrimary : c.TextDisabled))),
                ]);
            }, enabled ? () => onChanged!(!toggled) : null),
        ]);
    }
}

/// <summary>A Carbon link: text in the link colour that underlines on hover. <paramref name="inline"/> keeps the underline always, for links inside body copy.</summary>
public sealed class CarbonLink(string text, Action? onPressed, bool inline = false, bool visited = false, bool disabled = false, Key? key = null) : StatelessWidget(key)
{
    public override Widget Build(BuildContext context)
    {
        var theme = CarbonTheme.Of(context);
        var c = theme.Colors;
        return new Interactive((ctx, st) =>
        {
            var color = disabled ? c.TextDisabled : visited ? c.LinkPrimary.WithOpacity(0.8f) : c.LinkPrimary;
            var style = theme.Type.BodyCompact01.Merge(new TextStyle(Color: color,
                Decoration: inline || (st.Hover && !disabled) ? TextDecoration.Underline : TextDecoration.None));
            return CarbonFocus.Around(st.FocusVisible, theme, new Text(text, style: style));
        }, disabled ? null : onPressed);
    }
}
