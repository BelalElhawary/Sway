using SkiaSharp;
using Sway.Widgets;

namespace Sway.Example;

/// <summary>The demo app: a Material 3 shell with a navigation rail, theme-mode, seed-colour and text-direction switches.</summary>
public class DemoRoot(string page = "components", bool dark = false, bool rtl = false) : StatefulWidget
{
    public string Page => page;
    public bool Dark => dark;
    public bool Rtl => rtl;
    public override State CreateState() => new DemoRootState();
}

class DemoRootState : State<DemoRoot>
{
    static readonly (string id, string label, IconData icon)[] Pages =
    [
        ("components", "Home", Icons.Home), ("layout", "Layout", Icons.Menu), ("forms", "Forms", Icons.Edit),
        ("motion", "Motion", Icons.Star), ("effects", "Effects", Icons.Favorite), ("rtl", "RTL", Icons.ArrowForward), ("stress", "Stress", Icons.MoreVert),
        ("media", "Media", Icons.PlayArrow),
    ];

    /// <summary>Below this width (logical px) the shell drops the navigation rail and tightens page padding.</summary>
    public const float CompactWidth = 600;

    static readonly SKColor?[] Seeds = [null, Colors.FromRgb(0x006A6A), Colors.FromRgb(0xB3261E), Colors.FromRgb(0x1B6EF3), Colors.FromRgb(0x386A20)];

    string _page = "";
    ThemeMode _mode;
    bool _rtl;
    int _seed;

    public override void InitState()
    {
        _page = Widget.Page;
        _mode = Widget.Dark ? ThemeMode.Dark : ThemeMode.Light;
        _rtl = Widget.Rtl;
    }

    ThemeData Theme(Brightness b) => Seeds[_seed] is { } seed ? ThemeData.FromSeed(seed, b) : b == Brightness.Dark ? ThemeData.Dark() : ThemeData.Light();

    Widget Body() => _page switch
    {
        "layout" => new LayoutPage(),
        "forms" => new FormsPage(),
        "motion" => new MotionPage(),
        "effects" => new EffectsPage(),
        "rtl" => new RtlPage(),
        "stress" => new StressPage(),
        "media" => new MediaPage(),
        _ => new ComponentsPage(),
    };

    public override Widget Build(BuildContext context)
    {
        bool dark = _mode == ThemeMode.Dark;
        return new MaterialApp(
            themeMode: _mode, theme: Theme(Brightness.Light), darkTheme: Theme(Brightness.Dark),
            textDirection: _rtl ? TextDirection.Rtl : TextDirection.Ltr,
            home: new LayoutBuilder((ctx, box) =>
            {
                bool compact = box.MaxWidth < CompactWidth;
                int selected = Array.FindIndex(Pages, p => p.id == _page);
                return new Scaffold(
                    appBar: new AppBar(title: new Text("Sway Widgets"), actions:
                    [
                        new IconButton(new Icon(Icons.Star), () => SetState(() => _seed = (_seed + 1) % Seeds.Length)),
                        new IconButton(new Icon(dark ? Icons.LightMode : Icons.DarkMode), () => SetState(() => _mode = dark ? ThemeMode.Light : ThemeMode.Dark)),
                        new TextButton(new Text(_rtl ? "LTR" : "RTL"), () => SetState(() => _rtl = !_rtl)),
                    ]),
                    // A rail needs ~80px of the width; on a phone the pages move to a scrolling strip of chips under the app bar instead.
                    navigationRail: compact ? null : new NavigationRail(selected,
                        Pages.Select(p => new NavigationDestination(p.icon, p.label)).ToList(), i => SetState(() => _page = Pages[i].id)),
                    body: compact ? new Column(crossAxisAlignment: CrossAxisAlignment.Stretch, children:
                    [
                        new SingleChildScrollView(scrollDirection: Axis.Horizontal, padding: EdgeInsets.Symmetric(12, 4), child: new Row(spacing: 8, children:
                            Pages.Select(p => (Widget)new Chip(new Text(p.label), () => SetState(() => _page = p.id), selected: p.id == _page, icon: p.icon)).ToList())),
                        new Expanded(Body()),
                    ]) : Body());
            }));
    }
}

static class Ui
{
    /// <summary>A titled card section used by the demo pages.</summary>
    public static Widget Section(BuildContext context, string title, Widget body, string? subtitle = null)
    {
        var theme = Theme.Of(context);
        return new Card(variant: CardVariant.Outlined, margin: EdgeInsets.Zero, child: new Padding(EdgeInsets.All(16), new Column(
            crossAxisAlignment: CrossAxisAlignment.Start, mainAxisSize: MainAxisSize.Min, spacing: 12, children:
            [
                new Text(title, style: theme.TextTheme.TitleMedium),
                ..subtitle is null ? Array.Empty<Widget>() : [new Text(subtitle, style: theme.TextTheme.BodySmall.Merge(new TextStyle(Color: theme.ColorScheme.OnSurfaceVariant)))],
                body,
            ])));
    }

    public static bool IsCompact(BoxConstraints box) => box.MaxWidth < DemoRootState.CompactWidth;

    public static Widget Page(string title, IReadOnlyList<Widget> sections) => new LayoutBuilder((ctx, box) => new SingleChildScrollView(padding: EdgeInsets.All(IsCompact(box) ? 12 : 24),
        child: new Column(crossAxisAlignment: CrossAxisAlignment.Stretch, mainAxisSize: MainAxisSize.Min, spacing: 16, children:
        [
            new Text(title, style: Theme.Of(ctx).TextTheme.HeadlineMedium),
            ..sections,
        ])));

    public static Widget Box(string label, SKColor color, float? w = null, float? h = null, SKColor? fg = null) =>
        new Container(width: w, height: h, padding: EdgeInsets.Symmetric(10, 6),
            decoration: new BoxDecoration(Color: color, BorderRadius: BorderRadius.Circular(8)),
            // widthFactor/heightFactor make the Center shrink-wrap under loose constraints and still centre under tight ones.
            child: new Center(new Text(label, style: new TextStyle(Color: fg ?? Colors.White, FontWeight: FontWeight.W600, FontSize: 12)), 1, 1));
}
