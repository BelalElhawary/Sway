using SkiaSharp;
using Sway.Widgets;
using Sway.Widgets.Tests;
using Xunit;

namespace Sway.Extras.Ibm.Tests;

public class CarbonTests
{
    static Harness Show(Widget child, CarbonThemeData? theme = null, int width = 400, int height = 300)
    {
        var h = new Harness(new CarbonApp(new Align(Alignment.TopLeft, child), theme: theme ?? CarbonThemeData.White(), themeMode: CarbonThemeMode.Light), width, height);
        // A pointer parked at the origin would leave whatever sits there hovered.
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

    // ---- separation ----

    [Fact]
    public void CarbonDoesNotDependOnMaterial()
    {
        var references = typeof(CarbonApp).Assembly.GetReferencedAssemblies().Select(a => a.Name).ToList();
        Assert.DoesNotContain(references, n => n!.Contains("Material3"));
        Assert.Contains("Sway.Widgets", references);
    }

    [Fact]
    public void TheCoreLibraryDoesNotDependOnADesignSystem()
    {
        var references = typeof(Widget).Assembly.GetReferencedAssemblies().Select(a => a.Name).ToList();
        Assert.DoesNotContain(references, n => n!.Contains("Material3") || n.Contains("Ibm"));
    }

    // ---- theme ----

    [Fact]
    public void TheFourThemesHaveTheRightBrightnessAndPage()
    {
        Assert.Equal(Brightness.Light, CarbonThemeData.White().Brightness);
        Assert.Equal(Brightness.Light, CarbonThemeData.Gray10().Brightness);
        Assert.Equal(Brightness.Dark, CarbonThemeData.Gray90().Brightness);
        Assert.Equal(Brightness.Dark, CarbonThemeData.Gray100().Brightness);
        Assert.Equal(Colors.FromRgb(0xFFFFFF), CarbonThemeData.White().Colors.Background);
        Assert.Equal(Colors.FromRgb(0xF4F4F4), CarbonThemeData.Gray10().Colors.Background);
        Assert.Equal(Colors.FromRgb(0x262626), CarbonThemeData.Gray90().Colors.Background);
        Assert.Equal(Colors.FromRgb(0x161616), CarbonThemeData.Gray100().Colors.Background);
        // Layers sit on the page: white layers on Gray 10, Gray 10 layers on White.
        Assert.Equal(Colors.FromRgb(0xFFFFFF), CarbonThemeData.Gray10().Colors.Layer01);
        Assert.Equal(Colors.FromRgb(0xF4F4F4), CarbonThemeData.White().Colors.Layer01);
    }

    [Fact]
    public void TheTypeScaleIsPlex()
    {
        var type = CarbonThemeData.White().Type;
        Assert.StartsWith(IbmFonts.Sans, type.BodyCompact01.FontFamily);
        Assert.StartsWith(IbmFonts.Mono, type.Code01.FontFamily);
        Assert.Equal(14, type.BodyCompact01.FontSize);
        var assembly = typeof(IbmFonts).Assembly;
        Assert.NotNull(assembly.GetManifestResourceStream("Sway.Extras.Ibm.Fonts.IBMPlexSans-Regular.ttf"));
        Assert.NotNull(assembly.GetManifestResourceStream("Sway.Extras.Ibm.Fonts.IBMPlexMono-Regular.ttf"));
        Assert.Equal(IbmFonts.Sans, FontCache.Get(IbmFonts.Sans, 14, 400, false).Typeface.FamilyName);
    }

    [Fact]
    public void TheAppProvidesTheThemeAndPaintsThePage()
    {
        CarbonThemeData? seen = null;
        var h = new Harness(new CarbonApp(new Builder(ctx => { seen = CarbonTheme.Of(ctx); return new SizedBox(); }),
            theme: CarbonThemeData.White(), themeMode: CarbonThemeMode.Light));
        Assert.Equal(CarbonColors.White, seen!.Colors);
        using var bitmap = h.Render();
        Assert.Equal(CarbonColors.White.Background, bitmap.GetPixel(5, 5));
    }

    [Fact]
    public void TheAppFollowsThePlatformInSystemMode()
    {
        CarbonThemeData? seen = null;
        var h = new Harness(new CarbonApp(new Builder(ctx => { seen = CarbonTheme.Of(ctx); return new SizedBox(); }),
            theme: CarbonThemeData.White(), darkTheme: CarbonThemeData.Gray90()));
        Assert.Equal(CarbonColors.White, seen!.Colors);
        h.Binding.PlatformBrightness = Brightness.Dark;
        h.Pump();
        Assert.Equal(CarbonColors.Gray90, seen!.Colors);
    }

    [Fact]
    public void AnExplicitModeIgnoresThePlatform()
    {
        CarbonThemeData? seen = null;
        var h = new Harness(new CarbonApp(new Builder(ctx => { seen = CarbonTheme.Of(ctx); return new SizedBox(); }),
            themeMode: CarbonThemeMode.Light));
        h.Binding.PlatformBrightness = Brightness.Dark;
        h.Pump();
        Assert.Equal(Brightness.Light, seen!.Brightness);
    }

    // ---- buttons ----

    [Theory]
    [InlineData(CarbonButtonSize.Small, 32)]
    [InlineData(CarbonButtonSize.Medium, 40)]
    [InlineData(CarbonButtonSize.Large, 48)]
    [InlineData(CarbonButtonSize.ExtraLarge, 64)]
    public void ButtonHeightFollowsItsSize(CarbonButtonSize size, float height)
    {
        var h = Show(new CarbonButton(new Text("Go"), () => { }, size: size));
        Assert.Equal(height, h.Find<RenderDecoratedBox>().Last().Size.Height, 1);
    }

    [Fact]
    public void ButtonsAreSquareAndFilledWithTheirKindColour()
    {
        var h = Show(new CarbonButton(new Text("Go"), () => { }));
        using var bitmap = h.Render();
        // A square button fills its top-left pixel; a rounded one would leave the page background.
        Assert.Equal(CarbonColors.White.ButtonPrimary, bitmap.GetPixel(0, 0));

        var secondary = Show(new CarbonButton(new Text("Go"), () => { }, CarbonButtonKind.Secondary));
        using var b2 = secondary.Render();
        Assert.Equal(CarbonColors.White.ButtonSecondary, b2.GetPixel(0, 0));
    }

    [Fact]
    public void ADisabledButtonDoesNotFire()
    {
        int presses = 0;
        var enabled = Show(new CarbonButton(new Text("Go"), () => presses++));
        enabled.Tap(10, 10);
        Assert.Equal(1, presses);
        var disabled = Show(new CarbonButton(new Text("Go"), null));
        using var bitmap = disabled.Render();
        Assert.Equal(CarbonColors.White.ButtonDisabled, bitmap.GetPixel(0, 0));
    }

    [Fact]
    public void HoverUsesTheHoverColour()
    {
        var h = Show(new CarbonButton(new Text("Go"), () => { }));
        h.Gestures.PointerMove(10, 10);
        h.Pump();
        using var bitmap = h.Render();
        Assert.Equal(CarbonColors.White.ButtonPrimaryHover, bitmap.GetPixel(0, 0));
    }

    [Fact]
    public void ADarkTertiaryButtonInvertsOnHover()
    {
        var h = Show(new CarbonButton(new Text("Go"), () => { }, CarbonButtonKind.Tertiary), CarbonThemeData.Gray100());
        h.Gestures.PointerMove(10, 10);
        h.Pump();
        using var bitmap = h.Render();
        Assert.Equal(CarbonColors.Gray100.ButtonTertiaryHover, bitmap.GetPixel(2, 2));
    }

    // ---- checkbox ----

    [Fact]
    public void ACheckboxIsASixteenPixelSquare()
    {
        var h = Show(new CarbonCheckbox(true, _ => { }));
        var box = h.Find<RenderDecoratedBox>().Last();
        Assert.Equal(16, box.Size.Width, 1);
        Assert.Equal(16, box.Size.Height, 1);
        using var bitmap = h.Render();
        Assert.Equal(CarbonColors.White.IconPrimary, bitmap.GetPixel(1, 1));
    }

    [Fact]
    public void ACheckboxReportsTheOppositeValue()
    {
        bool? got = null;
        Show(new CarbonCheckbox(false, v => got = v)).Tap(8, 8);
        Assert.True(got);
        got = null;
        Show(new CarbonCheckbox(true, v => got = v)).Tap(8, 8);
        Assert.False(got);
    }

    [Fact]
    public void AnUncheckedCheckboxHasAHollowCentre()
    {
        var h = Show(new CarbonCheckbox(false, _ => { }));
        using var bitmap = h.Render();
        Assert.Equal(CarbonColors.White.Background, bitmap.GetPixel(8, 3));
    }

    // ---- text input ----

    [Fact]
    public void ATextInputShowsItsLabelHelperAndPlaceholder()
    {
        var h = Show(new CarbonTextInput(label: "Name", placeholder: "Ada", helperText: "Shown publicly"));
        var texts = Texts(h);
        Assert.Contains("Name", texts);
        Assert.Contains("Shown publicly", texts);
    }

    [Fact]
    public void AnErrorReplacesTheHelperText()
    {
        var h = Show(new CarbonTextInput(helperText: "Shown publicly", errorText: "Required"));
        var texts = Texts(h);
        Assert.Contains("Required", texts);
        Assert.DoesNotContain("Shown publicly", texts);
    }

    [Fact]
    public void ATextInputTakesTypingAndReportsIt()
    {
        string? changed = null;
        var controller = new TextEditingController();
        var h = Show(new CarbonTextInput(controller, onChanged: v => changed = v));
        var field = h.Find<RenderEditable>().First();
        var at = field.LocalToGlobal(Offset.Zero);
        h.Tap(at.Dx + 4, at.Dy + 4);
        h.Binding.TextInput("hello");
        h.Pump();
        Assert.Equal("hello", controller.Text);
        Assert.Equal("hello", changed);
    }

    [Fact]
    public void TheSearchFieldClearsWithItsButton()
    {
        string? changed = null;
        var controller = new TextEditingController("abc");
        var h = Show(new CarbonSearch(controller, onChanged: v => changed = v), width: 300);
        h.Pump();
        // The clear button is the square at the right edge of the 40px field.
        h.Tap(300 - 20 + 0, 20);
        Assert.Equal("", controller.Text);
        Assert.Equal("", changed);
    }

    // ---- dropdown and switcher ----

    [Fact]
    public void ADropdownOpensAndPicksAnItem()
    {
        int? picked = null;
        var h = Show(new SizedBox(width: 200, child: new CarbonDropdown<int>(
            [new(1, "One"), new(2, "Two"), new(3, "Three")], 1, v => picked = v)));
        Assert.Contains("One", Texts(h));
        Assert.DoesNotContain("Two", Texts(h));

        h.Tap(50, 20);
        Assert.Contains("Two", Texts(h));
        TapText(h, "Two");
        Assert.Equal(2, picked);
        Assert.DoesNotContain("Three", Texts(h));
    }

    [Fact]
    public void ADropdownMenuHasOneRowPerItemAtTheFieldHeight()
    {
        var h = Show(new SizedBox(width: 200, child: new CarbonDropdown<int>([new(1, "One"), new(2, "Two")], 1, _ => { })));
        h.Tap(50, 20);
        var one = h.Find<RenderParagraph>().Last(p => p.PlainText == "One").LocalToGlobal(Offset.Zero).Dy;
        var two = h.Find<RenderParagraph>().First(p => p.PlainText == "Two").LocalToGlobal(Offset.Zero).Dy;
        Assert.Equal(40, two - one, 1);
    }

    [Fact]
    public void TheContentSwitcherReportsTheTappedSegment()
    {
        int? got = null;
        var h = Show(new ContentSwitcher(["A", "B", "C"], 0, i => got = i));
        TapText(h, "C");
        Assert.Equal(2, got);
    }
}
