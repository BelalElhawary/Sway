using SkiaSharp;
using Sway.Extras.Ibm;
using Xunit;

namespace Sway.Widgets.Tests;

public class IbmThemeTests
{
    static Harness Show(ThemeData theme, Widget child) =>
        new(new MaterialApp(new Align(Alignment.TopLeft, child), theme: theme, themeMode: ThemeMode.Light));

    [Fact]
    public void MaterialThemeKeepsItsShape()
    {
        Assert.Same(ShapeTheme.Material3, ThemeData.Light().Shape);
        Assert.Equal(40, ThemeData.Light().Shape.ControlHeight);
        Assert.False(ThemeData.Light().Shape.Flat);
    }

    [Fact]
    public void CarbonSchemesHaveTheRightBrightness()
    {
        Assert.Equal(Brightness.Light, IbmTheme.White().Brightness);
        Assert.Equal(Brightness.Light, IbmTheme.Gray10().Brightness);
        Assert.Equal(Brightness.Dark, IbmTheme.Gray90().Brightness);
        Assert.Equal(Brightness.Dark, IbmTheme.Gray100().Brightness);
        Assert.Equal(Colors.FromRgb(0x161616), IbmTheme.Gray100().ColorScheme.Surface);
        Assert.Equal(Colors.FromRgb(0xF4F4F4), IbmTheme.Gray10().ColorScheme.Surface);
    }

    [Fact]
    public void ForPicksTheMatchingTheme()
    {
        Assert.Equal(CarbonColors.White, IbmTheme.For(Brightness.Light).ColorScheme);
        Assert.Equal(CarbonColors.Gray10, IbmTheme.For(Brightness.Light, gray: true).ColorScheme);
        Assert.Equal(CarbonColors.Gray90, IbmTheme.For(Brightness.Dark, gray: true).ColorScheme);
        Assert.Equal(CarbonColors.Gray100, IbmTheme.For(Brightness.Dark).ColorScheme);
    }

    [Fact]
    public void CarbonUsesPlexAndTheFontsAreEmbedded()
    {
        var theme = IbmTheme.White();
        Assert.StartsWith(IbmFonts.Sans, theme.TextTheme.BodyMedium.FontFamily);
        Assert.Equal(theme.ColorScheme.OnSurface, theme.TextTheme.BodyMedium.Color);
        var assembly = typeof(IbmFonts).Assembly;
        Assert.NotNull(assembly.GetManifestResourceStream("Sway.Extras.Ibm.Fonts.IBMPlexSans-Regular.ttf"));
        Assert.NotNull(assembly.GetManifestResourceStream("Sway.Extras.Ibm.Fonts.IBMPlexMono-Regular.ttf"));
    }

    [Fact]
    public void ThemeSwitchesTextToPlex()
    {
        var plex = FontCache.Get(IbmFonts.Sans, 14, 400, false);
        Assert.Equal(IbmFonts.Sans, plex.Typeface.FamilyName);
    }

    [Fact]
    public void ButtonHeightFollowsTheThemeShape()
    {
        var compact = ThemeData.Light() with { Shape = new ShapeTheme { ControlHeight = 32 } };
        Assert.Equal(40, Show(ThemeData.Light(), new FilledButton(new Text("Go"), () => { })).Find<RenderDecoratedBox>().Last().Size.Height, 1);
        Assert.Equal(32, Show(compact, new FilledButton(new Text("Go"), () => { })).Find<RenderDecoratedBox>().Last().Size.Height, 1);
    }

    [Fact]
    public void CarbonButtonsAreSquare()
    {
        var h = Show(IbmTheme.White(), new FilledButton(new Text("Go"), () => { }));
        var box = h.Find<RenderDecoratedBox>().Last();
        Assert.Equal(40, box.Size.Height, 1);
        using var bitmap = h.Render();
        // A square button fills its top-left pixel with the primary colour; a pill would leave it as the page background.
        Assert.Equal(IbmTheme.White().ColorScheme.Primary, bitmap.GetPixel(0, 0));
    }

    [Fact]
    public void DarkCarbonTextButtonsUseTheLighterAccent()
    {
        Assert.Equal(Colors.FromRgb(0x78A9FF), IbmTheme.Gray100().ColorScheme.AccentColor);
        Assert.Equal(Colors.FromRgb(0x0F62FE), IbmTheme.Gray100().ColorScheme.Primary);
        Assert.Equal(Colors.FromRgb(0x0F62FE), IbmTheme.White().ColorScheme.AccentColor);
        // Schemes that set no accent fall back to the primary colour.
        Assert.Equal(ColorScheme.Light.Primary, ColorScheme.Light.AccentColor);
        var h = Show(IbmTheme.Gray100(), new TextButton(new Text("Go"), () => { }));
        using var bitmap = h.Render();
        var accent = IbmTheme.Gray100().ColorScheme.AccentColor;
        bool found = false;
        for (int y = 0; y < 40 && !found; y++)
            for (int x = 0; x < 80 && !found; x++)
            {
                var p = bitmap.GetPixel(x, y);
                found = Math.Abs(p.Red - accent.Red) < 40 && Math.Abs(p.Green - accent.Green) < 40 && Math.Abs(p.Blue - accent.Blue) < 40;
            }
        Assert.True(found, "text button should be drawn in the accent colour");
    }

    [Fact]
    public void CarbonCheckboxIsSquareAndHasNoHalo()
    {
        var shape = IbmTheme.White().Shape;
        Assert.Equal(0, shape.CheckboxRadius);
        Assert.False(shape.ControlHalo);
        Assert.True(shape.CompactSwitch);
        var h = Show(IbmTheme.White(), new Checkbox(true, _ => { }));
        using var bitmap = h.Render();
        // The checked box is 18px centred in a 40px target; its corner pixel is filled when square.
        Assert.Equal(IbmTheme.White().ColorScheme.Primary, bitmap.GetPixel(11, 11));
    }

    [Fact]
    public void CarbonSwitchIsAFlat48By24Track()
    {
        var h = Show(IbmTheme.White(), new Sway.Widgets.Switch(true, _ => { }));
        var track = h.Find<RenderDecoratedBox>().Where(b => Math.Abs(b.Size.Width - 48) < 0.5f).ToList();
        Assert.NotEmpty(track);
        Assert.Equal(24, track[0].Size.Height, 1);
        var material = Show(ThemeData.Light(), new Sway.Widgets.Switch(true, _ => { }));
        Assert.Contains(material.Find<RenderDecoratedBox>(), b => Math.Abs(b.Size.Width - 52) < 0.5f);
    }

    [Fact]
    public void FlatThemesDrawNoShadows()
    {
        var flat = ShapeTheme.Material3 with { Flat = true };
        Assert.Null(flat.Shadows(3, SKColors.Black));
        Assert.NotNull(ShapeTheme.Material3.Shadows(3, SKColors.Black));
    }
}
