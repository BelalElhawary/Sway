using SkiaSharp;
using Xunit;

namespace Sway.Widgets.Tests;

public class ThemeTests
{
    sealed record BrandColors(SKColor Accent, float Radius) : ThemeExtension;
    sealed record Spacing(float Gap) : ThemeExtension;

    // ---- extensions ----

    [Fact]
    public void ExtensionsAreFoundByType()
    {
        var theme = ThemeData.Light().WithExtensions(new BrandColors(Colors.Red, 8), new Spacing(12));
        Assert.Equal(Colors.Red, theme.Extension<BrandColors>()!.Accent);
        Assert.Equal(12, theme.Extension<Spacing>()!.Gap);
        Assert.Null(ThemeData.Light().Extension<BrandColors>());
    }

    [Fact]
    public void AnExtensionReplacesOneOfTheSameType()
    {
        var theme = ThemeData.Light().WithExtensions(new Spacing(4)).WithExtensions(new Spacing(16), new BrandColors(Colors.Blue, 2));
        Assert.Equal(16, theme.Extension<Spacing>()!.Gap);
        Assert.Equal(2, theme.Extensions.Count);
    }

    [Fact]
    public void WidgetsReadExtensionsFromTheNearestTheme()
    {
        float? seen = null;
        var theme = ThemeData.Light().WithExtensions(new Spacing(24));
        var h = new Harness(new MaterialApp(new Builder(ctx =>
        {
            seen = Theme.Of(ctx).Extension<Spacing>()?.Gap;
            return new SizedBox();
        }), theme: theme));
        Assert.Equal(24, seen);
    }

    [Fact]
    public void ChangingAnExtensionRebuildsDependents()
    {
        int builds = 0;
        Widget Build(float gap) => new MaterialApp(new Builder(ctx =>
        {
            builds++;
            Theme.Of(ctx);
            return new SizedBox();
        }), theme: ThemeData.Light().WithExtensions(new Spacing(gap)));
        var h = new Harness(Build(1));
        int before = builds;
        h.Binding.ReassembleRoot(Build(2));
        h.Pump();
        Assert.True(builds > before);
    }

    // ---- platform preference parsing ----

    [Theory]
    [InlineData("Dark\n", Brightness.Dark)]
    [InlineData("dark", Brightness.Dark)]
    [InlineData("", Brightness.Light)]
    [InlineData(null, Brightness.Light)]
    [InlineData("Light", Brightness.Light)]
    public void MacInterfaceStyle(string? output, Brightness expected) =>
        Assert.Equal(expected, MacThemeSource.ParseInterfaceStyle(output));

    [Theory]
    [InlineData("'prefer-dark'\n", Brightness.Dark)]
    [InlineData("'prefer-light'", Brightness.Light)]
    [InlineData("'default'", Brightness.Light)]
    public void GnomeColorScheme(string output, Brightness expected) =>
        Assert.Equal(expected, LinuxThemeSource.ParseColorScheme(output));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("garbage")]
    public void GnomeColorSchemeIsNullWhenUnknown(string? output) => Assert.Null(LinuxThemeSource.ParseColorScheme(output));

    [Theory]
    [InlineData("'Adwaita-dark'", Brightness.Dark)]
    [InlineData("'Yaru'", Brightness.Light)]
    public void GnomeThemeName(string output, Brightness expected) =>
        Assert.Equal(expected, LinuxThemeSource.ParseThemeName(output));

    [Fact]
    public void WindowsAccentColoursAreStoredBlueGreenRed()
    {
        // 0xFFD5 7B 3A as AABBGGRR is red 0x3A, green 0x7B, blue 0xD5.
        var color = WindowsThemeSource.FromAbgr(0xFFD57B3A);
        Assert.Equal(new SKColor(0x3A, 0x7B, 0xD5), color);
    }

    [Fact]
    public void ASystemAccentThemeIsAlwaysUsable()
    {
        foreach (var brightness in new[] { Brightness.Light, Brightness.Dark })
        {
            var theme = ThemeData.FromSystemAccent(brightness);
            Assert.Equal(brightness, theme.Brightness);
        }
    }

    [Fact]
    public void BrightnessAlwaysResolves()
    {
        var source = DesktopSystemTheme.ForCurrentOS();
        Assert.True(source.Brightness() is Brightness.Light or Brightness.Dark);
        Assert.True(source.PollInterval > TimeSpan.Zero);
    }

    // ---- following the platform ----

    [Fact]
    public void MaterialAppFollowsPlatformBrightnessChanges()
    {
        SKColor? seen = null;
        var h = new Harness(new MaterialApp(new Builder(ctx =>
        {
            seen = Theme.Of(ctx).ColorScheme.Surface;
            return new SizedBox();
        })));
        var light = seen;
        h.Binding.PlatformBrightness = Brightness.Dark;
        h.Pump();
        Assert.NotEqual(light, seen);
        Assert.Equal(ColorScheme.Dark.Surface, seen);
        h.Binding.PlatformBrightness = Brightness.Light;
        h.Pump();
        Assert.Equal(light, seen);
    }

    [Fact]
    public void ExplicitThemeModeIgnoresThePlatform()
    {
        SKColor? seen = null;
        var h = new Harness(new MaterialApp(new Builder(ctx =>
        {
            seen = Theme.Of(ctx).ColorScheme.Surface;
            return new SizedBox();
        }), themeMode: ThemeMode.Light));
        h.Binding.PlatformBrightness = Brightness.Dark;
        h.Pump();
        Assert.Equal(ColorScheme.Light.Surface, seen);
    }
}
