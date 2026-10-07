using SkiaSharp;
using Sway.Widgets;

namespace Sway.Extras.Ibm;

/// <summary>
/// A Carbon checkbox: a 16px square with a 1px border that fills solid when checked. <paramref name="indeterminate"/>
/// shows a dash for a group that is partly checked. <paramref name="hitPadding"/> grows the clickable area without growing the box,
/// which is how the data table gets a 32px target.
/// </summary>
public sealed class CarbonCheckbox(bool value, Action<bool>? onChanged = null, Widget? label = null, bool indeterminate = false,
    float hitPadding = 0, Key? key = null) : StatelessWidget(key)
{
    public override Widget Build(BuildContext context)
    {
        var theme = CarbonTheme.Of(context);
        var c = theme.Colors;
        bool enabled = onChanged is not null;
        bool marked = value || indeterminate;

        return new Interactive((ctx, st) =>
        {
            var edge = !enabled ? c.IconDisabled : c.IconPrimary;
            Widget box = new Container(width: 16, height: 16,
                decoration: new BoxDecoration(Color: marked ? edge : Colors.Transparent, Border: Border.All(edge, 1)),
                child: marked ? new CustomPaint(indeterminate ? new CarbonDashPainter(c.IconInverse) : new CarbonCheckPainter(c.IconInverse), size: new Size(14, 14)) : null);
            box = CarbonFocus.Around(st.FocusVisible, theme, box, width: 2);

            Widget row = label is null
                ? box
                : new Row(mainAxisSize: MainAxisSize.Min, crossAxisAlignment: CrossAxisAlignment.Center, spacing: 8, children:
                [
                    box,
                    DefaultTextStyle.Merge(ctx, theme.Type.BodyCompact01.Merge(new TextStyle(Color: enabled ? c.TextPrimary : c.TextDisabled)), label),
                ]);
            return hitPadding > 0 ? new Padding(EdgeInsets.All(hitPadding), row) : row;
        }, enabled ? () => onChanged!(!value) : null);
    }
}

sealed class CarbonCheckPainter(SKColor color) : CustomPainter
{
    public override void Paint(SKCanvas canvas, Size size)
    {
        using var p = new SKPaint { Color = color, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1.5f };
        using var b = new SKPathBuilder();
        b.MoveTo(size.Width * 0.18f, size.Height * 0.52f);
        b.LineTo(size.Width * 0.42f, size.Height * 0.76f);
        b.LineTo(size.Width * 0.84f, size.Height * 0.28f);
        using var path = b.Detach();
        canvas.DrawPath(path, p);
    }
    public override bool ShouldRepaint(CustomPainter old) => true;
}

sealed class CarbonDashPainter(SKColor color) : CustomPainter
{
    public override void Paint(SKCanvas canvas, Size size)
    {
        using var p = new SKPaint { Color = color, IsAntialias = false, Style = SKPaintStyle.Stroke, StrokeWidth = 2 };
        canvas.DrawLine(size.Width * 0.2f, size.Height / 2, size.Width * 0.8f, size.Height / 2, p);
    }
    public override bool ShouldRepaint(CustomPainter old) => true;
}
