using SkiaSharp;

namespace Sway.Widgets;

/// <summary>Supplies a <see cref="ThemeData"/> to the subtree. Without one, widgets use the default Material 3 light theme.</summary>
public sealed class Theme(ThemeData data, Widget child, Key? key = null) : InheritedWidget(child, key)
{
    public ThemeData Data { get; } = data;
    public override bool UpdateShouldNotify(InheritedWidget old) => !ReferenceEquals(((Theme)old).Data, Data) && !((Theme)old).Data.Equals(Data);

    static readonly ThemeData Fallback = ThemeData.Light();

    /// <summary>The nearest theme; rebuilds the caller when the theme changes.</summary>
    public static ThemeData Of(BuildContext context) => context.DependOn<Theme>()?.Data ?? Fallback;
}

public enum ThemeMode { System, Light, Dark }

/// <summary>Root widget for Material apps: picks light or dark theme, installs the default text style and paints the surface.</summary>
public sealed class MaterialApp(Widget home, ThemeData? theme = null, ThemeData? darkTheme = null, ThemeMode themeMode = ThemeMode.System,
    TextDirection? textDirection = null, Key? key = null) : StatefulWidget(key)
{
    internal Widget Home => home;
    internal ThemeData? Theme => theme;
    internal ThemeData? DarkTheme => darkTheme;
    internal ThemeMode Mode => themeMode;
    internal TextDirection? Direction => textDirection;
    public override State CreateState() => new MaterialAppState();
}

sealed class MaterialAppState : State<MaterialApp>
{
    public override void InitState() => WidgetsBinding.Instance.PlatformBrightnessChanged += OnBrightness;
    public override void Dispose() => WidgetsBinding.Instance.PlatformBrightnessChanged -= OnBrightness;
    void OnBrightness() { if (Mounted) SetState(); }

    public override Widget Build(BuildContext context)
    {
        bool dark = Widget.Mode switch
        {
            ThemeMode.Dark => true,
            ThemeMode.Light => false,
            _ => WidgetsBinding.Instance.PlatformBrightness == Brightness.Dark,
        };
        var data = dark ? Widget.DarkTheme ?? ThemeData.Dark() : Widget.Theme ?? ThemeData.Light();
        Widget app = new Theme(data, new IconTheme(data.ColorScheme.OnSurfaceVariant, 24,
            new DefaultTextStyle(data.TextTheme.BodyMedium, new ColoredBox(data.ScaffoldBackground, Widget.Home))));
        if (Widget.Direction is { } d) app = new Directionality(d, app);
        return app;
    }
}

public sealed class IconTheme(SKColor? color, float? size, Widget child, Key? key = null) : InheritedWidget(child, key)
{
    public SKColor? Color { get; } = color;
    public float? Size { get; } = size;
    public override bool UpdateShouldNotify(InheritedWidget old) => ((IconTheme)old).Color != Color || ((IconTheme)old).Size != Size;
    public static IconTheme? Of(BuildContext context) => context.DependOn<IconTheme>();
}

/// <summary>A vector icon on a 24x24 grid, described by SVG path data.</summary>
public sealed record IconData(string Path);

public sealed class Icon(IconData icon, float? size = null, SKColor? color = null, Key? key = null) : StatelessWidget(key)
{
    public override Widget Build(BuildContext context)
    {
        var theme = IconTheme.Of(context);
        float s = size ?? theme?.Size ?? 24;
        var c = color ?? theme?.Color ?? Theme.Of(context).ColorScheme.OnSurfaceVariant;
        return new CustomPaint(new IconPainter(icon, c), size: new Size(s, s));
    }
}

sealed class IconPainter(IconData icon, SKColor color) : CustomPainter
{
    static readonly Dictionary<string, SKPath?> Cache = new();

    public override void Paint(SKCanvas canvas, Size size)
    {
        if (!Cache.TryGetValue(icon.Path, out var path)) Cache[icon.Path] = path = SKPath.ParseSvgPathData(icon.Path);
        if (path is null) return;
        canvas.Save();
        canvas.Scale(size.Width / 24f, size.Height / 24f);
        using var paint = new SKPaint { Color = color, IsAntialias = true, Style = SKPaintStyle.Fill };
        canvas.DrawPath(path, paint);
        canvas.Restore();
    }

    public override bool ShouldRepaint(CustomPainter old) => old is not IconPainter p || p.GetHashCode() != GetHashCode() || true;
}

/// <summary>A small set of Material icons (Apache 2.0, Google) as path data.</summary>
public static partial class Icons
{
    public static readonly IconData Add = new("M19 13h-6v6h-2v-6H5v-2h6V5h2v6h6v2z");
    public static readonly IconData Close = new("M19 6.41L17.59 5 12 10.59 6.41 5 5 6.41 10.59 12 5 17.59 6.41 19 12 13.41 17.59 19 19 17.59 13.41 12z");
    public static readonly IconData Check = new("M9 16.17L4.83 12l-1.42 1.41L9 19 21 7l-1.41-1.41z");
    public static readonly IconData Menu = new("M3 18h18v-2H3v2zm0-5h18v-2H3v2zm0-7v2h18V6H3z");
    public static readonly IconData Search = new("M15.5 14h-.79l-.28-.27C15.41 12.59 16 11.11 16 9.5 16 5.91 13.09 3 9.5 3S3 5.91 3 9.5 5.91 16 9.5 16c1.61 0 3.09-.59 4.23-1.57l.27.28v.79l5 4.99L20.49 19l-4.99-5zm-6 0C7.01 14 5 11.99 5 9.5S7.01 5 9.5 5 14 7.01 14 9.5 11.99 14 9.5 14z");
    public static readonly IconData ArrowBack = new("M20 11H7.83l5.59-5.59L12 4l-8 8 8 8 1.41-1.41L7.83 13H20v-2z");
    public static readonly IconData ArrowForward = new("M12 4l-1.41 1.41L16.17 11H4v2h12.17l-5.58 5.59L12 20l8-8z");
    public static readonly IconData ExpandMore = new("M16.59 8.59L12 13.17 7.41 8.59 6 10l6 6 6-6z");
    public static readonly IconData ExpandLess = new("M12 8l-6 6 1.41 1.41L12 10.83l4.59 4.58L18 14z");
    public static readonly IconData ChevronRight = new("M10 6L8.59 7.41 13.17 12l-4.58 4.59L10 18l6-6z");
    public static readonly IconData Favorite = new("M12 21.35l-1.45-1.32C5.4 15.36 2 12.28 2 8.5 2 5.42 4.42 3 7.5 3c1.74 0 3.41.81 4.5 2.09C13.09 3.81 14.76 3 16.5 3 19.58 3 22 5.42 22 8.5c0 3.78-3.4 6.86-8.55 11.54L12 21.35z");
    public static readonly IconData Star = new("M12 17.27L18.18 21l-1.64-7.03L22 9.24l-7.19-.61L12 2 9.19 8.63 2 9.24l5.46 4.73L5.82 21z");
    public static readonly IconData Home = new("M10 20v-6h4v6h5v-8h3L12 3 2 12h3v8z");
    public static readonly IconData Delete = new("M6 19c0 1.1.9 2 2 2h8c1.1 0 2-.9 2-2V7H6v12zM19 4h-3.5l-1-1h-5l-1 1H5v2h14V4z");
    public static readonly IconData Edit = new("M3 17.25V21h3.75L17.81 9.94l-3.75-3.75L3 17.25zM20.71 7.04c.39-.39.39-1.02 0-1.41l-2.34-2.34c-.39-.39-1.02-.39-1.41 0l-1.83 1.83 3.75 3.75 1.83-1.83z");
    public static readonly IconData Info = new("M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm1 15h-2v-6h2v6zm0-8h-2V7h2v2z");
    public static readonly IconData CheckCircle = new("M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm-2 15l-5-5 1.41-1.41L10 14.17l7.59-7.59L19 8l-9 9z");
    public static readonly IconData MoreVert = new("M12 8c1.1 0 2-.9 2-2s-.9-2-2-2-2 .9-2 2 .9 2 2 2zm0 2c-1.1 0-2 .9-2 2s.9 2 2 2 2-.9 2-2-.9-2-2-2zm0 6c-1.1 0-2 .9-2 2s.9 2 2 2 2-.9 2-2-.9-2-2-2z");
    public static readonly IconData DarkMode = new("M12 3c-4.97 0-9 4.03-9 9s4.03 9 9 9 9-4.03 9-9c0-.46-.04-.92-.1-1.36-.98 1.37-2.58 2.26-4.4 2.26-2.98 0-5.4-2.42-5.4-5.4 0-1.81.89-3.42 2.26-4.4-.44-.06-.9-.1-1.36-.1z");
    public static readonly IconData LightMode = new("M12 7c-2.76 0-5 2.24-5 5s2.24 5 5 5 5-2.24 5-5-2.24-5-5-5zM2 13h2c.55 0 1-.45 1-1s-.45-1-1-1H2c-.55 0-1 .45-1 1s.45 1 1 1zm18 0h2c.55 0 1-.45 1-1s-.45-1-1-1h-2c-.55 0-1 .45-1 1s.45 1 1 1zM11 2v2c0 .55.45 1 1 1s1-.45 1-1V2c0-.55-.45-1-1-1s-1 .45-1 1zm0 18v2c0 .55.45 1 1 1s1-.45 1-1v-2c0-.55-.45-1-1-1s-1 .45-1 1zM5.99 4.58c-.39-.39-1.03-.39-1.41 0-.39.39-.39 1.03 0 1.41l1.06 1.06c.39.39 1.03.39 1.41 0s.39-1.03 0-1.41L5.99 4.58zm12.37 12.37c-.39-.39-1.03-.39-1.41 0-.39.39-.39 1.03 0 1.41l1.06 1.06c.39.39 1.03.39 1.41 0 .39-.39.39-1.03 0-1.41l-1.06-1.06zm1.06-10.96c.39-.39.39-1.03 0-1.41-.39-.39-1.03-.39-1.41 0l-1.06 1.06c-.39.39-.39 1.03 0 1.41s1.03.39 1.41 0l1.06-1.06zM7.05 18.36c.39-.39.39-1.03 0-1.41-.39-.39-1.03-.39-1.41 0l-1.06 1.06c-.39.39-.39 1.03 0 1.41s1.03.39 1.41 0l1.06-1.06z");
}

/// <summary>Helpers for Material 3 interaction "state layers": a translucent on-colour overlay for hover, focus and press.</summary>
public static class StateLayer
{
    public static float Opacity(InteractionState s) => s.Pressed ? 0.10f : s.FocusVisible ? 0.10f : s.Hover ? 0.08f : 0f;

    /// <summary>Composites <paramref name="layer"/> at <paramref name="opacity"/> over <paramref name="baseColor"/>.</summary>
    public static SKColor Blend(SKColor baseColor, SKColor layer, float opacity)
    {
        if (opacity <= 0) return baseColor;
        // A transparent base keeps its transparency so a text button only shows the tint.
        if (baseColor.Alpha == 0) return layer.WithOpacity(opacity);
        return Lerps.Color(baseColor, layer.WithAlpha(baseColor.Alpha), opacity);
    }
}

/// <summary>A surface with colour, elevation (shadow), shape and optional border.</summary>
public sealed class Material(Widget? child = null, SKColor? color = null, int elevation = 0, BorderRadius? borderRadius = null,
    Border? border = null, bool clip = true, Key? key = null) : StatelessWidget(key)
{
    public override Widget Build(BuildContext context)
    {
        var scheme = Theme.Of(context).ColorScheme;
        var fill = color ?? scheme.Surface;
        Widget inner = child ?? new SizedBox();
        if (clip && borderRadius is { IsZero: false } r) inner = new ClipRRect(r, inner);
        return new DecoratedBox(new BoxDecoration(Color: fill, BorderRadius: borderRadius, Border: border, BoxShadow: Elevation.Shadows(elevation, scheme.Shadow)), inner);
    }
}

public sealed class Divider(float height = 16, float thickness = 1, float indent = 0, float endIndent = 0, SKColor? color = null, Key? key = null) : StatelessWidget(key)
{
    public override Widget Build(BuildContext context) => new SizedBox(height: height, child: new Center(
        new Container(height: thickness, margin: EdgeInsets.Only(left: indent, right: endIndent),
            color: color ?? Theme.Of(context).ColorScheme.OutlineVariant)));
}

public enum CardVariant { Elevated, Filled, Outlined }

public sealed class Card(Widget? child = null, CardVariant variant = CardVariant.Elevated, EdgeInsets? margin = null, SKColor? color = null,
    Action? onTap = null, Key? key = null) : StatelessWidget(key)
{
    public override Widget Build(BuildContext context)
    {
        var s = Theme.Of(context).ColorScheme;
        var radius = BorderRadius.Circular(Shapes.Medium);
        var (bg, elev, border) = variant switch
        {
            CardVariant.Filled => (s.SurfaceContainerHighest, 0, (Border?)null),
            CardVariant.Outlined => (s.Surface, 0, Border.All(s.OutlineVariant)),
            _ => (s.SurfaceContainerLow, 1, null),
        };
        bg = color ?? bg;
        Widget card = onTap is null
            ? new Material(child, bg, elev, radius, border)
            : new Interactive((ctx, st) => new AnimatedContainer(TimeSpan.FromMilliseconds(120),
                    decoration: new BoxDecoration(Color: StateLayer.Blend(bg, s.OnSurface, StateLayer.Opacity(st)), BorderRadius: radius, Border: border,
                        BoxShadow: Elevation.Shadows(st.Hover && variant == CardVariant.Elevated ? 2 : elev, s.Shadow)),
                    child: new ClipRRect(radius, child ?? new SizedBox())), onTap);
        return new Padding(margin ?? EdgeInsets.All(4), card);
    }
}

public sealed class ListTile(Widget? title = null, Widget? subtitle = null, Widget? leading = null, Widget? trailing = null,
    Action? onTap = null, bool selected = false, Key? key = null) : StatelessWidget(key)
{
    public override Widget Build(BuildContext context)
    {
        var theme = Theme.Of(context);
        var s = theme.ColorScheme;
        bool two = subtitle is not null;
        return new Interactive((ctx, st) =>
        {
            var bg = StateLayer.Blend(selected ? s.SecondaryContainer : Colors.Transparent, s.OnSurface, onTap is null ? 0 : StateLayer.Opacity(st));
            var content = new Row(spacing: 16, crossAxisAlignment: CrossAxisAlignment.Center, children:
            [
                ..leading is null ? Array.Empty<Widget>() : [new IconTheme(selected ? s.OnSecondaryContainer : s.OnSurfaceVariant, 24, leading)],
                new Expanded(new Column(mainAxisSize: MainAxisSize.Min, crossAxisAlignment: CrossAxisAlignment.Start, children:
                [
                    DefaultTextStyle.Merge(ctx, theme.TextTheme.BodyLarge.Merge(new TextStyle(Color: selected ? s.OnSecondaryContainer : s.OnSurface)), title ?? new SizedBox()),
                    ..two ? [DefaultTextStyle.Merge(ctx, theme.TextTheme.BodyMedium.Merge(new TextStyle(Color: s.OnSurfaceVariant)), subtitle!)] : Array.Empty<Widget>(),
                ])),
                ..trailing is null ? Array.Empty<Widget>() : [DefaultTextStyle.Merge(ctx, theme.TextTheme.LabelSmall.Merge(new TextStyle(Color: s.OnSurfaceVariant)), trailing)],
            ]);
            return new AnimatedContainer(TimeSpan.FromMilliseconds(100), color: bg, constraints: new BoxConstraints(0, float.PositiveInfinity, two ? 72 : 56, float.PositiveInfinity),
                padding: EdgeInsets.Symmetric(16, 8), child: content);
        }, onTap, cursor: onTap is null ? MouseCursor.Default : MouseCursor.Click, focusable: onTap is not null);
    }
}

public sealed class Chip(Widget label, Action? onPressed = null, bool selected = false, IconData? icon = null, Action? onDeleted = null, Key? key = null) : StatelessWidget(key)
{
    public override Widget Build(BuildContext context)
    {
        var theme = Theme.Of(context);
        var s = theme.ColorScheme;
        var radius = BorderRadius.Circular(Shapes.Small);
        return new Interactive((ctx, st) =>
        {
            var fg = selected ? s.OnSecondaryContainer : s.OnSurfaceVariant;
            var bg = StateLayer.Blend(selected ? s.SecondaryContainer : Colors.Transparent, selected ? s.OnSecondaryContainer : s.OnSurfaceVariant, StateLayer.Opacity(st));
            Widget body = new AnimatedContainer(TimeSpan.FromMilliseconds(120), height: 32,
                decoration: new BoxDecoration(Color: bg, BorderRadius: radius, Border: selected ? null : Border.All(s.Outline)),
                // The content swaps instantly; the size eases once, so the chip does not jump wider and then shrink back.
                child: new AnimatedSize(TimeSpan.FromMilliseconds(120), new Padding(EdgeInsets.Symmetric(horizontal: icon is null && !selected ? 16 : 8), new Row(mainAxisSize: MainAxisSize.Min, spacing: 8, children:
                [
                    ..selected ? [new Icon(Icons.Check, 18, fg)] : icon is not null ? [new Icon(icon, 18, fg)] : Array.Empty<Widget>(),
                    DefaultTextStyle.Merge(ctx, theme.TextTheme.LabelLarge.Merge(new TextStyle(Color: fg)), label),
                    ..onDeleted is null ? Array.Empty<Widget>() : [new GestureDetector(onTap: onDeleted, child: new Icon(Icons.Close, 18, fg))],
                ]))));
            return FocusRing.Around(st.FocusVisible, radius, body, s.Primary);
        }, onPressed);
    }
}

// ---- app structure ----

public sealed class AppBar(Widget? title = null, Widget? leading = null, IReadOnlyList<Widget>? actions = null, SKColor? backgroundColor = null,
    bool centerTitle = false, Key? key = null) : StatelessWidget(key)
{
    public const float Height = 64;

    public override Widget Build(BuildContext context)
    {
        var theme = Theme.Of(context);
        var s = theme.ColorScheme;
        return new Container(height: Height, color: backgroundColor ?? s.Surface, padding: EdgeInsets.Symmetric(horizontal: 4),
            child: new IconTheme(s.OnSurfaceVariant, 24, new Row(children:
            [
                ..(leading is null ? new Widget[] { new SizedBox(width: 12) } : new Widget[] { new SizedBox(48, 48, new Center(leading)), new SizedBox(width: 4) }),
                new Expanded(new Align(centerTitle ? Alignment.Center : AlignmentDirectional.CenterStart,
                    DefaultTextStyle.Merge(context, theme.TextTheme.TitleLarge.Merge(new TextStyle(Color: s.OnSurface)), title ?? new SizedBox()))),
                ..actions ?? Array.Empty<Widget>(),
                new SizedBox(width: 8),
            ])));
    }
}

public sealed class Scaffold(Widget body, AppBar? appBar = null, Widget? floatingActionButton = null, Widget? bottomNavigationBar = null,
    Widget? navigationRail = null, SKColor? backgroundColor = null, Key? key = null) : StatelessWidget(key)
{
    public override Widget Build(BuildContext context)
    {
        var s = Theme.Of(context).ColorScheme;
        Widget content = new Column(crossAxisAlignment: CrossAxisAlignment.Stretch, children:
        [
            ..appBar is null ? Array.Empty<Widget>() : [appBar],
            new Expanded(new Row(crossAxisAlignment: CrossAxisAlignment.Stretch, children:
            [
                ..navigationRail is null ? Array.Empty<Widget>() : [navigationRail],
                new Expanded(body),
            ])),
            ..bottomNavigationBar is null ? Array.Empty<Widget>() : [bottomNavigationBar],
        ]);
        content = new ColoredBox(backgroundColor ?? s.Surface, content);
        if (floatingActionButton is null) return content;
        return new Stack([content,
            new PositionedDirectional(floatingActionButton, end: 16, bottom: bottomNavigationBar is null ? 16 : 96)], fit: StackFit.Expand, clip: false);
    }
}

public sealed class FloatingActionButton(Widget? child = null, Action? onPressed = null, Widget? label = null, Key? key = null) : StatelessWidget(key)
{
    public override Widget Build(BuildContext context)
    {
        var theme = Theme.Of(context);
        var s = theme.ColorScheme;
        var radius = BorderRadius.Circular(Shapes.Large);
        bool extended = label is not null;
        return new Interactive((ctx, st) =>
        {
            var bg = StateLayer.Blend(s.PrimaryContainer, s.OnPrimaryContainer, StateLayer.Opacity(st));
            Widget body = new AnimatedContainer(TimeSpan.FromMilliseconds(120), height: 56, constraints: new BoxConstraints(56, float.PositiveInfinity, 56, 56),
                padding: EdgeInsets.Symmetric(horizontal: extended ? 16 : 0),
                decoration: new BoxDecoration(Color: bg, BorderRadius: radius, BoxShadow: Elevation.Shadows(st.Hover ? 4 : 3, s.Shadow)),
                child: new IconTheme(s.OnPrimaryContainer, 24, new Row(mainAxisSize: MainAxisSize.Min, mainAxisAlignment: MainAxisAlignment.Center, spacing: 8, children:
                [
                    child ?? new SizedBox(),
                    ..extended ? [DefaultTextStyle.Merge(ctx, theme.TextTheme.LabelLarge.Merge(new TextStyle(Color: s.OnPrimaryContainer)), label!)] : Array.Empty<Widget>(),
                ])));
            return FocusRing.Around(st.FocusVisible, radius, body, s.Primary);
        }, onPressed);
    }
}

public enum IconButtonVariant { Standard, Filled, Tonal, Outlined }

public sealed class IconButton(Widget icon, Action? onPressed = null, IconButtonVariant variant = IconButtonVariant.Standard,
    bool selected = false, Key? key = null) : StatelessWidget(key)
{
    public override Widget Build(BuildContext context)
    {
        var s = Theme.Of(context).ColorScheme;
        bool enabled = onPressed is not null;
        return new Interactive((ctx, st) =>
        {
            SKColor bg = Colors.Transparent, fg = selected ? s.Primary : s.OnSurfaceVariant;
            Border? border = null;
            switch (variant)
            {
                case IconButtonVariant.Filled:
                    bg = selected || variant == IconButtonVariant.Filled ? s.Primary : s.SurfaceContainerHighest; fg = s.OnPrimary; break;
                case IconButtonVariant.Tonal:
                    bg = s.SecondaryContainer; fg = s.OnSecondaryContainer; break;
                case IconButtonVariant.Outlined:
                    border = Border.All(s.Outline); bg = selected ? s.InverseSurface : Colors.Transparent; fg = selected ? s.OnInverseSurface : s.OnSurfaceVariant; break;
            }
            if (!enabled) { fg = s.OnSurface.WithOpacity(0.38f); bg = bg.Alpha == 0 ? bg : s.OnSurface.WithOpacity(0.12f); }
            var layered = StateLayer.Blend(bg, fg, enabled ? StateLayer.Opacity(st) : 0);
            Widget body = new AnimatedContainer(TimeSpan.FromMilliseconds(120), width: 40, height: 40, alignment: Alignment.Center,
                decoration: new BoxDecoration(Color: layered, Shape: BoxShape.Circle, Border: border), child: new IconTheme(fg, 24, icon));
            return FocusRing.Around(st.FocusVisible, BorderRadius.Circular(100), body, s.Primary);
        }, onPressed);
    }
}

// ---- progress ----

public sealed class LinearProgressIndicator(float? value = null, SKColor? color = null, SKColor? trackColor = null, float height = 4, Key? key = null) : StatefulWidget(key)
{
    internal float? Value => value;
    internal SKColor? Color => color;
    internal SKColor? Track => trackColor;
    internal float Height => height;
    public override State CreateState() => new LinearProgressState();
}

sealed class LinearProgressState : TickerProviderState<LinearProgressIndicator>
{
    AnimationController? _c;

    public override void InitState() => Sync();
    public override void DidUpdateWidget(LinearProgressIndicator old) => Sync();

    void Sync()
    {
        if (Widget.Value is null && _c is null)
        {
            _c = new AnimationController(this, TimeSpan.FromMilliseconds(1800));
            _c.AddListener(() => { if (Mounted) SetState(); });
            _c.Repeat();
        }
        else if (Widget.Value is not null && _c is not null) { _c.Dispose(); _c = null; }
    }

    public override void Dispose() { _c?.Dispose(); base.Dispose(); }

    public override Widget Build(BuildContext context)
    {
        var s = Theme.Of(context).ColorScheme;
        return new SizedBox(height: Widget.Height, child: new ClipRRect(BorderRadius.Circular(Widget.Height / 2),
            new CustomPaint(new LinearProgressPainter(Widget.Value, _c?.Value ?? 0, Widget.Color ?? s.Primary, Widget.Track ?? s.SecondaryContainer))));
    }
}

sealed class LinearProgressPainter(float? value, float t, SKColor color, SKColor track) : CustomPainter
{
    public override void Paint(SKCanvas canvas, Size size)
    {
        using var tp = new SKPaint { Color = track };
        canvas.DrawRect(0, 0, size.Width, size.Height, tp);
        using var p = new SKPaint { Color = color, IsAntialias = true };
        if (value is { } v)
        {
            canvas.DrawRect(0, 0, size.Width * Math.Clamp(v, 0, 1), size.Height, p);
            return;
        }
        // Two bars chase each other: the long one leads, the short one trails.
        float Bar(float phase, float lead, float tail)
        {
            float x = Math.Clamp((t - phase) / (1 - phase), 0, 1);
            float a = Curves.EaseInOut.Transform(Math.Clamp(x / lead, 0, 1));
            float b = Curves.EaseInOut.Transform(Math.Clamp((x - (1 - tail)) / tail, 0, 1));
            canvas.DrawRect(size.Width * (-0.3f + 1.3f * b), 0, size.Width * (1.3f * (a - b) + 0.0001f), size.Height, p);
            return x;
        }
        Bar(0, 0.7f, 0.55f);
        Bar(0.4f, 0.8f, 0.5f);
    }
}

public sealed class CircularProgressIndicator(float? value = null, SKColor? color = null, float strokeWidth = 4, float size = 40, Key? key = null) : StatefulWidget(key)
{
    internal float? Value => value;
    internal SKColor? Color => color;
    internal float Stroke => strokeWidth;
    internal float Diameter => size;
    public override State CreateState() => new CircularProgressState();
}

sealed class CircularProgressState : TickerProviderState<CircularProgressIndicator>
{
    AnimationController? _c;

    public override void InitState() => Sync();
    public override void DidUpdateWidget(CircularProgressIndicator old) => Sync();

    void Sync()
    {
        if (Widget.Value is null && _c is null)
        {
            _c = new AnimationController(this, TimeSpan.FromMilliseconds(1400));
            _c.AddListener(() => { if (Mounted) SetState(); });
            _c.Repeat();
        }
        else if (Widget.Value is not null && _c is not null) { _c.Dispose(); _c = null; }
    }

    public override void Dispose() { _c?.Dispose(); base.Dispose(); }

    public override Widget Build(BuildContext context)
    {
        var s = Theme.Of(context).ColorScheme;
        return new CustomPaint(new CircularPainter(Widget.Value, _c?.Value ?? 0, Widget.Color ?? s.Primary, s.SecondaryContainer, Widget.Stroke),
            size: new Size(Widget.Diameter, Widget.Diameter));
    }
}

sealed class CircularPainter(float? value, float t, SKColor color, SKColor track, float stroke) : CustomPainter
{
    public override void Paint(SKCanvas canvas, Size size)
    {
        var rect = new SKRect(stroke / 2, stroke / 2, size.Width - stroke / 2, size.Height - stroke / 2);
        using var p = new SKPaint { Color = color, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = stroke, StrokeCap = SKStrokeCap.Round };
        if (value is { } v)
        {
            canvas.DrawArc(rect, -90, 360 * Math.Clamp(v, 0, 1), false, p);
            return;
        }
        float rotate = t * 360 * 2;
        float sweep = 30 + 240 * (0.5f - 0.5f * MathF.Cos(t * MathF.PI * 2));
        canvas.DrawArc(rect, -90 + rotate, sweep, false, p);
    }
}

// ---- navigation ----

public sealed record NavigationDestination(IconData Icon, string Label, IconData? SelectedIcon = null);

public sealed class NavigationRail(int selectedIndex, IReadOnlyList<NavigationDestination> destinations, Action<int>? onDestinationSelected = null,
    Widget? leading = null, Key? key = null) : StatelessWidget(key)
{
    public override Widget Build(BuildContext context)
    {
        var theme = Theme.Of(context);
        var s = theme.ColorScheme;
        var items = new List<Widget>();
        if (leading is not null) items.Add(new Padding(EdgeInsets.Symmetric(vertical: 8), leading));
        for (int i = 0; i < destinations.Count; i++)
        {
            int index = i;
            var d = destinations[i];
            bool sel = i == selectedIndex;
            items.Add(new Interactive((ctx, st) => new Padding(EdgeInsets.Symmetric(vertical: 6), new Column(mainAxisSize: MainAxisSize.Min, spacing: 4, children:
            [
                new AnimatedContainer(TimeSpan.FromMilliseconds(200), width: 56, height: 32, alignment: Alignment.Center, curve: Curves.EaseOutCubic,
                    decoration: new BoxDecoration(
                        Color: sel ? s.SecondaryContainer.WithOpacity(1) : StateLayer.Blend(Colors.Transparent, s.OnSurface, StateLayer.Opacity(st)),
                        BorderRadius: BorderRadius.Circular(16)),
                    child: new Icon(sel ? d.SelectedIcon ?? d.Icon : d.Icon, 24, sel ? s.OnSecondaryContainer : s.OnSurfaceVariant)),
                new Text(d.Label, style: new TextStyle(Color: sel ? s.OnSurface : s.OnSurfaceVariant, FontWeight: sel ? FontWeight.W600 : FontWeight.W500)
                    .Merge(new TextStyle(FontSize: theme.TextTheme.LabelMedium.FontSize, LetterSpacing: theme.TextTheme.LabelMedium.LetterSpacing))),
            ])), () => onDestinationSelected?.Invoke(index)));
        }
        return new Container(width: 80, color: s.Surface, padding: EdgeInsets.Symmetric(vertical: 8),
            child: new Column(mainAxisSize: MainAxisSize.Max, crossAxisAlignment: CrossAxisAlignment.Center, children: items));
    }
}

public sealed class NavigationBar(int selectedIndex, IReadOnlyList<NavigationDestination> destinations, Action<int>? onDestinationSelected = null, Key? key = null) : StatelessWidget(key)
{
    public override Widget Build(BuildContext context)
    {
        var theme = Theme.Of(context);
        var s = theme.ColorScheme;
        return new Container(height: 80, color: s.SurfaceContainer, child: new Row(children: destinations.Select((d, i) =>
        {
            bool sel = i == selectedIndex;
            return (Widget)new Expanded(new Interactive((ctx, st) => new Column(mainAxisAlignment: MainAxisAlignment.Center, spacing: 4, children:
            [
                new AnimatedContainer(TimeSpan.FromMilliseconds(200), width: 64, height: 32, alignment: Alignment.Center,
                    decoration: new BoxDecoration(Color: sel ? s.SecondaryContainer : StateLayer.Blend(Colors.Transparent, s.OnSurface, StateLayer.Opacity(st)), BorderRadius: BorderRadius.Circular(16)),
                    child: new Icon(sel ? d.SelectedIcon ?? d.Icon : d.Icon, 24, sel ? s.OnSecondaryContainer : s.OnSurfaceVariant)),
                new Text(d.Label, style: new TextStyle(Color: sel ? s.OnSurface : s.OnSurfaceVariant, FontWeight: sel ? FontWeight.W600 : FontWeight.W500, FontSize: 12)),
            ]), () => onDestinationSelected?.Invoke(i)));
        }).ToList()));
    }
}

// ---- dialogs and snack bars ----

public static class Dialogs
{
    /// <summary>
    /// Shows <paramref name="builder"/> above the app with a dimming scrim. The builder receives a function that closes the dialog.
    /// Returns that same function so callers can also close it. Focus is trapped inside while it is open, Escape closes it (when
    /// <paramref name="barrierDismissible"/>), and focus returns to where it was when it closes.
    /// </summary>
    public static Action Show(BuildContext context, Func<BuildContext, Action, Widget> builder, bool barrierDismissible = true)
    {
        var overlay = Overlay.Of(context);
        var previousFocus = WidgetsBinding.Instance.Focus.Primary;
        OverlayEntry? entry = null;
        void Close()
        {
            if (entry is null) return;
            entry.Remove();
            entry = null;
            if (previousFocus is { Element.Mounted: true }) previousFocus.RequestFocus();
        }
        entry = new OverlayEntry(ctx =>
        {
            var s = Theme.Of(context).ColorScheme;
            return Wrap(context, new Focus(autofocus: true, trapFocus: true, skipTraversal: true,
                onKey: e => e.IsDown && e.Key == "Escape" && barrierDismissible && CloseAndConsume(Close),
                child: new TweenAnimationBuilder<float>(new FloatTween(0, 1), TimeSpan.FromMilliseconds(180),
                    (_, t, __) => new Stack([
                        Positioned.Fill(new GestureDetector(onTap: barrierDismissible ? Close : null, behavior: HitTestBehavior.Opaque,
                            child: new ColoredBox(s.Scrim.WithOpacity(0.32f * t)))),
                        new Center(new Opacity(t, Transform.Scale(0.92f + 0.08f * t, builder(ctx, Close)))),
                    ], fit: StackFit.Expand, clip: false), curve: Curves.EaseOutCubic)));
        });
        overlay.Insert(entry);
        return Close;
    }

    static bool CloseAndConsume(Action close)
    {
        close();
        return true;
    }

    // Overlay entries sit outside the app's theme/text scope, so re-establish them from the opening context.
    internal static Widget Wrap(BuildContext origin, Widget child)
    {
        var theme = Theme.Of(origin);
        return new Theme(theme, new IconTheme(theme.ColorScheme.OnSurfaceVariant, 24,
            new DefaultTextStyle(theme.TextTheme.BodyMedium, new Directionality(Directionality.Of(origin), child))));
    }

    // Snack bars show one at a time; the rest wait their turn.
    static readonly Queue<Func<Action>> PendingSnackBars = new();
    static bool _snackBarShowing;

    /// <summary>Shows a snack bar, or queues it if one is already visible. Returns a function that dismisses (or cancels) this one.</summary>
    public static Action ShowSnackBar(BuildContext context, string message, string? actionLabel = null, Action? onAction = null, TimeSpan? duration = null)
    {
        bool cancelled = false;
        Action? closeVisible = null;

        void Present()
        {
            _snackBarShowing = true;
            closeVisible = PresentSnackBar(context, message, actionLabel, onAction, duration, () =>
            {
                _snackBarShowing = false;
                // A cancelled entry presents nothing, so keep going until one is actually shown.
                while (!_snackBarShowing && PendingSnackBars.TryDequeue(out var next)) next();
            });
        }

        if (_snackBarShowing)
            PendingSnackBars.Enqueue(() =>
            {
                if (!cancelled) Present();
                return () => { };
            });
        else Present();

        return () =>
        {
            cancelled = true;
            closeVisible?.Invoke();
        };
    }

    static Action PresentSnackBar(BuildContext context, string message, string? actionLabel, Action? onAction, TimeSpan? duration, Action onClosed)
    {
        var overlay = Overlay.Of(context);
        OverlayEntry? entry = null;
        Action timer = null!;
        void Close()
        {
            if (entry is null) return;
            WidgetsBinding.Instance.CancelTimer(timer);
            entry.Remove();
            entry = null;
            onClosed();
        }
        timer = Close;
        entry = new OverlayEntry(ctx =>
        {
            var theme = Theme.Of(context);
            var s = theme.ColorScheme;
            return Wrap(context, new Positioned(new Align(Alignment.BottomCenter, new Padding(EdgeInsets.All(16),
                new TweenAnimationBuilder<float>(new FloatTween(0, 1), TimeSpan.FromMilliseconds(200), (_, t, __) => new Opacity(t,
                    new FractionalTranslation(new Offset(0, 0.4f * (1 - t)), new ConstrainedBox(new BoxConstraints(0, 560, 0, float.PositiveInfinity),
                        new Material(new Padding(EdgeInsets.Symmetric(16, 0), new ConstrainedBox(BoxConstraints.TightFor(height: 48), new Row(children:
                        [
                            new Expanded(new Text(message, style: theme.TextTheme.BodyMedium.Merge(new TextStyle(Color: s.OnInverseSurface)))),
                            ..actionLabel is null ? Array.Empty<Widget>() : [new TextButton(new Text(actionLabel), () => { onAction?.Invoke(); Close(); }, color: s.InversePrimary)],
                        ]))), s.InverseSurface, 3, BorderRadius.Circular(Shapes.ExtraSmall))))), curve: Curves.EaseOutCubic))),
                left: 0, top: 0, right: 0, bottom: 0));
        });
        overlay.Insert(entry);
        WidgetsBinding.Instance.ScheduleTimer(duration ?? TimeSpan.FromSeconds(4), timer);
        return Close;
    }
}

public sealed class AlertDialog(Widget? title = null, Widget? content = null, IReadOnlyList<Widget>? actions = null, IconData? icon = null, Key? key = null) : StatelessWidget(key)
{
    public override Widget Build(BuildContext context)
    {
        var theme = Theme.Of(context);
        var s = theme.ColorScheme;
        return new ConstrainedBox(new BoxConstraints(280, 560, 0, float.PositiveInfinity), new Material(new Padding(EdgeInsets.All(24),
            new Column(mainAxisSize: MainAxisSize.Min, crossAxisAlignment: CrossAxisAlignment.Start, children:
            [
                ..icon is null ? Array.Empty<Widget>() : [new Align(Alignment.Center, new Padding(EdgeInsets.Only(bottom: 16), new Icon(icon, 24, s.Secondary)))],
                ..title is null ? Array.Empty<Widget>() : [new Padding(EdgeInsets.Only(bottom: 16),
                    DefaultTextStyle.Merge(context, theme.TextTheme.HeadlineSmall.Merge(new TextStyle(Color: s.OnSurface)), title))],
                ..content is null ? Array.Empty<Widget>() : [DefaultTextStyle.Merge(context, theme.TextTheme.BodyMedium.Merge(new TextStyle(Color: s.OnSurfaceVariant)), content)],
                ..actions is null ? Array.Empty<Widget>() : [new Padding(EdgeInsets.Only(top: 24), new Align(Alignment.CenterRight, new Row(mainAxisSize: MainAxisSize.Min, spacing: 8, children: actions)))],
            ])), s.SurfaceContainerHigh, 3, BorderRadius.Circular(Shapes.ExtraLarge)));
    }
}
