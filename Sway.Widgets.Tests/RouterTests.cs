using Xunit;

namespace Sway.Widgets.Tests;

public class RouterTests
{
    sealed class Probe
    {
        public Harness Harness = null!;
        public MemoryLocationSource Source = null!;
        public RouterController Router = null!;
        public List<string> Built = [];

        public string[] Pages => Harness.Find<RenderParagraph>().Select(p => p.PlainText).ToArray();
    }

    static Probe Start(string location = "/", Func<RouteMatch, string?>? redirect = null, bool maintainState = true, string? initial = null)
    {
        var probe = new Probe { Source = new MemoryLocationSource(location) };
        RouteDefinition[] routes =
        [
            new("/", (_, m) => Page(probe, "home")),
            new("/products", (_, m) => Page(probe, "products")),
            new("/products/:id", (_, m) => Page(probe, $"product {m.Param("id")} {m.Query.GetValueOrDefault("color")}"), m => $"Product {m.Param("id")}"),
            new("/files/*", (_, m) => Page(probe, $"files {m.Param("*")}")),
        ];
        var router = new Router(routes, initial, probe.Source, redirect: redirect, maintainState: maintainState,
            shell: (ctx, child) => { probe.Router = Router.Of(ctx); return child; });
        probe.Harness = new Harness(router);
        return probe;
    }

    static Widget Page(Probe probe, string name)
    {
        probe.Built.Add(name);
        return new Text(name);
    }

    [Theory]
    [InlineData("/products/:id", "/products/42", true, "id=42")]
    [InlineData("/products/:id", "/products", false, "")]
    [InlineData("/products/:id", "/products/42/extra", false, "")]
    [InlineData("/files/*", "/files/a/b%20c", true, "*=a/b c")]
    [InlineData("/", "/", true, "")]
    [InlineData("/", "/x", false, "")]
    [InlineData("/a/b", "a//b/", true, "")]
    public void Patterns_match_paths(string pattern, string location, bool matches, string captured)
    {
        var route = new RouteDefinition(pattern, (_, _) => new Text(""));
        Assert.Equal(matches, route.TryMatch(location, out var match));
        if (matches) Assert.Equal(captured, string.Join("&", match.Params.Select(p => $"{p.Key}={p.Value}")));
    }

    [Fact]
    public void Query_is_parsed_and_decoded()
    {
        var route = new RouteDefinition("/p", (_, _) => new Text(""));
        Assert.True(route.TryMatch("/p?color=dark%20red&flag&x=1&x=2#top", out var match));
        Assert.Equal("dark red", match.Query["color"]);
        Assert.Equal("", match.Query["flag"]);
        Assert.Equal("2", match.Query["x"]);
        Assert.Equal("/p?color=dark%20red&flag&x=1&x=2", match.Location);
    }

    [Fact]
    public void Starts_on_the_source_location()
    {
        var p = Start("/products/7?color=red");
        Assert.Equal("/products/7", p.Router.Path);
        Assert.Contains("product 7 red", p.Pages);
        Assert.Equal("Product 7", ((MemoryLocationSource)p.Source).Title);
    }

    [Fact]
    public void Initial_location_applies_only_to_a_fresh_source()
    {
        Assert.Equal("/products", Start("/", initial: "/products").Router.Path);
        Assert.Equal("/products/3", Start("/products/3", initial: "/products").Router.Path);
    }

    [Fact]
    public void Unknown_location_shows_not_found()
    {
        var p = Start("/nope");
        Assert.Contains("Page not found: /nope", p.Pages);
    }

    [Fact]
    public void Push_adds_history_and_back_returns()
    {
        var p = Start();
        p.Router.Push("/products/1");
        p.Harness.Advance(400);
        Assert.Equal("/products/1", p.Router.Path);
        Assert.Equal("/products/1", p.Source.Current);
        Assert.Equal(1, p.Source.Index);
        Assert.True(p.Router.CanPop);

        Assert.True(p.Router.Back());
        p.Harness.Advance(400);
        Assert.Equal("/", p.Router.Path);
        Assert.Equal("/", p.Source.Current);
        Assert.False(p.Router.CanPop);
        Assert.False(p.Router.Back());
    }

    [Fact]
    public void Pushing_the_current_location_does_nothing()
    {
        var p = Start();
        p.Router.Push("/");
        Assert.Equal(0, p.Source.Index);
    }

    [Fact]
    public void Pages_underneath_keep_state_and_are_not_rebuilt()
    {
        var p = Start();
        p.Router.Push("/products");
        p.Harness.Advance(400);
        p.Router.Back();
        p.Harness.Advance(400);
        Assert.Equal(["home", "products"], p.Built);
    }

    [Fact]
    public void Pages_underneath_are_dropped_without_maintain_state()
    {
        var p = Start(maintainState: false);
        p.Router.Push("/products");
        p.Harness.Advance(400);
        Assert.Equal(["products"], p.Pages);
        p.Router.Back();
        p.Harness.Advance(400);
        Assert.Equal(["home"], p.Pages);
    }

    [Fact]
    public void Go_replaces_the_stack_but_keeps_browser_history()
    {
        var p = Start();
        p.Router.Push("/products");
        p.Router.Go("/products/9");
        p.Harness.Advance(400);
        Assert.Equal(["product 9 "], p.Pages);
        Assert.Equal(2, p.Source.Index);

        // Back lands on a page that is no longer on the stack: it is rebuilt in place.
        p.Router.Back();
        p.Harness.Advance(400);
        Assert.Equal("/products", p.Router.Path);
        Assert.Equal(["products"], p.Pages);
        Assert.Equal(1, p.Source.Index);
    }

    [Fact]
    public void Replace_rewrites_the_current_entry()
    {
        var p = Start();
        p.Router.Replace("/products");
        p.Harness.Advance(400);
        Assert.Equal("/products", p.Source.Current);
        Assert.Equal(0, p.Source.Index);
        Assert.Equal(["products"], p.Pages);
    }

    [Fact]
    public void Browser_style_changes_from_the_source_update_the_stack()
    {
        var p = Start();
        p.Source.Open("/products/5"); // a deep link arrives
        p.Harness.Advance(400);
        Assert.Equal("/products/5", p.Router.Path);
        Assert.Equal(2, p.Pages.Length);

        p.Source.Back(); // the system back gesture
        p.Harness.Advance(400);
        Assert.Equal("/", p.Router.Path);
        Assert.Equal(["home"], p.Pages);
    }

    [Fact]
    public void Redirect_rewrites_locations_everywhere()
    {
        var p = Start(redirect: m => m.Path == "/products" ? "/products/1" : null);
        p.Router.Push("/products");
        p.Harness.Advance(400);
        Assert.Equal("/products/1", p.Router.Path);
        Assert.Equal("/products/1", p.Source.Current);
    }

    [Fact]
    public void Back_button_is_handled_while_history_exists()
    {
        var p = Start();
        Assert.False(p.Harness.Binding.HandleBack());
        p.Router.Push("/products");
        Assert.True(p.Harness.Binding.HandleBack());
        p.Harness.Advance(400);
        Assert.Equal("/", p.Router.Path);
    }

    [Fact]
    public void Disposing_the_router_unhooks_the_back_handler_and_source()
    {
        var p = Start();
        p.Router.Push("/products");
        p.Harness.Binding.ReassembleRoot(new Text("gone"));
        p.Harness.Pump();
        Assert.False(p.Harness.Binding.HandleBack());
    }

    [Fact]
    public void Route_match_of_a_page_is_stable_while_it_leaves()
    {
        RouteMatch? seen = null;
        var source = new MemoryLocationSource("/");
        var router = new Router(
        [
            new("/", (_, _) => new Text("home")),
            new("/a/:id", (ctx, m) => new Builder(c => { seen = Router.MatchOf(c); return new Text(m.Param("id")!); })),
        ], source: source);
        var h = new Harness(router);
        source.Open("/a/1");
        h.Advance(400);
        Assert.Equal("1", seen!.Param("id"));
    }
}
