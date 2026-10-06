using Sway.Extras.Material3;
using Sway.Widgets;

namespace Sway.Example;

/// <summary>Routing demo: a list at <c>/navigation</c> and a detail page at <c>/navigation/:id?color=...</c> that is pushed on top of it.</summary>
static class NavigationPage
{
    static readonly string[] Colors = ["teal", "crimson", "indigo", "olive"];

    public static Widget List() => new Builder(ctx =>
    {
        var router = Router.Of(ctx);
        return Ui.Page("Navigation", [
            Ui.Section(ctx, "URL-based routes",
                new Column(crossAxisAlignment: CrossAxisAlignment.Start, spacing: 8, children:
                [
                    new Text($"Current location: {router.Location}"),
                    new Text("On the web the address bar follows every page; on desktop use Alt+Left or the mouse back button, on Android the back gesture."),
                ]),
                "Router, RouteDefinition and an ILocationSource per platform"),
            Ui.Section(ctx, "Push a detail page",
                new Column(crossAxisAlignment: CrossAxisAlignment.Stretch, children:
                    Enumerable.Range(1, 4).Select(i => (Widget)new ListTile(
                        new Text($"Item {i}"), new Text($"/navigation/{i}?color={Colors[i - 1]}"),
                        leading: new Icon(Icons.Link), trailing: new Icon(Icons.ChevronRight),
                        onTap: () => router.Push($"/navigation/{i}?color={Colors[i - 1]}"))).ToList()),
                "Back returns to this page with its scroll position and state intact"),
        ]);
    });

    public static Widget Detail(BuildContext context, RouteMatch match)
    {
        var router = Router.Of(context);
        int id = int.TryParse(match.Param("id"), out var n) ? n : 0;
        return Ui.Page($"Item {id}", [
            Ui.Section(context, "Route values",
                new Column(crossAxisAlignment: CrossAxisAlignment.Start, spacing: 8, children:
                [
                    new Text($"Path: {match.Path}"),
                    new Text($"id = {match.Param("id")}"),
                    new Text($"color = {match.Query.GetValueOrDefault("color") ?? "(none)"}"),
                ])),
            Ui.Section(context, "Navigate",
                new Wrap(spacing: 8, runSpacing: 8, children:
                [
                    new FilledButton(new Text("Back"), () => router.Back(), icon: Icons.ArrowBack),
                    new FilledButton(new Text("Next item"), () => router.Push($"/navigation/{id + 1}?color={Colors[id % Colors.Length]}")),
                    new TextButton(new Text("Replace with item 1"), () => router.Replace("/navigation/1?color=teal")),
                    new TextButton(new Text("Go to Material"), () => router.Go("/material")),
                ])),
        ]);
    }
}
