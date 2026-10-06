using SkiaSharp;
using Sway.Widgets;
using Sway.Widgets.Tests;
using Xunit;

namespace Sway.Extras.Shopify.Tests;

public class ShopifyTests
{
    static Harness Show(Widget child, int width = 360, int height = 700, ShopifyThemeData? theme = null, ShopifyThemeMode mode = ShopifyThemeMode.Light)
    {
        var h = new Harness(new ShopifyApp(new Align(Alignment.TopLeft, child), theme: theme, themeMode: mode), width, height);
        h.Gestures.PointerMove(width - 1, height - 1);
        h.Pump();
        return h;
    }

    static List<string> Texts(Harness h) => h.Find<RenderParagraph>().Select(p => p.PlainText).ToList();

    static void TapText(Harness h, string text)
    {
        var p = h.Find<RenderParagraph>().First(x => x.PlainText == text);
        var at = p.LocalToGlobal(Offset.Zero);
        h.Tap(at.Dx + p.Size.Width / 2, at.Dy + p.Size.Height / 2);
    }

    // The core harness doesn't throw on overflow, so every flex's overflow is checked here: a phone layout must never clip.
    static void AssertNoOverflow(Harness h) =>
        Assert.All(h.Find<RenderFlex>(), f => Assert.True(f.OverflowExtent < 0.5f, $"a Row/Column overflows by {f.OverflowExtent}px"));

    sealed class Host<T>(T initial, Func<T, Action<T>, Widget> build) : StatefulWidget
    {
        internal T Initial => initial;
        internal Func<T, Action<T>, Widget> Build => build;
        public override State CreateState() => new HostState<T>();
    }

    sealed class HostState<T> : State<Host<T>>
    {
        T _value = default!;
        public override void InitState() => _value = Widget.Initial;
        public override Widget Build(BuildContext context) => Widget.Build(_value, v => SetState(() => _value = v));
    }

    // ---- theme ----

    [Fact]
    public void TheTwoTracksHaveOppositeCanvases()
    {
        Assert.Equal(Brightness.Light, ShopifyThemeData.Light().Brightness);
        Assert.Equal(Brightness.Dark, ShopifyThemeData.Dark().Brightness);
        Assert.Equal(Colors.FromRgb(0xFBFBF5), ShopifyColors.Light.CanvasAlt);
        Assert.Equal(Colors.FromRgb(0x000000), ShopifyColors.Night.Canvas);
    }

    [Fact]
    public void DisplayTypeIsThinAndBodyTypeIsInter()
    {
        var t = new ShopifyType();
        Assert.Equal(330, t.DisplayXxl.FontWeight);
        Assert.Equal(96, t.DisplayXxl.FontSize);
        Assert.Contains("Neue Haas", t.DisplayMd.FontFamily);
        Assert.Contains("Inter", t.BodyMd.FontFamily);
    }

    [Theory]
    [InlineData(ShopifyBreakpoint.Mobile, 56)]
    [InlineData(ShopifyBreakpoint.Tablet, 70)]
    [InlineData(ShopifyBreakpoint.Desktop, 96)]
    public void DisplayScalesDownForSmallerScreens(ShopifyBreakpoint bp, float expected)
    {
        var t = new ShopifyType();
        Assert.Equal(expected, t.Responsive(t.DisplayXxl, bp).FontSize);
    }

    [Fact]
    public void TextBelowDisplaySizeIsNotScaled()
    {
        var t = new ShopifyType();
        Assert.Same(t.HeadingXl, t.Responsive(t.HeadingXl, ShopifyBreakpoint.Mobile));
    }

    [Theory]
    [InlineData(320, ShopifyBreakpoint.Mobile)]
    [InlineData(767, ShopifyBreakpoint.Mobile)]
    [InlineData(768, ShopifyBreakpoint.Tablet)]
    [InlineData(1023, ShopifyBreakpoint.Tablet)]
    [InlineData(1024, ShopifyBreakpoint.Desktop)]
    public void BreakpointsFollowTheDesign(float width, ShopifyBreakpoint expected) =>
        Assert.Equal(expected, ShopifyBreakpoints.For(width));

    [Fact]
    public void MoneyFormatsWithGroupingAndSign()
    {
        Assert.Equal("$1,299.00", ShopifyMoney.Format(1299m));
        Assert.Equal("-$5.50", ShopifyMoney.Format(-5.5m));
        Assert.Equal("€9.99", ShopifyMoney.Format(9.99m, "€"));
    }

    [Fact]
    public void TheAppPaintsTheCanvasForTheChosenTrack()
    {
        var light = Show(new SizedBox(), mode: ShopifyThemeMode.Light);
        using var l = light.Render();
        Assert.Equal(Colors.FromRgb(0xFBFBF5), l.GetPixel(200, 400));
        var dark = Show(new SizedBox(), mode: ShopifyThemeMode.Dark);
        using var d = dark.Render();
        Assert.Equal(Colors.FromRgb(0x000000), d.GetPixel(200, 400));
    }

    [Fact]
    public void TheBundledInterFacesBackTheTypeScale()
    {
        ShopifyFonts.Register();
        var t = new ShopifyType();
        Assert.Contains("Inter", t.BodyMd.ToFont().Typeface.FamilyName);
        Assert.Contains("Inter", t.DisplayXxl.ToFont().Typeface.FamilyName);
    }

    [Fact]
    public void TheDarkTrackNeverShowsTheGreens()
    {
        var greens = new[] { ShopifyColors.Light.Aloe, ShopifyColors.Light.Pistachio };
        Assert.DoesNotContain(ShopifyColors.Night.Aloe, greens);
        Assert.DoesNotContain(ShopifyColors.Night.Pistachio, greens);

        var h = Show(new SizedBox(width: 340, child: new Column(spacing: 8, children:
        [
            new ShopifyButton("Featured", () => { }, ShopifyButtonKind.Aloe), new ShopifyTag("New"), new ShopifyCountBadge(3),
            new ShopifyFreeShippingBar(10m, 50m), new ShopifyOrderSummary([new("Subtotal", "$10.00")], "$10.00", featured: true),
        ])), width: 360, height: 700, mode: ShopifyThemeMode.Dark);
        using var bmp = h.Render();
        for (int y = 0; y < bmp.Height; y++)
            for (int x = 0; x < bmp.Width; x++)
                Assert.DoesNotContain(bmp.GetPixel(x, y), greens);
    }

    [Fact]
    public void TextOnAFillAlwaysContrastsWithTheFill()
    {
        foreach (var c in new[] { ShopifyColors.Light, ShopifyColors.Night })
        {
            Assert.NotEqual(c.OnAloe, c.Aloe);
            Assert.NotEqual(c.OnPistachio, c.Pistachio);
            Assert.NotEqual(c.OnShade, c.Shade);
        }
    }

    // ---- buttons ----

    [Fact]
    public void ButtonCallsItsHandler()
    {
        bool hit = false;
        var h = Show(new ShopifyButton("Buy now", () => hit = true));
        TapText(h, "Buy now");
        Assert.True(hit);
    }

    [Fact]
    public void ADisabledButtonIgnoresTaps()
    {
        var h = Show(new ShopifyButton("Buy now", null));
        TapText(h, "Buy now");
        Assert.Contains("Buy now", Texts(h));
    }

    [Theory]
    [InlineData(ShopifyButtonSize.Medium)]
    [InlineData(ShopifyButtonSize.Large)]
    public void ButtonsClearTheTouchTarget(ShopifyButtonSize size)
    {
        var h = Show(new ShopifyButton("Go", () => { }, size: size));
        Assert.Contains(h.Find<RenderDecoratedBox>(), b => b.Size.Height >= ShopifyBreakpoints.TouchTarget);
    }

    // Containers with an alignment fill their parent, so shrink-wrapping controls are checked against the app's 360px width explicitly.
    [Fact]
    public void ControlsHugTheirContentInsteadOfFillingTheParent()
    {
        var button = Show(new ShopifyButton("Go", () => { }));
        Assert.Contains(button.Find<RenderDecoratedBox>(), b => Math.Abs(b.Size.Height - 44) < 0.5f && b.Size.Width < 200);

        var tag = Show(new ShopifyTag("New"));
        Assert.Contains(tag.Find<RenderDecoratedBox>(), b => b.Size.Width < 100 && b.Size.Height < 40);

        var badge = Show(new ShopifyCountBadge(3));
        Assert.Contains(badge.Find<RenderDecoratedBox>(), b => b.Size.Width < 40 && Math.Abs(b.Size.Height - 18) < 0.5f);

        var chips = Show(new ShopifyOptionChips<string>([new("S", "S"), new("M", "M")], "S", _ => { }));
        Assert.Contains(chips.Find<RenderDecoratedBox>(), b => Math.Abs(b.Size.Height - 44) < 0.5f && b.Size.Width < 100);
    }

    [Fact]
    public void AnnouncementBarIsAsTallAsItsContentOnly()
    {
        var h = Show(new SizedBox(width: 360, child: new ShopifyAnnouncementBar("Free shipping over $50", onClose: () => { })));
        Assert.Contains(h.Find<RenderDecoratedBox>(), b => Math.Abs(b.Size.Width - 360) < 0.5f && b.Size.Height < 80);
    }

    [Fact]
    public void AFilledButtonSpansTheWidthItIsGiven()
    {
        var h = Show(new SizedBox(width: 320, child: new ShopifyButton("Checkout", () => { }, fill: true)));
        Assert.Contains(h.Find<RenderDecoratedBox>(), b => Math.Abs(b.Size.Width - 320) < 0.5f);
        AssertNoOverflow(h);
    }

    [Fact]
    public void AVeryLongLabelEllipsisesInsteadOfOverflowing()
    {
        var h = Show(new SizedBox(width: 200, child: new ShopifyButton("A really quite long call to action label", () => { }, fill: true)));
        AssertNoOverflow(h);
    }

    [Fact]
    public void IconButtonIsATouchTargetAndShowsABadge()
    {
        bool hit = false;
        var h = Show(new ShopifyIconButton(Icons.ShoppingBag, () => hit = true, badge: 3));
        Assert.Contains("3", Texts(h));
        Assert.Contains(h.Find<RenderConstrainedBox>(), b => b.Size.Width >= 44 && b.Size.Height >= 44);
        h.Tap(20, 20);
        Assert.True(hit);
    }

    [Fact]
    public void BadgeCapsLargeCounts()
    {
        var h = Show(new ShopifyCountBadge(250));
        Assert.Contains("99+", Texts(h));
    }

    [Fact]
    public void TagDismissCallsItsHandler()
    {
        bool closed = false;
        var h = Show(new ShopifyTag("New", onClose: () => closed = true));
        Assert.Contains("New", Texts(h));
        // The close glyph is the last thing in the row; tap just inside the tag's right edge.
        var p = h.Find<RenderParagraph>().First();
        h.Tap(p.LocalToGlobal(Offset.Zero).Dx + p.Size.Width + 14, p.LocalToGlobal(Offset.Zero).Dy + p.Size.Height / 2);
        Assert.True(closed);
    }

    // ---- fields ----

    [Fact]
    public void TextFieldShowsLabelPlaceholderAndError()
    {
        var h = Show(new SizedBox(width: 320, child: new ShopifyTextField(label: "Email", placeholder: "you@shop.com", errorText: "Enter a valid email")));
        var t = Texts(h);
        Assert.Contains("Email", t);
        Assert.Contains("Enter a valid email", t);
        AssertNoOverflow(h);
    }

    [Fact]
    public void TextFieldHelperIsReplacedByTheError()
    {
        var h = Show(new SizedBox(width: 320, child: new ShopifyTextField(helperText: "We never share it", errorText: "Required")));
        Assert.DoesNotContain("We never share it", Texts(h));
        Assert.Contains("Required", Texts(h));
    }

    [Fact]
    public void TextFieldIsAtLeast44Tall()
    {
        var h = Show(new SizedBox(width: 320, child: new ShopifyTextField()));
        Assert.Contains(h.Find<RenderDecoratedBox>(), b => b.Size.Height >= ShopifyBreakpoints.TouchTarget);
    }

    [Fact]
    public void SearchFieldClearsItsText()
    {
        var c = new TextEditingController { Text = "shoes" };
        string? last = null;
        var h = Show(new SizedBox(width: 320, child: new ShopifySearchField(c, onChanged: v => last = v)));
        // The clear button is the 44px square at the end of the field.
        h.Tap(320 - 22, 24);
        Assert.Equal("", c.Text);
        Assert.Equal("", last);
    }

    // ---- commerce ----

    [Fact]
    public void PriceShowsTheOriginalStruckThroughWhenOnSale()
    {
        var h = Show(new ShopifyPrice(79m, 120m));
        Assert.Contains("$79.00", Texts(h));
        Assert.Contains("$120.00", Texts(h));
    }

    [Fact]
    public void PriceHidesAHigherOrEqualCompareAt()
    {
        var h = Show(new ShopifyPrice(79m, 60m));
        Assert.DoesNotContain("$60.00", Texts(h));
    }

    [Fact]
    public void RatingShowsValueAndCount()
    {
        var h = Show(new ShopifyRating(4.5, 128));
        Assert.Contains("4.5 (128)", Texts(h));
    }

    [Fact]
    public void StepperIncrementsAndDecrements()
    {
        var h = Show(new Host<int>(2, (v, set) => new ShopifyQuantityStepper(v, set)));
        Assert.Contains("2", Texts(h));
        h.Tap(44 * 2 + 28 - 22 + 22, 22); // plus target: after the minus (44) and the value (28)
        Assert.Contains("3", Texts(h));
        h.Tap(22, 22);
        h.Tap(22, 22);
        Assert.Contains("1", Texts(h));
    }

    [Fact]
    public void StepperStopsAtItsBounds()
    {
        int? last = null;
        var h = Show(new ShopifyQuantityStepper(1, v => last = v));
        h.Tap(22, 22); // minus at the minimum is disabled
        Assert.Null(last);
        var h2 = Show(new ShopifyQuantityStepper(5, v => last = v, max: 5));
        h2.Tap(44 + 28 + 22, 22);
        Assert.Null(last);
    }

    [Fact]
    public void StepperTargetsAre44Square()
    {
        var h = Show(new ShopifyQuantityStepper(1, _ => { }));
        Assert.True(h.Find<RenderConstrainedBox>().Count(b => Math.Abs(b.Size.Width - 44) < 0.5f && Math.Abs(b.Size.Height - 44) < 0.5f) >= 2);
    }

    [Fact]
    public void ProductCardPressAndAddToCartAreIndependent()
    {
        bool opened = false, added = false;
        var h = Show(new SizedBox(width: 170, child: new ShopifyProductCard("Linen shirt", 59m, onPressed: () => opened = true, onAddToCart: () => added = true)));
        TapText(h, "Linen shirt");
        Assert.True(opened);
        Assert.False(added);
        TapText(h, "Add to cart");
        Assert.True(added);
    }

    [Fact]
    public void WishlistHeartTogglesWithoutOpeningTheProduct()
    {
        bool opened = false;
        bool? wish = null;
        var h = Show(new SizedBox(width: 170, child: new ShopifyProductCard("Linen shirt", 59m, onPressed: () => opened = true, wishlisted: false, onWishlistChanged: v => wish = v)));
        // The heart sits in the photo's top-right corner.
        h.Tap(170 - 22, 22);
        Assert.Equal(true, wish);
        Assert.False(opened);
    }

    [Fact]
    public void ASoldOutProductCannotBeAdded()
    {
        bool added = false;
        var h = Show(new SizedBox(width: 170, child: new ShopifyProductCard("Linen shirt", 59m, soldOut: true, onAddToCart: () => added = true)));
        Assert.Contains("Sold out", Texts(h));
        Assert.DoesNotContain("Add to cart", Texts(h));
        TapText(h, "Sold out");
        Assert.False(added);
    }

    [Fact]
    public void ProductCardFitsATwoUpPhoneGrid()
    {
        var h = Show(new SizedBox(width: 164, child: new ShopifyProductCard("A very long product name that wraps over several lines", 1299m, compareAtPrice: 1499m,
            rating: 4.5, reviewCount: 1280, badge: "Sale", wishlisted: false, onAddToCart: () => { })));
        AssertNoOverflow(h);
    }

    static List<Widget> Cards(int n) => Enumerable.Range(0, n).Select(i => (Widget)new SizedBox(height: 40, child: new Text($"item{i}"))).ToList();

    static int Columns(Harness h)
    {
        var xs = h.Find<RenderParagraph>().Where(p => p.PlainText.StartsWith("item")).Select(p => MathF.Round(p.LocalToGlobal(Offset.Zero).Dx)).Distinct().Count();
        return xs;
    }

    [Theory]
    [InlineData(360, 2)]
    [InlineData(767, 2)]
    [InlineData(800, 3)]
    [InlineData(1200, 4)]
    public void ProductGridReflowsWithWidth(int width, int expected)
    {
        var h = Show(new SizedBox(width: width, child: new ShopifyProductGrid(Cards(8))), width: width, height: 800);
        Assert.Equal(expected, Columns(h));
    }

    [Fact]
    public void ProductGridHonoursAnExplicitColumnCount()
    {
        var h = Show(new SizedBox(width: 360, child: new ShopifyProductGrid(Cards(6), columns: 3)));
        Assert.Equal(3, Columns(h));
    }

    [Fact]
    public void OptionChipsSelectAndSkipSoldOutOptions()
    {
        var h = Show(new Host<string>("M", (v, set) => new ShopifyOptionChips<string>(
            [new("S", "S"), new("M", "M"), new("L", "L", Available: false)], v, set, label: "Size")));
        Assert.Contains("Size: M", Texts(h));
        TapText(h, "S");
        Assert.Contains("Size: S", Texts(h));
        TapText(h, "L");
        Assert.Contains("Size: S", Texts(h));
    }

    [Fact]
    public void OptionChipsAreAtLeast44Tall()
    {
        var h = Show(new ShopifyOptionChips<string>([new("S", "S"), new("M", "M")], "S", _ => { }));
        Assert.True(h.Find<RenderDecoratedBox>().All(b => b.Size.Height >= 44));
    }

    [Fact]
    public void OptionChipsWrapOnANarrowScreen()
    {
        var sizes = Enumerable.Range(1, 12).Select(i => new ShopifyOption<int>(i, (30 + i).ToString())).ToList();
        var h = Show(new SizedBox(width: 280, child: new ShopifyOptionChips<int>(sizes, 1, _ => { }, label: "Size")), width: 320);
        AssertNoOverflow(h);
        var ys = h.Find<RenderParagraph>().Where(p => p.PlainText.Length == 2 && char.IsDigit(p.PlainText[0])).Select(p => MathF.Round(p.LocalToGlobal(Offset.Zero).Dy)).Distinct().Count();
        Assert.True(ys > 1);
    }

    [Fact]
    public void SwatchPickerChoosesAColour()
    {
        string? chosen = null;
        var h = Show(new ShopifySwatchPicker<string>([new("red", Colors.Red, "Red"), new("blue", Colors.Blue, "Blue", Available: false)], "red", v => chosen = v, label: "Colour"));
        Assert.Contains("Colour: Red", Texts(h));
        h.Tap(44 + 4 + 22, 22); // the sold-out swatch
        Assert.Null(chosen);
    }

    // ---- cart ----

    [Theory]
    [InlineData(320)]
    [InlineData(360)]
    [InlineData(412)]
    public void CartLineHoldsTogetherOnPhones(int width)
    {
        var h = Show(new SizedBox(width: width, child: new ShopifyCartLine("Merino wool crew neck jumper in heather grey", 129.5m, 2, _ => { }, () => { }, "Heather grey / Large")), width: width);
        Assert.Contains("$259.00", Texts(h));
        AssertNoOverflow(h);
    }

    [Fact]
    public void CartLineQuantityAndRemoveWork()
    {
        int? q = null;
        bool removed = false;
        var h = Show(new SizedBox(width: 360, child: new ShopifyCartLine("Jumper", 10m, 2, v => q = v, () => removed = true)));
        // Plus is the third control in the stepper, which sits below the title and price, right of the 88px photo and 16px gap.
        var plus = h.Find<RenderConstrainedBox>().Where(b => Math.Abs(b.Size.Width - 44) < 0.5f && Math.Abs(b.Size.Height - 44) < 0.5f).ToList();
        Assert.True(plus.Count >= 3);
        var bin = plus.Last();
        var at = bin.LocalToGlobal(Offset.Zero);
        h.Tap(at.Dx + 22, at.Dy + 22);
        Assert.True(removed);
        var inc = plus[1];
        at = inc.LocalToGlobal(Offset.Zero);
        h.Tap(at.Dx + 22, at.Dy + 22);
        Assert.Equal(3, q);
    }

    [Fact]
    public void OrderSummaryListsLinesTotalAndChecksOut()
    {
        bool go = false;
        var h = Show(new SizedBox(width: 340, child: new ShopifyOrderSummary(
            [new("Subtotal", "$100.00"), new("Shipping", "Free", Emphasis: true)], "$100.00", () => go = true)));
        var t = Texts(h);
        Assert.Contains("Subtotal", t);
        Assert.Contains("Free", t);
        Assert.Contains("Total", t);
        AssertNoOverflow(h);
        TapText(h, "Checkout");
        Assert.True(go);
    }

    [Fact]
    public void FreeShippingBarShowsTheRemainingAmountThenConfirms()
    {
        var h = Show(new SizedBox(width: 340, child: new ShopifyFreeShippingBar(38m, 50m)));
        Assert.Contains("You're $12.00 away from free shipping", Texts(h));
        var done = Show(new SizedBox(width: 340, child: new ShopifyFreeShippingBar(60m, 50m)));
        Assert.Contains("You've unlocked free shipping", Texts(done));
        AssertNoOverflow(h);
    }

    [Fact]
    public void EmptyStateOffersAnAction()
    {
        bool go = false;
        var h = Show(new SizedBox(width: 360, height: 500, child: new ShopifyEmptyState(Icons.ShoppingBag, "Your cart is empty", "Add something you love.", "Continue shopping", () => go = true)));
        Assert.Contains("Your cart is empty", Texts(h));
        TapText(h, "Continue shopping");
        Assert.True(go);
    }

    [Fact]
    public void CheckoutStepsCollapseToACompactLabelOnPhones()
    {
        var steps = new[] { "Cart", "Shipping", "Payment" };
        var phone = Show(new SizedBox(width: 360, child: new ShopifyCheckoutSteps(steps, 1)));
        Assert.Contains("Step 2 of 3 · Shipping", Texts(phone));
        AssertNoOverflow(phone);

        var wide = Show(new SizedBox(width: 1000, child: new ShopifyCheckoutSteps(steps, 1)), width: 1000);
        Assert.Contains("Cart", Texts(wide));
        Assert.Contains("Payment", Texts(wide));
        Assert.DoesNotContain("Step 2 of 3 · Shipping", Texts(wide));
        AssertNoOverflow(wide);
    }

    // ---- navigation & layout ----

    [Fact]
    public void AnnouncementBarClosesFromA44Target()
    {
        bool closed = false;
        var h = Show(new SizedBox(width: 360, child: new ShopifyAnnouncementBar("Free shipping over $50", onClose: () => closed = true)));
        Assert.Contains("Free shipping over $50", Texts(h));
        h.Tap(360 - 22, 20);
        Assert.True(closed);
        AssertNoOverflow(h);
    }

    [Fact]
    public void HeaderIsCompactOnPhonesAndShowsLinksOnWiderScreens()
    {
        ShopifyHeader Header() => new(new Text("Shop"), [new("Women"), new("Men"), new("Sale")], onMenu: () => { }, onSearch: () => { }, onCart: () => { }, cartCount: 2);

        var phone = Show(new SizedBox(width: 360, child: Header()));
        Assert.Contains("Shop", Texts(phone));
        Assert.Contains("2", Texts(phone));
        Assert.DoesNotContain("Women", Texts(phone));
        AssertNoOverflow(phone);

        var desktop = Show(new SizedBox(width: 1200, child: Header()), width: 1200);
        Assert.Contains("Women", Texts(desktop));
        Assert.Contains("Sale", Texts(desktop));
        AssertNoOverflow(desktop);
    }

    [Fact]
    public void HeaderCartButtonOpensTheCart()
    {
        bool cart = false;
        var h = Show(new SizedBox(width: 360, child: new ShopifyHeader(new Text("Shop"), onMenu: () => { }, onCart: () => cart = true)));
        h.Tap(360 - 8 - 22, 28);
        Assert.True(cart);
    }

    [Fact]
    public void BottomNavSelectsDestinationsAndShowsBadges()
    {
        int sel = 0;
        var h = Show(new SizedBox(width: 360, child: new Host<int>(0, (v, set) => new ShopifyBottomNavBar(
            [new(Icons.Home, "Home"), new(Icons.Search, "Search"), new(Icons.ShoppingBag, "Cart", Badge: 4), new(Icons.Person, "Account")], v, i => { sel = i; set(i); }))));
        var t = Texts(h);
        Assert.Contains("Home", t);
        Assert.Contains("4", t);
        h.Tap(360 / 4 * 3 - 20, 30); // the third of four equal columns
        Assert.Equal(2, sel);
        AssertNoOverflow(h);
    }

    [Fact]
    public void BottomNavTargetsAre64TallAndClearTheInset()
    {
        var h = Show(new SizedBox(width: 360, child: new ShopifyBottomNavBar([new(Icons.Home, "Home"), new(Icons.Person, "Me")], 0, bottomInset: 24)));
        Assert.Contains(h.Find<RenderDecoratedBox>(), b => Math.Abs(b.Size.Height - (1 + 64 + 24)) < 1.5f);
    }

    [Fact]
    public void SectionHeaderShowsEyebrowTitleAndAction()
    {
        bool all = false;
        var h = Show(new SizedBox(width: 360, child: new ShopifySectionHeader("New arrivals", "Spring", "View all", () => all = true)));
        Assert.Contains("SPRING", Texts(h));
        Assert.Contains("New arrivals", Texts(h));
        TapText(h, "View all");
        Assert.True(all);
        AssertNoOverflow(h);
    }

    [Theory]
    [InlineData(360)]
    [InlineData(820)]
    [InlineData(1280)]
    public void HeroLaysOutAtEveryBreakpoint(int width)
    {
        var h = Show(new SizedBox(width: width, child: new ShopifyHero("Built for the way you shop", "Everything you need.", "Shop now", () => { }, "Learn more", () => { }, "Spring")),
            width: width, height: 1000);
        Assert.Contains("Built for the way you shop", Texts(h));
        Assert.Contains("SPRING", Texts(h));
        AssertNoOverflow(h);
    }

    [Fact]
    public void HeroHeadlineStepsDownOnPhones()
    {
        double Size(int width)
        {
            var h = Show(new SizedBox(width: width, child: new ShopifyHero("Hello", large: true)), width: width, height: 1000);
            return h.Find<RenderParagraph>().First(p => p.PlainText == "Hello").Size.Height;
        }
        Assert.True(Size(360) < Size(1280));
    }

    [Fact]
    public void HeroAlwaysUsesTheNightCanvas()
    {
        var h = Show(new SizedBox(width: 360, child: new ShopifyHero("Hello")), mode: ShopifyThemeMode.Light, height: 700);
        using var bmp = h.Render();
        Assert.Equal(Colors.FromRgb(0x000000), bmp.GetPixel(10, 10));
    }

    [Fact]
    public void AccordionOpensOneAtATime()
    {
        var h = Show(new SizedBox(width: 360, child: new ShopifyAccordion([new("Details", new Text("Cotton")), new("Shipping", new Text("Free"))])));
        Assert.DoesNotContain("Cotton", Texts(h));
        TapText(h, "Details");
        Assert.Contains("Cotton", Texts(h));
        TapText(h, "Shipping");
        Assert.Contains("Free", Texts(h));
        Assert.DoesNotContain("Cotton", Texts(h));
    }

    static (float X, float Y) HandleCentre(Harness h)
    {
        var handle = h.Find<RenderDecoratedBox>().First(b => Math.Abs(b.Size.Width - 36) < 0.5f && Math.Abs(b.Size.Height - 4) < 0.5f);
        var at = handle.LocalToGlobal(Offset.Zero);
        return (at.Dx + 18, at.Dy + 2);
    }

    [Fact]
    public void DraggingThePhoneSheetByItsHandleDismissesIt()
    {
        var h = Show(new Builder(ctx => new ShopifyButton("Filter", () => ShopifySheet.Show(ctx, (c, close) => new Text("Sizes"), "Filters"))));
        TapText(h, "Filter");
        var (x, y) = HandleCentre(h);
        h.Gestures.PointerDown(x, y);
        for (int i = 1; i <= 10; i++) { h.Gestures.PointerMove(x, y + i * 20); h.Advance(16); }
        h.Gestures.PointerUp(x, y + 200);
        h.Pump();
        Assert.DoesNotContain("Filters", Texts(h));
    }

    [Fact]
    public void ASmallDragSnapsTheSheetBack()
    {
        var h = Show(new Builder(ctx => new ShopifyButton("Filter", () => ShopifySheet.Show(ctx, (c, close) => new Text("Sizes"), "Filters"))));
        TapText(h, "Filter");
        float Top() => h.Find<RenderParagraph>().First(p => p.PlainText == "Filters").LocalToGlobal(Offset.Zero).Dy;
        float before = Top();
        var (x, y) = HandleCentre(h);
        h.Gestures.PointerDown(x, y);
        for (int i = 1; i <= 4; i++) { h.Gestures.PointerMove(x, y + i * 8); h.Advance(100); }
        Assert.True(Top() > before);
        h.Gestures.PointerUp(x, y + 32);
        h.Pump();
        Assert.Contains("Filters", Texts(h));
        Assert.Equal(before, Top(), 0.5);
    }

    // ---- sheet ----

    [Fact]
    public void SheetOpensAndTheCloseButtonDismissesIt()
    {
        var h = Show(new Builder(ctx => new ShopifyButton("Filter", () => ShopifySheet.Show(ctx, (c, close) => new Text("Sizes"), "Filters"))));
        TapText(h, "Filter");
        Assert.Contains("Filters", Texts(h));
        Assert.Contains("Sizes", Texts(h));
        // The close button is the 44px square at the sheet's top-right.
        h.Tap(360 - 8 - 22, 700 - h.Find<RenderDecoratedBox>().Where(b => b.Size.Width >= 359).Max(b => b.Size.Height) + 10 + 4 + 22 + 4);
        Assert.DoesNotContain("Filters", Texts(h));
    }

    [Fact]
    public void TappingTheScrimOrPressingEscapeDismissesTheSheet()
    {
        var h = Show(new Builder(ctx => new ShopifyButton("Filter", () => ShopifySheet.Show(ctx, (c, close) => new Text("Sizes"), "Filters"))));
        TapText(h, "Filter");
        h.Tap(180, 40); // above the sheet
        Assert.DoesNotContain("Filters", Texts(h));

        TapText(h, "Filter");
        Assert.Contains("Filters", Texts(h));
        h.Advance(100); // autofocus lands on the next frame
        h.Binding.KeyDown("Escape", "Escape");
        h.Pump();
        Assert.DoesNotContain("Filters", Texts(h));
    }

    [Fact]
    public void TappingInsideTheSheetDoesNotDismissIt()
    {
        var h = Show(new Builder(ctx => new ShopifyButton("Filter", () => ShopifySheet.Show(ctx, (c, close) => new Text("Sizes"), "Filters"))));
        TapText(h, "Filter");
        TapText(h, "Sizes");
        Assert.Contains("Sizes", Texts(h));
    }

    [Fact]
    public void ThePhoneSheetSitsAtTheBottomAndTheWideOneIsCentred()
    {
        Widget Open() => new Builder(ctx => new ShopifyButton("Filter", () => ShopifySheet.Show(ctx, (c, close) => new Text("Sizes"), "Filters")));

        var phone = Show(Open());
        TapText(phone, "Filter");
        var p = phone.Find<RenderParagraph>().First(x => x.PlainText == "Sizes");
        Assert.True(p.LocalToGlobal(Offset.Zero).Dy > 700 / 2);

        var wide = Show(Open(), width: 1000, height: 700);
        TapText(wide, "Filter");
        var w = wide.Find<RenderParagraph>().First(x => x.PlainText == "Filters");
        Assert.InRange(w.LocalToGlobal(Offset.Zero).Dx, 1000 / 2 - 270, 1000 / 2);
    }
}
