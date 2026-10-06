using SkiaSharp;
using Sway.Extras.Material3;
using Sway.Widgets;

namespace Sway.Example;

/// <summary>
/// The demo app: a Material 3 shell with a navigation rail, theme-mode, seed-colour and language switches. Every page has a URL
/// (<c>/forms</c>, <c>/navigation/2</c>), served by a <see cref="Router"/>: the address bar in the browser, an in-memory history elsewhere.
/// <paramref name="page"/> pins the start page and a private history (the headless tooling); null follows the host's location.
/// </summary>
public class DemoRoot(string? page = null, bool dark = false, bool rtl = false) : StatefulWidget
{
    public string? Page => page;
    public bool Dark => dark;
    public bool Rtl => rtl;
    public override State CreateState() => new DemoRootState();
}

class DemoRootState : State<DemoRoot>
{
    static readonly (string id, string label, IconData icon)[] Pages =
    [
        ("material", "Material", Icons.Home), ("layout", "Layout", Icons.Menu), ("forms", "Forms", Icons.Edit),
        ("motion", "Motion", Icons.Star), ("effects", "Effects", Icons.Favorite), ("rtl", "Language", Icons.ArrowForward), ("stress", "Stress", Icons.MoreVert),
        ("media", "Media", Icons.PlayArrow), ("icons", "Icons", Icons.GridView), ("carbon", "Carbon", Icons.Menu), ("shopify", "Shop", Icons.ShoppingCart),
        ("navigation", "Routing", Icons.Route),
    ];

    /// <summary>Below this width (logical px) the shell drops the navigation rail and tightens page padding.</summary>
    public const float CompactWidth = 600;

    static readonly SKColor?[] Seeds = [null, Colors.FromRgb(0x006A6A), Colors.FromRgb(0xB3261E), Colors.FromRgb(0x1B6EF3), Colors.FromRgb(0x386A20)];

    ThemeMode _mode;
    bool _rtl;
    int _seed;
    MemoryLocationSource? _history;
    IReadOnlyList<RouteDefinition> _routes = [];

    public override void InitState()
    {
        _mode = Widget.Dark ? ThemeMode.Dark : ThemeMode.Light;
        _rtl = Widget.Rtl;
        if (Widget.Page is { } page) _history = new MemoryLocationSource("/" + page);
        _routes =
        [
            ..Pages.Select(p => new RouteDefinition("/" + p.id, (_, _) => Body(p.id), _ => "Sway - " + p.label)),
            new RouteDefinition("/navigation/:id", NavigationPage.Detail, m => $"Sway - Item {m.Param("id")}"),
        ];
    }

    ThemeData Theme(Brightness b) => Seeds[_seed] is { } seed ? ThemeData.FromSeed(seed, b) : b == Brightness.Dark ? ThemeData.Dark() : ThemeData.Light();

    static Widget Body(string page) => page switch
    {
        "layout" => new LayoutPage(),
        "forms" => new FormsPage(),
        "motion" => new MotionPage(),
        "effects" => new EffectsPage(),
        "rtl" => new RtlPage(),
        "stress" => new StressPage(),
        "media" => new MediaPage(),
        "icons" => new IconsPage(),
        "carbon" => new CarbonPage(),
        "shopify" => new ShopifyPage(),
        "navigation" => NavigationPage.List(),
        _ => new MaterialPage(),
    };

    /// <summary>The persistent chrome around the pages: app bar, rail (or chips on a phone) and the page stack as its body.</summary>
    Widget Shell(BuildContext context, Widget pages)
    {
        bool dark = _mode == ThemeMode.Dark;
        var router = Router.Of(context);
        // A section stays selected while its detail pages are open (/navigation/2 belongs to /navigation).
        string section = router.Path.Split('/', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
        int selected = Array.FindIndex(Pages, p => p.id == section);
        return new LayoutBuilder((ctx, box) =>
        {
            bool compact = box.MaxWidth < CompactWidth;
            return new Scaffold(
                appBar: new AppBar(title: new Text("Sway Widgets"),
                    leading: router.CanPop ? new IconButton(new Icon(Icons.ArrowBack), () => router.Back()) : null,
                    actions:
                    [
                        new IconButton(new Icon(Icons.Star), () => SetState(() => _seed = (_seed + 1) % Seeds.Length)),
                        new IconButton(new Icon(dark ? Icons.LightMode : Icons.DarkMode), () => SetState(() => _mode = dark ? ThemeMode.Light : ThemeMode.Dark)),
                        new TextButton(new Text(_rtl ? "English" : "العربية"), () => SetState(() => _rtl = !_rtl)),
                    ]),
                // A rail needs ~80px of the width; on a phone the pages move to a scrolling strip of chips under the app bar instead.
                navigationRail: compact ? null : new NavigationRail(selected,
                    Pages.Select(p => new NavigationDestination(p.icon, p.label)).ToList(), i => router.Go("/" + Pages[i].id)),
                body: compact ? new Column(crossAxisAlignment: CrossAxisAlignment.Stretch, children:
                [
                    new SingleChildScrollView(scrollDirection: Axis.Horizontal, padding: EdgeInsets.Symmetric(12, 4), child: new Row(spacing: 8, children:
                        Pages.Select(p => (Widget)new Chip(new Text(p.label), () => router.Go("/" + p.id), selected: p.id == section, icon: p.icon)).ToList())),
                    new Expanded(pages),
                ]) : pages);
        });
    }

    public override Widget Build(BuildContext context) => new MaterialApp(
        themeMode: _mode, theme: Theme(Brightness.Light), darkTheme: Theme(Brightness.Dark),
        locale: _rtl ? new Locale("ar") : new Locale("en"), localizationsDelegates: [DemoStrings.Delegate],
        home: new Router(_routes, initialLocation: "/material", source: _history, shell: Shell,
            // Pages slide over each other, so each needs its own opaque background.
            pageBuilder: (ctx, page) => new ColoredBox(Sway.Extras.Material3.Theme.Of(ctx).ScaffoldBackground, page),
            redirect: m => m.Path == "/" ? "/material" : null));
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
