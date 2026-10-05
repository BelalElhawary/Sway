using Xunit;

using Sway.Extras.Material3;
using Sway.Widgets;
using Sway.Widgets.Tests;
namespace Sway.Extras.Material3.Tests;

/// <summary>Renders widgets to a PNG so a person can look at them. Set SWAY_GALLERY to a directory to enable.</summary>
public class GalleryTests
{
    static string? Dir => Environment.GetEnvironmentVariable("SWAY_GALLERY") is { Length: > 0 } d ? d : null;

    [Fact]
    public void Components()
    {
        if (Dir is not { } dir) return;
        var page = new MaterialApp(new Scaffold(new Padding(EdgeInsets.All(16), new Column(crossAxisAlignment: CrossAxisAlignment.Start, spacing: 16, children:
        [
            new SizedBox(width: 300, child: new Slider(0.4f, _ => { }, label: v => $"{v:0.00}")),
            new SizedBox(width: 300, child: new Slider(50, _ => { }, min: 0, max: 100, divisions: 5)),
            new SizedBox(width: 300, child: new Slider(0.4f)),
            new Row(spacing: 24, children:
            [
                new Badge(new Icon(Icons.Mail, 28), label: "3"),
                new Badge(new Icon(Icons.Notifications, 28)),
                new Badge(new Icon(Icons.ShoppingCart, 28), label: "99+"),
            ]),
            new SegmentedButton<string>(
                [new ButtonSegment<string>("d", "Day"), new ButtonSegment<string>("w", "Week", Icons.Event), new ButtonSegment<string>("m", "Month")],
                new[] { "w" }, _ => { }),
            new SegmentedButton<string>(
                [new ButtonSegment<string>("a", "Bold"), new ButtonSegment<string>("b", "Italic"), new ButtonSegment<string>("c", "Underline")],
                new[] { "a", "c" }, _ => { }, multiSelectionEnabled: true),
            new SizedBox(width: 380, child: new TabBar([new Tab("Flights", Icons.Place), new Tab("Trips", Icons.Event), new Tab("Explore", Icons.Search)], 1, _ => { })),
            new SizedBox(width: 380, child: new TabBar([new Tab("Overview"), new Tab("Specs"), new Tab("Reviews")], 0, _ => { })),
            new SearchBar(hintText: "Search the library"),
            new SizedBox(width: 300, child: new SelectableText("This text can be selected with the mouse.")),
        ]))));
        var h = new Harness(page, 460, 640);
        h.Advance(300);
        h.Save(Path.Combine(dir, "components.png"));
    }

    static Harness AppWithContext(Widget body, out Func<BuildContext> context, int width, int height)
    {
        BuildContext? captured = null;
        var h = new Harness(new MaterialApp(new Builder(ctx =>
        {
            captured = ctx;
            return body;
        })), width, height);
        context = () => captured!;
        return h;
    }

    [Fact]
    public void DatePickerAndMenus()
    {
        if (Dir is not { } dir) return;
        var h = AppWithContext(new SizedBox(), out var ctx, 400, 700);
        DatePicker.Show(ctx(), new DateTime(2026, 3, 15), new DateTime(2020, 1, 1), new DateTime(2030, 12, 31), _ => { });
        h.Pump();
        h.Advance(400);
        h.Pump();
        h.Save(Path.Combine(dir, "datepicker.png"));

        var h2 = AppWithContext(new SizedBox(), out var ctx2, 400, 400);
        Menus.Show(ctx2(), new Rect(20, 20, 100, 40), new MenuEntry<string>[]
        {
            new MenuItem<string>("a", new Text("Copy"), Icon: Icons.ContentCopy),
            new MenuItem<string>("b", new Text("Paste"), Enabled: false, Icon: Icons.Download),
            new MenuDivider<string>(),
            new MenuItem<string>("c", new Text("Delete"), Icon: Icons.Remove),
        }, _ => { });
        h2.Pump();
        h2.Advance(400);
        h2.Pump();
        h2.Save(Path.Combine(dir, "menu.png"));

        var h3 = AppWithContext(new SizedBox(), out var ctx3, 400, 500);
        Panels.ShowDrawer(ctx3(), (_, close) => new NavigationDrawer(1,
            [new NavigationDestination(Icons.Home, "Home"), new NavigationDestination(Icons.Settings, "Settings"), new NavigationDestination(Icons.Mail, "Mail")],
            header: new Text("Mail")), width: 280);
        h3.Pump();
        h3.Advance(500);
        h3.Pump();
        h3.Save(Path.Combine(dir, "drawer.png"));
    }
}
