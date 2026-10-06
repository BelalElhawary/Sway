---
sidebar_position: 6.5
---

# Navigation

`Router` gives an app pages with URLs, and it behaves the same on every host. A route table maps locations to pages, and the
host supplies the history: on the web it is the browser's address bar and History API, everywhere else it is kept in memory.

```csharp
var routes = new RouteDefinition[]
{
    new("/", (context, match) => new HomePage()),
    new("/products", (context, match) => new ProductList(), title: m => "Products"),
    new("/products/:id", (context, match) => new ProductPage(match.Param("id")!, match.Query.GetValueOrDefault("color"))),
    new("/files/*", (context, match) => new FileViewer(match.Param("*")!)),
};

App.Run(new MaterialApp(home: new Router(routes)), "Shop");
```

Patterns are literal segments, `:name` segments that capture a value, and a trailing `*` that captures the rest. Routes are tried in
order and the first match wins. Values are percent-decoded; the query string is in `match.Query`.

## Moving around

```csharp
var router = Router.Of(context);
router.Push("/products/42?color=red"); // a detail page on top; Back returns to what was underneath
router.Go("/products");                // a top-level destination: new history entry, the page stack is replaced
router.Replace("/login");              // swaps the current page and its history entry
router.Back();                         // one history entry back; false when there is none
```

`Router.Of(context)` rebuilds the caller when the location changes (use it for a selected tab). `RouterController.Location`, `Path`,
`Query` and `CanPop` describe the visible page. `Router.MatchOf(context)` is the route that built the page the context is in. `RouteLink`
turns any widget into a link.

Pages underneath a pushed page stay alive with their state (scroll position, text fields) and are rebuilt only if `maintainState: false`.
Push slides a page in, `Go` and `Replace` cross-fade; set `PageTransition` per route or on the router.

## Chrome around the pages

`shell` wraps the page stack with chrome that stays put (app bar, navigation rail) and can call `Router.Of`. `pageBuilder` wraps each page,
which is where a page background goes, since pages slide over one another.

```csharp
new Router(routes,
    shell: (context, pages) => new Scaffold(
        navigationRail: new NavigationRail(index, destinations, i => Router.Of(context).Go(paths[i])),
        body: pages),
    pageBuilder: (context, page) => new ColoredBox(Theme.Of(context).ScaffoldBackground, page),
    notFound: (context, match) => new Center(new Text("No such page")),
    redirect: match => !signedIn && match.Path.StartsWith("/account") ? "/login" : null)
```

`redirect` runs for every location, including the first one and the ones the browser reports, so it works as a sign-in guard.

## Hosts

| Host | Location | Back |
| --- | --- | --- |
| Web | The address bar. Reload, deep links, back and forward (also Alt+Left and Alt+Right) work. `title:` sets the tab title. | The browser's own buttons. |
| Android | A launching link (`https://host/products/42`) starts there; a link opened while running pushes a page. | The back button and gesture, then the activity closes when there is nothing to go back to. |
| Desktop | `App.Run(root, location: "/products/42")`; the history is in memory. | Alt+Left, Alt+Right and the mouse back and forward buttons. |
| Headless | In memory; tests pass their own `MemoryLocationSource`. | `WidgetsBinding.HandleBack()`. |

A web app needs a server that answers every path with `index.html`; `dotnet run` and the usual static hosts for Blazor WebAssembly do. To receive
Android deep links, give your activity an `IntentFilter` for the scheme or host and `LaunchMode = LaunchMode.SingleTop`. Platform code reaches the same machinery through
`WidgetsBinding.HandleBack()` and `AppLocation.Source`, so a new host only has to implement `ILocationSource`.
