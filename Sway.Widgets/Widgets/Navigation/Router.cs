namespace Sway.Widgets;

/// <summary>
/// URL-based navigation that behaves the same on every host. The route table maps locations (<c>/products/:id</c>) to pages; the
/// <see cref="ILocationSource"/> keeps the history and, in the browser, the address bar, so back, forward, reload and pasted links work.
/// Navigate with <see cref="Of"/>: <c>Router.Of(context).Push("/products/42")</c>.
/// <list type="bullet">
/// <item><description><b>Push</b> opens a page on top of the current one (a detail screen); Back returns to what was underneath.</description></item>
/// <item><description><b>Go</b> switches to a top-level destination: it adds a history entry but replaces the whole page stack.</description></item>
/// <item><description><b>Replace</b> swaps the current page and its history entry.</description></item>
/// </list>
/// </summary>
/// <param name="routes">The route table; the first matching entry wins.</param>
/// <param name="initialLocation">Where to start when the host did not supply a location (the source is still at <c>/</c>); a browser opened on a real URL keeps it.</param>
/// <param name="source">The history to use; the host's <see cref="AppLocation.Source"/> when null.</param>
/// <param name="shell">Wraps the pages with persistent chrome (navigation rail, app bar); receives the page stack as its child and can call <see cref="Of"/>.</param>
/// <param name="pageBuilder">Wraps each page, for a background or a page-level theme; runs inside the router's context.</param>
/// <param name="notFound">The page for a location no route matches.</param>
/// <param name="redirect">Called for every location before it is shown; return another location to redirect (a sign-in guard), or null to continue.</param>
/// <param name="maintainState">Keep the pages underneath alive, with their state, while another page covers them.</param>
public sealed class Router(IReadOnlyList<RouteDefinition> routes, string? initialLocation = null, ILocationSource? source = null,
    Func<BuildContext, Widget, Widget>? shell = null, Func<BuildContext, Widget, Widget>? pageBuilder = null, RouteBuilder? notFound = null,
    Func<RouteMatch, string?>? redirect = null, PageTransition transition = PageTransition.Slide, bool maintainState = true,
    TimeSpan? transitionDuration = null, Key? key = null) : StatefulWidget(key)
{
    internal IReadOnlyList<RouteDefinition> Routes => routes;
    internal string? InitialLocation => initialLocation;
    internal ILocationSource? Source => source;
    internal Func<BuildContext, Widget, Widget>? Shell => shell;
    internal Func<BuildContext, Widget, Widget>? PageBuilder => pageBuilder;
    internal RouteBuilder? NotFound => notFound;
    internal Func<RouteMatch, string?>? Redirect => redirect;
    internal PageTransition Transition => transition;
    internal bool MaintainState => maintainState;
    internal TimeSpan Duration => transitionDuration ?? TimeSpan.FromMilliseconds(260);

    /// <summary>The router above <paramref name="context"/>; rebuilds the caller whenever the location changes.</summary>
    public static RouterController Of(BuildContext context) =>
        context.DependOn<RouterScope>()?.Controller ?? throw new InvalidOperationException("No Router above this context.");

    public static RouterController? MaybeOf(BuildContext context) => context.DependOn<RouterScope>()?.Controller;

    /// <summary>The route that built the page <paramref name="context"/> is in. Unlike <see cref="RouterController.Match"/> it stays fixed while the page animates out.</summary>
    public static RouteMatch MatchOf(BuildContext context) =>
        context.DependOn<PageScope>()?.Match ?? Of(context).Match;

    public override State CreateState() => new RouterState();
}

/// <summary>Navigates the <see cref="Router"/> it came from.</summary>
public sealed class RouterController
{
    readonly RouterState _state;
    internal RouterController(RouterState state) => _state = state;

    /// <summary>The visible page's location, path and query.</summary>
    public RouteMatch Match => _state.TopMatch;
    public string Location => Match.Location;
    public string Path => Match.Path;
    public IReadOnlyDictionary<string, string> Query => Match.Query;

    /// <summary>True when there is a history entry to go back to.</summary>
    public bool CanPop => _state.CanPop;

    public void Push(string location) => _state.Push(location);
    public void Go(string location) => _state.Go(location);
    public void Replace(string location) => _state.ReplaceTop(location);

    /// <summary>Goes back one history entry. Returns false when there is none (the first page).</summary>
    public bool Back() => _state.Back();
}

sealed class RouterScope(RouterController controller, int version, Widget child) : InheritedWidget(child)
{
    public RouterController Controller => controller;
    public int Version => version;
    public override bool UpdateShouldNotify(InheritedWidget oldWidget) => ((RouterScope)oldWidget).Version != version;
}

sealed class PageScope(RouteMatch match, Widget child) : InheritedWidget(child)
{
    public RouteMatch Match => match;
    public override bool UpdateShouldNotify(InheritedWidget oldWidget) => !ReferenceEquals(((PageScope)oldWidget).Match, match);
}

sealed class PageEntry(int id, int index, RouteMatch match, RouteDefinition? definition, PageTransition transition, Widget content)
{
    public int Id => id;
    /// <summary>The history ordinal this page was opened at.</summary>
    public int Index => index;
    public RouteMatch Match => match;
    public RouteDefinition? Definition => definition;
    public PageTransition Transition => transition;
    public Widget Content => content;
    public AnimationController Controller = null!;
    public CurvedAnimation Curved = null!;
    public Animation<Offset> Slide = null!;
    public OffsetTween SlideTween = null!;
    /// <summary>Animating out after a pop; removed once dismissed.</summary>
    public bool Removing;
    /// <summary>Pages this one replaces; they stay underneath until this one has fully appeared.</summary>
    public List<PageEntry>? Replaces;
}

sealed class RouterState : TickerProviderState<Router>
{
    const int MaxRedirects = 8;

    readonly List<PageEntry> _pages = [];
    ILocationSource _source = null!;
    RouterController _controller = null!;
    Func<bool> _backHandler = null!;
    int _nextId, _version;

    public RouteMatch TopMatch => Top.Match;
    public bool CanPop => _source.Index > 0;
    PageEntry Top => _pages.Last(p => !p.Removing);

    public override void InitState()
    {
        _source = Widget.Source ?? AppLocation.Source;
        _controller = new RouterController(this);

        RouteMatch.Normalize(_source.Current, out var currentPath, out _);
        if (Widget.InitialLocation is { } initial && currentPath == "/") _source.Replace(initial);
        string location = Redirected(_source.Current);
        if (location != _source.Current) _source.Replace(location);

        _pages.Add(CreatePage(location, _source.Index, PageTransition.None));
        _source.Changed += OnSourceChanged;
        _backHandler = Back;
        WidgetsBinding.Instance.AddBackHandler(_backHandler);
        SyncTitle();
    }

    public override void Dispose()
    {
        _source.Changed -= OnSourceChanged;
        WidgetsBinding.Instance.RemoveBackHandler(_backHandler);
        foreach (var p in _pages) Release(p);
        _pages.Clear();
        base.Dispose();
    }

    // ---- navigation ----

    public void Push(string location)
    {
        location = Redirected(location);
        if (location == Top.Match.Location) return;
        _source.Push(location);
        _pages.Add(CreatePage(location, _source.Index, PageFor(location)));
        Commit();
    }

    public void Go(string location)
    {
        location = Redirected(location);
        if (location == Top.Match.Location && _pages.Count(p => !p.Removing) == 1) return;
        _source.Push(location);
        AddReplacing(location, _source.Index, PageTransition.Fade, _pages.Where(p => !p.Removing).ToList());
    }

    public void ReplaceTop(string location)
    {
        location = Redirected(location);
        if (location == Top.Match.Location) return;
        _source.Replace(location);
        AddReplacing(location, _source.Index, PageTransition.Fade, [Top]);
    }

    public bool Back()
    {
        if (_source.Index <= 0) return false;
        _source.Back(); // the source answers through OnSourceChanged, which updates the stack
        return true;
    }

    /// <summary>History moved without us asking: browser back/forward, an address-bar edit, a deep link.</summary>
    void OnSourceChanged(LocationChange change)
    {
        var top = Top;
        string location = Redirected(change.Location);
        if (location != change.Location) _source.Replace(location);

        if (change.Index == top.Index)
        {
            if (location != top.Match.Location) AddReplacing(location, change.Index, PageTransition.Fade, [top]);
            return;
        }

        var existing = _pages.FirstOrDefault(p => !p.Removing && p.Index == change.Index);
        if (existing is not null)
        {
            // Back to a page that is still on the stack: pop what is above it.
            foreach (var p in _pages.Where(p => !p.Removing && p.Index > existing.Index).ToList()) Pop(p);
            Commit();
        }
        else if (change.Index > top.Index)
        {
            _pages.Add(CreatePage(location, change.Index, PageFor(location)));
            Commit();
        }
        else
        {
            // Back to a page that is not on the stack (after Go, a reload, or a restored session): show it in place of everything.
            AddReplacing(location, change.Index, PageTransition.Fade, _pages.Where(p => !p.Removing).ToList());
        }
    }

    // ---- pages ----

    (RouteDefinition?, RouteMatch) Resolve(string location)
    {
        foreach (var route in Widget.Routes)
            if (route.TryMatch(location, out var match)) return (route, match);
        return (null, RouteMatch.Unmatched(location));
    }

    string Redirected(string location)
    {
        for (int i = 0; i < MaxRedirects; i++)
        {
            var (_, match) = Resolve(location);
            if (Widget.Redirect?.Invoke(match) is not { } to) return match.Location;
            location = to;
        }
        return Resolve(location).Item2.Location;
    }

    PageTransition PageFor(string location) => Resolve(location).Item1?.Transition ?? Widget.Transition;

    PageEntry CreatePage(string location, int index, PageTransition transition)
    {
        var (definition, match) = Resolve(location);
        var builder = definition?.Builder ?? Widget.NotFound ?? DefaultNotFound;
        var entry = new PageEntry(_nextId++, index, match, definition, transition, new Builder(ctx => builder(ctx, match)));
        entry.Controller = new AnimationController(this, Widget.Duration, value: transition == PageTransition.None ? 1 : 0);
        entry.Curved = new CurvedAnimation(entry.Controller, Curves.EaseOutCubic, Curves.EaseInCubic);
        entry.SlideTween = new OffsetTween(new Offset(0.1f, 0), Offset.Zero);
        entry.Slide = entry.SlideTween.Animate(entry.Curved);
        entry.Controller.AddStatusListener(status => OnStatus(entry, status));
        if (transition != PageTransition.None) entry.Controller.Forward();
        return entry;
    }

    void AddReplacing(string location, int index, PageTransition transition, List<PageEntry> replaced)
    {
        var entry = CreatePage(location, index, transition);
        entry.Replaces = replaced;
        _pages.Add(entry);
        if (transition == PageTransition.None) FinishReplacing(entry);
        Commit();
    }

    void OnStatus(PageEntry entry, AnimationStatus status)
    {
        if (status == AnimationStatus.Completed && !entry.Removing)
        {
            FinishReplacing(entry);
            if (Mounted) SetState(); // pages underneath may now be offstage
        }
        else if (status == AnimationStatus.Dismissed && entry.Removing)
        {
            Remove(entry);
            if (Mounted) SetState();
        }
    }

    void FinishReplacing(PageEntry entry)
    {
        if (entry.Replaces is null) return;
        foreach (var old in entry.Replaces) Remove(old);
        entry.Replaces = null;
    }

    void Pop(PageEntry entry)
    {
        entry.Removing = true;
        if (entry.Transition == PageTransition.None || entry.Controller.Value == 0) Remove(entry);
        else entry.Controller.Reverse();
    }

    void Remove(PageEntry entry)
    {
        if (_pages.Remove(entry)) Release(entry);
    }

    static void Release(PageEntry entry)
    {
        entry.Curved.Dispose();
        entry.Controller.Dispose();
    }

    void Commit()
    {
        _version++;
        SyncTitle();
        SetState();
    }

    void SyncTitle()
    {
        var top = Top;
        if (top.Definition?.Title?.Invoke(top.Match) is { } title) _source.SetTitle(title);
    }

    static Widget DefaultNotFound(BuildContext context, RouteMatch match) => new Center(new Text($"Page not found: {match.Path}"));

    // ---- build ----

    public override Widget Build(BuildContext context)
    {
        bool rtl = Directionality.Of(context) == TextDirection.Rtl;
        var children = new List<Widget>(_pages.Count);
        for (int i = 0; i < _pages.Count; i++)
        {
            var page = _pages[i];
            bool covered = false;
            for (int j = i + 1; j < _pages.Count && !covered; j++)
                covered = !_pages[j].Removing && _pages[j].Controller.Status == AnimationStatus.Completed;
            if (covered && !Widget.MaintainState) continue;

            Widget content = new PageScope(page.Match, page.Content);
            page.SlideTween.Begin = new Offset(rtl ? -0.1f : 0.1f, 0);
            // A pushed page slides in with its own (opaque) background while its content fades; a replacement cross-fades as a whole.
            if (page.Transition == PageTransition.Slide)
            {
                content = new FadeTransition(page.Curved, content);
                if (Widget.PageBuilder is { } wrapSlide) content = wrapSlide(context, content);
                content = new SlideTransition(page.Slide, content);
            }
            else
            {
                if (Widget.PageBuilder is { } wrap) content = wrap(context, content);
                if (page.Transition == PageTransition.Fade) content = new FadeTransition(page.Curved, content);
            }
            children.Add(new KeyedSubtree(new ValueKey<int>(page.Id), new Offstage(content, covered)));
        }

        Widget stack = new Stack(children, fit: StackFit.Expand, clip: false);
        Widget body = Widget.Shell is { } shell ? new Builder(ctx => shell(ctx, stack)) : stack;
        return new RouterScope(_controller, _version, body);
    }
}
