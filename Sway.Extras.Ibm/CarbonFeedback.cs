using SkiaSharp;
using Sway.Widgets;

namespace Sway.Extras.Ibm;

public enum CarbonStatus { Active, Finished, Error }

/// <summary>
/// A Carbon progress bar. A null <paramref name="value"/> is indeterminate: a segment sweeps along the track.
/// <paramref name="value"/> runs from 0 to <paramref name="max"/>.
/// </summary>
public sealed class CarbonProgressBar(double? value = null, double max = 100, string? label = null, string? helperText = null,
    CarbonStatus status = CarbonStatus.Active, Key? key = null) : StatefulWidget(key)
{
    internal double? Value => value;
    internal double Max => max;
    internal string? Label => label;
    internal string? HelperText => helperText;
    internal CarbonStatus Status => status;
    public override State CreateState() => new CarbonProgressBarState();
}

sealed class CarbonProgressBarState : TickerProviderState<CarbonProgressBar>
{
    AnimationController? _c;

    public override void InitState() => Sync();
    public override void DidUpdateWidget(CarbonProgressBar old) => Sync();

    void Sync()
    {
        bool sweep = Widget.Value is null && Widget.Status == CarbonStatus.Active;
        if (sweep && _c is null)
        {
            _c = new AnimationController(this, TimeSpan.FromMilliseconds(1400));
            _c.AddListener(() => { if (Mounted) SetState(); });
            _c.Repeat();
        }
        else if (!sweep && _c is not null) { _c.Dispose(); _c = null; }
    }

    public override void Dispose() { _c?.Dispose(); base.Dispose(); }

    public override Widget Build(BuildContext context)
    {
        var theme = CarbonTheme.Of(context);
        var c = theme.Colors;
        var fill = Widget.Status switch { CarbonStatus.Error => c.SupportError, CarbonStatus.Finished => c.SupportSuccess, _ => c.IconPrimary };
        float frac = Widget.Status == CarbonStatus.Finished ? 1 : (float)Math.Clamp((Widget.Value ?? 0) / Math.Max(1e-9, Widget.Max), 0, 1);
        float t = _c?.Value ?? 0;

        Widget bar = new LayoutBuilder((ctx, box) =>
        {
            float w = box.MaxWidth;
            Widget segment = Widget.Value is null && Widget.Status == CarbonStatus.Active
                ? new Positioned(new Container(color: fill), left: (w * 1.4f) * t - w * 0.4f, width: w * 0.4f, top: 0, bottom: 0)
                : new Positioned(new Container(color: fill), left: 0, width: w * frac, top: 0, bottom: 0);
            return new SizedBox(height: 8, child: new Stack([new Container(color: c.LayerAccent01), segment], fit: StackFit.Expand));
        });

        Widget? status = Widget.Status switch
        {
            CarbonStatus.Finished => new Icon(Icons.CheckCircle, 16, c.SupportSuccess),
            CarbonStatus.Error => new Icon(Icons.Error, 16, c.SupportError),
            _ => null,
        };
        string? below = Widget.HelperText;
        return new Column(crossAxisAlignment: CrossAxisAlignment.Stretch, mainAxisSize: MainAxisSize.Min, spacing: 8, children:
        [
            ..Widget.Label is null && status is null ? Array.Empty<Widget>() :
            [new Row(crossAxisAlignment: CrossAxisAlignment.Center, children:
            [
                new Expanded(Widget.Label is null ? new SizedBox() : new Text(Widget.Label, style: theme.Type.Label01.Merge(new TextStyle(Color: c.TextSecondary)))),
                ..status is null ? Array.Empty<Widget>() : [status],
            ])],
            new ClipRect(bar),
            ..below is null ? Array.Empty<Widget>() : [new Text(below, style: theme.Type.HelperText01.Merge(new TextStyle(Color: Widget.Status == CarbonStatus.Error ? c.TextError : c.TextSecondary)))],
        ]);
    }
}

/// <summary>A Carbon loading spinner. 88px by default, or the 16px <paramref name="small"/> size for use beside text.</summary>
public sealed class CarbonLoading(bool small = false, bool active = true, Key? key = null) : StatefulWidget(key)
{
    internal bool Small => small;
    internal bool Active => active;
    public override State CreateState() => new CarbonLoadingState();
}

sealed class CarbonLoadingState : TickerProviderState<CarbonLoading>
{
    AnimationController? _c;

    public override void InitState() => Sync();
    public override void DidUpdateWidget(CarbonLoading old) => Sync();

    void Sync()
    {
        if (Widget.Active && _c is null)
        {
            _c = new AnimationController(this, TimeSpan.FromMilliseconds(1000));
            _c.AddListener(() => { if (Mounted) SetState(); });
            _c.Repeat();
        }
        else if (!Widget.Active && _c is not null) { _c.Dispose(); _c = null; }
    }

    public override void Dispose() { _c?.Dispose(); base.Dispose(); }

    public override Widget Build(BuildContext context)
    {
        var c = CarbonTheme.Of(context).Colors;
        float size = Widget.Small ? 16 : 88;
        return new CustomPaint(new CarbonSpinnerPainter(_c?.Value ?? 0, c.IconPrimary, c.LayerAccent01, Widget.Small ? 2 : 8, Widget.Active), size: new Size(size, size));
    }
}

sealed class CarbonSpinnerPainter(float t, SKColor color, SKColor track, float stroke, bool active) : CustomPainter
{
    public override void Paint(SKCanvas canvas, Size size)
    {
        var rect = new SKRect(stroke / 2, stroke / 2, size.Width - stroke / 2, size.Height - stroke / 2);
        using var p = new SKPaint { Color = track, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = stroke };
        canvas.DrawOval(rect, p);
        if (!active) return;
        p.Color = color;
        canvas.DrawArc(rect, -90 + t * 360, 90, false, p);
    }
    public override bool ShouldRepaint(CustomPainter old) => true;
}

/// <summary>Loading text with a small spinner, which turns into a check or an error icon once the work is done.</summary>
public sealed class CarbonInlineLoading(string text, CarbonStatus status = CarbonStatus.Active, Key? key = null) : StatelessWidget(key)
{
    public override Widget Build(BuildContext context)
    {
        var theme = CarbonTheme.Of(context);
        var c = theme.Colors;
        Widget icon = status switch
        {
            CarbonStatus.Finished => new Icon(Icons.CheckCircle, 16, c.SupportSuccess),
            CarbonStatus.Error => new Icon(Icons.Error, 16, c.SupportError),
            _ => new CarbonLoading(small: true),
        };
        return new Row(mainAxisSize: MainAxisSize.Min, crossAxisAlignment: CrossAxisAlignment.Center, spacing: 8, children:
            [icon, new Text(text, style: theme.Type.BodyCompact01.Merge(new TextStyle(Color: c.TextSecondary)))]);
    }
}

/// <summary>A grey block that stands in for content that is still loading.</summary>
public sealed class CarbonSkeleton(float? width = null, float height = 16, Key? key = null) : StatelessWidget(key)
{
    public override Widget Build(BuildContext context) =>
        new Container(width: width, height: height, color: CarbonTheme.Of(context).Colors.LayerAccent01);
}

/// <summary>Skeleton lines of text; the last one is shorter so it reads as a paragraph.</summary>
public sealed class CarbonSkeletonText(int lines = 3, Key? key = null) : StatelessWidget(key)
{
    public override Widget Build(BuildContext context) =>
        new Column(crossAxisAlignment: CrossAxisAlignment.Stretch, mainAxisSize: MainAxisSize.Min, spacing: 8, children:
            Enumerable.Range(0, lines).Select(i => (Widget)(i == lines - 1 && lines > 1
                ? new FractionallySizedBox(widthFactor: 0.6f, alignment: Alignment.CenterLeft, child: new CarbonSkeleton())
                : new CarbonSkeleton())).ToList());
}

public sealed record CarbonStep(string Label, string? Secondary = null, bool Invalid = false, bool Enabled = true);

/// <summary>A Carbon progress indicator: a row of steps, the ones before <paramref name="current"/> complete. Pass <paramref name="onStepTapped"/> to let people jump to a step.</summary>
public sealed class CarbonProgressIndicator(IReadOnlyList<CarbonStep> steps, int current, Action<int>? onStepTapped = null, Key? key = null) : StatelessWidget(key)
{
    public override Widget Build(BuildContext context)
    {
        var theme = CarbonTheme.Of(context);
        var c = theme.Colors;
        return new Row(crossAxisAlignment: CrossAxisAlignment.Start, children: steps.Select((s, i) =>
        {
            bool done = i < current, now = i == current;
            var tone = s.Invalid ? c.SupportError : done || now ? c.Interactive : c.IconDisabled;
            Widget dot = s.Invalid ? new Icon(Icons.Error, 16, c.SupportError)
                : done ? new Icon(Icons.CheckCircle, 16, c.Interactive)
                : new Container(width: 16, height: 16, decoration: new BoxDecoration(Border: Border.All(tone, 1), BorderRadius: BorderRadius.Circular(8)),
                    child: now ? new Center(new Container(width: 8, height: 8, decoration: new BoxDecoration(Color: tone, BorderRadius: BorderRadius.Circular(4)))) : null);
            bool tappable = onStepTapped is not null && s.Enabled && !now;
            Widget step = new Interactive((ctx, st) => CarbonFocus.Around(st.FocusVisible, theme, new Column(
                crossAxisAlignment: CrossAxisAlignment.Start, mainAxisSize: MainAxisSize.Min, spacing: 8, children:
            [
                new Container(height: 16, child: new Row(crossAxisAlignment: CrossAxisAlignment.Center, children:
                [
                    dot,
                    new Expanded(new Container(height: 1, color: i < current ? c.Interactive : c.BorderSubtle01, margin: EdgeInsets.Only(left: 0))),
                ])),
                new Padding(EdgeInsets.Only(right: 8), new Column(crossAxisAlignment: CrossAxisAlignment.Start, mainAxisSize: MainAxisSize.Min, spacing: 2, children:
                [
                    new Text(s.Label, style: theme.Type.BodyCompact01.Merge(new TextStyle(Color: s.Enabled ? c.TextPrimary : c.TextDisabled,
                        FontWeight: now ? FontWeight.W600 : FontWeight.W400, Decoration: st.Hover && tappable ? TextDecoration.Underline : TextDecoration.None))),
                    ..s.Secondary is null ? Array.Empty<Widget>() : [new Text(s.Secondary, style: theme.Type.Label01.Merge(new TextStyle(Color: c.TextSecondary)))],
                ])),
            ])), tappable ? () => onStepTapped!(i) : null, cursor: tappable ? MouseCursor.Click : MouseCursor.Default, focusable: tappable);
            return (Widget)new Expanded(step);
        }).ToList());
    }
}

public enum CarbonNotificationKind { Info, Success, Warning, Error }

/// <summary>
/// A Carbon notification. By default it is an inline notification that fills its width in a low-contrast tint; <paramref name="toast"/>
/// gives the high-contrast 288px toast for the corner of the screen. <paramref name="actionLabel"/> adds an action button.
/// </summary>
public sealed class CarbonNotification(CarbonNotificationKind kind, string title, string? subtitle = null, Action? onClose = null,
    string? actionLabel = null, Action? onAction = null, bool toast = false, Key? key = null) : StatelessWidget(key)
{
    public override Widget Build(BuildContext context)
    {
        var theme = CarbonTheme.Of(context);
        var c = theme.Colors;
        bool dark = c.Brightness == Brightness.Dark;
        (uint tint, uint darkTint, SKColor edge, IconData icon) = kind switch
        {
            CarbonNotificationKind.Success => (0xDEFBE6u, 0x0E3D1Bu, c.SupportSuccess, Icons.CheckCircle),
            CarbonNotificationKind.Warning => (0xFCF4D6u, 0x3D3000u, c.SupportWarning, Icons.Warning),
            CarbonNotificationKind.Error => (0xFFF1F1u, 0x3E1215u, c.SupportError, Icons.Error),
            _ => (0xEDF5FFu, 0x0C2142u, c.SupportInfo, Icons.Info),
        };
        var back = toast ? c.BackgroundInverse : Colors.FromRgb(dark ? darkTint : tint);
        var main = toast ? c.TextInverse : c.TextPrimary;
        var second = toast ? c.TextInverse : c.TextPrimary;
        var iconColor = kind == CarbonNotificationKind.Warning && !dark ? Colors.FromRgb(0x161616) : toast ? edge : edge;
        if (toast && kind == CarbonNotificationKind.Warning) iconColor = c.SupportWarning;

        Widget text = new Column(crossAxisAlignment: CrossAxisAlignment.Start, mainAxisSize: MainAxisSize.Min, spacing: 0, children:
        [
            new Text(title, style: theme.Type.Heading01.Merge(new TextStyle(Color: main))),
            ..subtitle is null ? Array.Empty<Widget>() : [new Text(subtitle, style: theme.Type.Body01.Merge(new TextStyle(Color: second)))],
        ]);
        Widget action = actionLabel is null ? new SizedBox() : new CarbonButton(new Text(actionLabel), onAction,
            toast ? CarbonButtonKind.GhostOnColor : CarbonButtonKind.Ghost, CarbonButtonSize.Small);

        Widget row = new Row(crossAxisAlignment: CrossAxisAlignment.Start, children:
        [
            new Padding(EdgeInsets.Only(left: 16, top: 14, right: 16), new Icon(icon, 20, iconColor)),
            new Expanded(new Padding(EdgeInsets.Symmetric(vertical: 14), toast || actionLabel is null ? text : new Wrap(spacing: 16, runSpacing: 8, children: [text, action]))),
            ..onClose is null ? Array.Empty<Widget>() : [new Padding(EdgeInsets.All(0), new SizedBox(width: 48, height: 48, child: new Interactive((ctx, st) =>
                new Container(color: st.Hover ? (toast ? c.BackgroundInverse.WithOpacity(0.85f) : c.LayerHover01) : Colors.Transparent,
                    child: CarbonFocus.Around(st.FocusVisible, theme, new Center(new Icon(Icons.Close, 16, toast ? c.IconInverse : c.IconPrimary)))), onClose)))],
        ]);
        Widget body = Container(back, edge, row);
        return toast ? new SizedBox(width: 288, child: body) : body;

        static Widget Container(SKColor back, SKColor edge, Widget child) => new DecoratedBox(
            new BoxDecoration(Color: back), new Container(
                decoration: new BoxDecoration(Border: Border.Only(left: new BorderSide(edge, 3))), child: child));
    }
}
