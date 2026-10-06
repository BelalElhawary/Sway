using SkiaSharp;
using Sway.Extras.Material3;
using Sway.Extras.Shopify;
using Sway.Widgets;

namespace Sway.Example;

sealed record Product(int Id, string Name, string Category, decimal Price, decimal? Was, double Rating, int Reviews, string? Badge, float Hue, bool SoldOut = false);

/// <summary>Product and hero pictures drawn with Skia, so the demo needs no image files or network. Real apps use <c>ImageSource.FromUrl</c> and friends.</summary>
static class ShopArt
{
    static readonly Dictionary<float, SKImage> Cache = new();

    public static SKImage Product(float hue)
    {
        if (Cache.TryGetValue(hue, out var hit)) return hit;
        const int w = 600, h = 750;
        using var bmp = new SKBitmap(w, h);
        using (var canvas = new SKCanvas(bmp))
        {
            using var bg = new SKPaint { Shader = SKShader.CreateLinearGradient(new SKPoint(0, 0), new SKPoint(0, h),
                [SKColor.FromHsv(hue, 14, 97), SKColor.FromHsv(hue, 32, 84)], SKShaderTileMode.Clamp) };
            canvas.DrawRect(0, 0, w, h, bg);
            using var soft = new SKPaint { IsAntialias = true, Color = SKColor.FromHsv(hue, 20, 100).WithAlpha(150) };
            canvas.DrawCircle(w * 0.78f, h * 0.2f, 130, soft);
            using var body = new SKPaint { IsAntialias = true, Color = SKColor.FromHsv(hue, 55, 52) };
            canvas.DrawRoundRect(new SKRoundRect(new SKRect(w * 0.27f, h * 0.3f, w * 0.73f, h * 0.84f), 46), body);
            canvas.DrawRoundRect(new SKRoundRect(new SKRect(w * 0.12f, h * 0.3f, w * 0.32f, h * 0.62f), 40), body);
            canvas.DrawRoundRect(new SKRoundRect(new SKRect(w * 0.68f, h * 0.3f, w * 0.88f, h * 0.62f), 40), body);
            using var neck = new SKPaint { IsAntialias = true, Color = SKColor.FromHsv(hue, 20, 100).WithAlpha(230) };
            canvas.DrawOval(new SKRect(w * 0.40f, h * 0.255f, w * 0.60f, h * 0.36f), neck);
        }
        return Cache[hue] = SKImage.FromBitmap(bmp);
    }

    static SKImage? _hero;

    public static SKImage Hero()
    {
        if (_hero is not null) return _hero;
        const int w = 1400, h = 1000;
        using var bmp = new SKBitmap(w, h);
        using (var canvas = new SKCanvas(bmp))
        {
            canvas.Clear(SKColors.Black);
            using var glow = new SKPaint { Shader = SKShader.CreateRadialGradient(new SKPoint(w * 0.62f, h * 0.45f), 520,
                [new SKColor(0x2E, 0x4A, 0x52), new SKColor(0x0A, 0x0A, 0x0A), SKColors.Black], [0, 0.6f, 1], SKShaderTileMode.Clamp) };
            canvas.DrawRect(0, 0, w, h, glow);
            using var ring = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2, Color = new SKColor(0x9D, 0xAB, 0xAD, 90) };
            for (int i = 1; i <= 4; i++) canvas.DrawCircle(w * 0.62f, h * 0.45f, 90 * i, ring);
        }
        return _hero = SKImage.FromBitmap(bmp);
    }
}

/// <summary>
/// The Shopify-style commerce components from Sway.Extras.Shopify, as a small working storefront: search, category and filter sheet, wishlist,
/// add to bag, a spotlight product with variants, and a bag with checkout summary. It adapts from phone to desktop width (try narrowing the window:
/// below 768 the header collapses, the grid goes two-up and a bottom tab bar appears). It hosts its own ShopifyApp, which follows the light/dark switch.
/// </summary>
class ShopifyPage : StatefulWidget
{
    public override State CreateState() => new ShopifyPageState();
}

sealed class ShopifyPageState : State<ShopifyPage>
{
    static readonly Product[] Products =
    [
        new(1, "Linen button-down shirt", "Tops", 59m, 79m, 4.5, 128, "Sale", 28),
        new(2, "Merino crew neck jumper", "Knitwear", 129.5m, null, 4.8, 52, "New", 160),
        new(3, "Waxed canvas tote", "Bags", 89m, null, 4.2, 31, null, 38, SoldOut: true),
        new(4, "Heavy cotton tee", "Tops", 32m, null, 4.6, 410, null, 200),
        new(5, "Ribbed cardigan", "Knitwear", 98m, 120m, 4.4, 77, "Sale", 340),
        new(6, "Leather crossbody bag", "Bags", 149m, null, 4.9, 18, "New", 18),
        new(7, "Oversized wool coat", "Knitwear", 249m, null, 4.7, 64, null, 215),
        new(8, "Everyday poplin shirt", "Tops", 54m, null, 4.3, 203, null, 95),
    ];

    static readonly string[] Categories = ["All", "Tops", "Knitwear", "Bags"];

    readonly Dictionary<int, int> _cart = new() { [2] = 1 };
    readonly HashSet<int> _wish = [1];
    readonly TextEditingController _search = new();
    readonly FocusNode _searchNode = new() { DebugLabel = "ShopifyDemoSearch" };
    readonly ScrollController _scroll = new();
    string _category = "All";
    string _size = "M", _colour = "sand";
    int _qty = 1, _spot = 2, _tab;
    string? _notice;

    public override void InitState() => _search.Changed += OnSearch;
    public override void Dispose() => _search.Changed -= OnSearch;
    void OnSearch() { if (Mounted) SetState(); }

    Product Spot => Products.First(p => p.Id == _spot);
    int CartCount => _cart.Values.Sum();
    decimal Subtotal => _cart.Sum(kv => Products.First(p => p.Id == kv.Key).Price * kv.Value);
    decimal Shipping => Subtotal == 0 || Subtotal >= 50 ? 0 : 6m;

    IEnumerable<Product> Visible => Products.Where(p =>
        (_category == "All" || p.Category == _category) && p.Name.Contains(_search.Text.Trim(), StringComparison.OrdinalIgnoreCase));

    void AddToBag(Product p, int qty = 1) => SetState(() =>
    {
        _cart[p.Id] = Math.Min(99, _cart.GetValueOrDefault(p.Id) + qty);
        _notice = $"Added {p.Name} to your bag";
    });

    void ClearFilters() { _search.Text = ""; SetState(() => _category = "All"); }

    // ---- sheets ----

    void OpenCart(BuildContext ctx) => ShopifySheet.Show(ctx, (c, close) => new RefreshingBody(refresh => CartContent(refresh, close, c)), "Your bag");

    void OpenFilters(BuildContext ctx) => ShopifySheet.Show(ctx, (c, close) => new FilterBody(_category, Categories, v => SetState(() => _category = v), close), "Filter");

    void OpenMenu(BuildContext ctx) => ShopifySheet.Show(ctx, (c, close) => new Column(crossAxisAlignment: CrossAxisAlignment.Stretch, mainAxisSize: MainAxisSize.Min, spacing: 8, children:
        [
            ..Categories.Select(cat => (Widget)new ShopifyButton(cat, () => { SetState(() => _category = cat); close(); },
                cat == _category ? ShopifyButtonKind.Primary : ShopifyButtonKind.Outline, ShopifyButtonSize.Large, fill: true)),
        ]), "Shop");

    // ---- the bag ----

    Widget CartContent(Action refresh, Action close, BuildContext ctx)
    {
        if (_cart.Count == 0)
            return new SizedBox(height: 280, child: new ShopifyEmptyState(Icons.ShoppingBag, "Your bag is empty", "Add something you love and it will show up here.", "Continue shopping", close));
        return BagBody(refresh, close, ctx, twoColumn: false);
    }

    Widget BagBody(Action refresh, Action? close, BuildContext ctx, bool twoColumn)
    {
        void Mutate(Action change) { change(); SetState(); refresh(); }

        var lines = _cart.Select(kv =>
        {
            var p = Products.First(x => x.Id == kv.Key);
            return (Widget)new ShopifyCartLine(p.Name, p.Price, kv.Value, q => Mutate(() => _cart[p.Id] = q), () => Mutate(() => _cart.Remove(p.Id)),
                "Size M / " + (p.Id % 2 == 0 ? "Sand" : "Charcoal"), new Image(ImageSource.FromImage(ShopArt.Product(p.Hue))), key: new ValueKey<int>(p.Id));
        }).ToList();

        decimal tax = Math.Round(Subtotal * 0.08m, 2);
        Widget summary = new ShopifyOrderSummary(
            [new("Subtotal", ShopifyMoney.Format(Subtotal)), new("Shipping", Shipping == 0 ? "Free" : ShopifyMoney.Format(Shipping)), new("Estimated tax", ShopifyMoney.Format(tax))],
            ShopifyMoney.Format(Subtotal + Shipping + tax), () => { close?.Invoke(); SetState(() => _notice = "Thanks! This is a demo, so no order was placed."); });
        Widget list = new Column(crossAxisAlignment: CrossAxisAlignment.Stretch, mainAxisSize: MainAxisSize.Min, spacing: 12, children:
            [new ShopifyFreeShippingBar(Subtotal, 50m), ..lines]);

        return twoColumn
            ? new Row(crossAxisAlignment: CrossAxisAlignment.Start, spacing: 32, children: [new Expanded(list, 3), new SizedBox(width: 360, child: summary)])
            : new Column(crossAxisAlignment: CrossAxisAlignment.Stretch, mainAxisSize: MainAxisSize.Min, spacing: 16, children: [list, summary]);
    }

    // ---- page ----

    public override Widget Build(BuildContext context)
    {
        bool dark = Theme.Of(context).Brightness == Brightness.Dark;
        return new ShopifyApp(new Builder(ctx => new ShopifyResponsive((c, bp, width) => Store(c, bp, width))), themeMode: dark ? ShopifyThemeMode.Dark : ShopifyThemeMode.Light);
    }

    Widget Store(BuildContext ctx, ShopifyBreakpoint bp, float width)
    {
        bool phone = bp == ShopifyBreakpoint.Mobile;
        float gutter = ShopifyBreakpoints.Gutter(bp);
        var theme = ShopifyTheme.Of(ctx);
        var c = theme.Colors;

        // Page content sits in a centred column at most 1200 wide; the hero and bars run edge to edge.
        Widget Wide(Widget child) => new Center(new ConstrainedBox(new BoxConstraints(0, 1200, 0, float.PositiveInfinity),
            new Padding(EdgeInsets.Symmetric(horizontal: gutter), child)), heightFactor: 1);

        var visible = Visible.ToList();
        Widget grid = visible.Count == 0
            ? new SizedBox(height: 280, child: new ShopifyEmptyState(Icons.Search, "No matches", "Try a different search or clear the filters.", "Clear filters", ClearFilters))
            : new ShopifyProductGrid(visible.Select(p => (Widget)new ShopifyProductCard(p.Name, p.Price, new Image(ImageSource.FromImage(ShopArt.Product(p.Hue))), p.Was,
                badge: p.Badge, rating: p.Rating, reviewCount: p.Reviews, soldOut: p.SoldOut,
                onPressed: () => SetState(() => { _spot = p.Id; _notice = $"{p.Name} is in the spotlight below"; }),
                onAddToCart: () => AddToBag(p), wishlisted: _wish.Contains(p.Id),
                onWishlistChanged: on => SetState(() => { if (on) _wish.Add(p.Id); else _wish.Remove(p.Id); }), key: new ValueKey<int>(p.Id))).ToList());

        var spot = Spot;
        Widget photo = new AspectRatio(phone ? 1f : 0.9f, new ClipRRect(BorderRadius.Circular(12), new Image(ImageSource.FromImage(ShopArt.Product(spot.Hue)))));
        Widget details = new Column(crossAxisAlignment: CrossAxisAlignment.Stretch, mainAxisSize: MainAxisSize.Min, spacing: 16, children:
        [
            new Wrap([new ShopifyTag("Bestseller"), new ShopifyTag("Merino wool", ShopifyTagKind.Shade)], spacing: 8, runSpacing: 8),
            new Text(spot.Name, style: theme.Type.Responsive(theme.Type.DisplayMd, bp).Merge(new TextStyle(Color: c.Ink, FontSize: phone ? 30 : 40))),
            new Align(Alignment.CenterLeft, new ShopifyRating(spot.Rating, spot.Reviews)),
            new Align(Alignment.CenterLeft, new ShopifyPrice(spot.Price, spot.Was, large: true)),
            new Text("A soft, midweight crew neck knitted from extra-fine merino. Holds its shape, resists odour and layers over anything.",
                style: theme.Type.BodyMd.Merge(new TextStyle(Color: c.InkSecondary))),
            new ShopifyOptionChips<string>([new("XS", "XS"), new("S", "S"), new("M", "M"), new("L", "L", false), new("XL", "XL")], _size, v => SetState(() => _size = v), "Size"),
            new ShopifySwatchPicker<string>([new("sand", new SKColor(0xC9, 0xB8, 0x9B), "Sand"), new("char", new SKColor(0x3B, 0x3B, 0x3F), "Charcoal"),
                new("sage", new SKColor(0x9C, 0xAF, 0x9A), "Sage"), new("rust", new SKColor(0xA6, 0x5A, 0x3C), "Rust", false)], _colour, v => SetState(() => _colour = v), "Colour"),
            new Row(spacing: 12, children:
            [
                new ShopifyQuantityStepper(_qty, v => SetState(() => _qty = v)),
                new Expanded(new ShopifyButton("Add to bag", () => AddToBag(spot, _qty), size: ShopifyButtonSize.Large, icon: Icons.ShoppingBag, fill: true)),
            ]),
            new ShopifyAccordion([
                new("Details", new Text("100% extra-fine merino wool. Regular fit. Machine washable at 30 degrees.")),
                new("Shipping", new Text("Free standard delivery over $50. Orders ship within one working day.")),
                new("Returns", new Text("Free returns within 30 days, unworn and with the tags attached.")),
            ], initiallyOpen: [0]),
        ]);

        var content = new Column(crossAxisAlignment: CrossAxisAlignment.Stretch, mainAxisSize: MainAxisSize.Min, spacing: 24, children:
        [
            new SizedBox(height: phone ? 8 : 24),
            new ShopifySectionHeader("New arrivals", "Just in", "View all", ClearFilters),
            new Row(spacing: 8, children:
            [
                new Expanded(new ShopifySearchField(_search, _searchNode, "Search products")),
                new ShopifyIconButton(Icons.Tune, () => OpenFilters(ctx)),
            ]),
            new ShopifyOptionChips<string>(Categories.Select(k => new ShopifyOption<string>(k, k)).ToList(), _category, v => SetState(() => _category = v)),
            grid,
            new SizedBox(height: phone ? 16 : 40),
            phone
                ? new Column(crossAxisAlignment: CrossAxisAlignment.Stretch, mainAxisSize: MainAxisSize.Min, spacing: 20, children: [photo, details])
                : new Row(crossAxisAlignment: CrossAxisAlignment.Start, spacing: 40, children: [new Expanded(photo), new Expanded(details)]),
            new SizedBox(height: phone ? 16 : 40),
            new Text("Your bag", style: theme.Type.HeadingXl.Merge(new TextStyle(Color: c.Ink))),
            new ShopifyCheckoutSteps(["Bag", "Shipping", "Payment"], 0),
            _cart.Count == 0
                ? new SizedBox(height: 280, child: new ShopifyEmptyState(Icons.ShoppingBag, "Your bag is empty", "Add something you love and it will show up here."))
                : BagBody(() => { }, null, ctx, twoColumn: !phone),
            new SizedBox(height: phone ? 24 : 64),
        ]);

        return new Column(crossAxisAlignment: CrossAxisAlignment.Stretch, children:
        [
            new ShopifyAnnouncementBar(_notice ?? "Free shipping on orders over $50", _notice is null ? "Shop now" : null, _notice is null ? () => ClearFilters() : null,
                _notice is null ? null : () => SetState(() => _notice = null)),
            new ShopifyHeader(new Text("Northwind"), Categories.Skip(1).Append("Sale").Select(k => new ShopifyNavLink(k, () => SetState(() => _category = Categories.Contains(k) ? k : "All"))).ToList(),
                onMenu: () => OpenMenu(ctx), onSearch: () => _searchNode.RequestFocus(), onCart: () => OpenCart(ctx), cartCount: CartCount, onAccount: () => SetState(() => _notice = "Accounts aren't part of this demo")),
            new Expanded(new SingleChildScrollView(controller: _scroll, child: new Column(crossAxisAlignment: CrossAxisAlignment.Stretch, mainAxisSize: MainAxisSize.Min, children:
            [
                new ShopifyHero("Built for the way you shop", "Considered essentials in natural fibres, made to last and easy to keep.", "Shop the collection",
                    () => _scroll.JumpTo(0), eyebrow: "Spring 2026", image: new Image(ImageSource.FromImage(ShopArt.Hero()))),
                Wide(content),
            ]))),
            ..phone ? [new ShopifyBottomNavBar([new(Icons.Home, "Home"), new(Icons.Search, "Shop"), new(Icons.Favorite, "Saved", Badge: _wish.Count), new(Icons.ShoppingBag, "Bag", Badge: CartCount)],
                _tab, i =>
                {
                    if (i == 3) { OpenCart(ctx); return; }
                    SetState(() => _tab = i);
                    if (i == 0) _scroll.JumpTo(0);
                    if (i == 1) _searchNode.RequestFocus();
                })] : Array.Empty<Widget>(),
        ]);
    }
}

/// <summary>Content that rebuilds itself when a callback says the data behind it changed, for sheets whose content outlives one build.</summary>
sealed class RefreshingBody(Func<Action, Widget> build) : StatefulWidget
{
    internal Func<Action, Widget> Build => build;
    public override State CreateState() => new RefreshingBodyState();
}

sealed class RefreshingBodyState : State<RefreshingBody>
{
    public override Widget Build(BuildContext context) => Widget.Build(() => { if (Mounted) SetState(); });
}

/// <summary>The filter sheet: pick a category, then apply. It keeps its own choice until applied.</summary>
sealed class FilterBody(string initial, IReadOnlyList<string> categories, Action<string> onApply, Action close) : StatefulWidget
{
    internal string Initial => initial;
    internal IReadOnlyList<string> Categories => categories;
    internal Action<string> OnApply => onApply;
    internal Action Close => close;
    public override State CreateState() => new FilterBodyState();
}

sealed class FilterBodyState : State<FilterBody>
{
    string _value = "";
    public override void InitState() => _value = Widget.Initial;

    public override Widget Build(BuildContext context) => new Column(crossAxisAlignment: CrossAxisAlignment.Stretch, mainAxisSize: MainAxisSize.Min, spacing: 20, children:
    [
        new ShopifyOptionChips<string>(Widget.Categories.Select(k => new ShopifyOption<string>(k, k)).ToList(), _value, v => SetState(() => _value = v), "Category"),
        new ShopifyButton("Show results", () => { Widget.OnApply(_value); Widget.Close(); }, size: ShopifyButtonSize.Large, fill: true),
        new ShopifyButton("Reset", () => SetState(() => _value = Widget.Categories[0]), ShopifyButtonKind.Outline, ShopifyButtonSize.Large, fill: true),
    ]);
}
