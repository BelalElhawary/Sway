using SkiaSharp;
using Xunit;

using Sway.Extras.Material3;
using Sway.Widgets;
using Sway.Widgets.Tests;
namespace Sway.Extras.Material3.Tests;

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

    [Fact]
    public void ASystemAccentThemeIsAlwaysUsable()
    {
        foreach (var brightness in new[] { Brightness.Light, Brightness.Dark })
        {
            var theme = ThemeData.FromSystemAccent(brightness);
            Assert.Equal(brightness, theme.Brightness);
        }
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
