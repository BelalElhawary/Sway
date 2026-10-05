using SkiaSharp;
using Xunit;

namespace Sway.Widgets.Tests;

public class SystemThemeTests
{
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
    public void BrightnessAlwaysResolves()
    {
        var source = DesktopSystemTheme.ForCurrentOS();
        Assert.True(source.Brightness() is Brightness.Light or Brightness.Dark);
        Assert.True(source.PollInterval > TimeSpan.Zero);
    }
}
