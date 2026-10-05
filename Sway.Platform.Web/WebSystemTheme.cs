using SkiaSharp;

namespace Sway.Widgets;

/// <summary>
/// The browser's light/dark preference is delivered by the JS side (<c>prefers-color-scheme</c>) straight into
/// <see cref="WidgetsBinding.PlatformBrightness"/>; this source only reports the default until then.
/// </summary>
sealed class WebThemeSource : ISystemThemeSource
{
    public TimeSpan PollInterval => TimeSpan.FromSeconds(5);
    public Brightness Brightness() => Widgets.Brightness.Light;
    public SKColor? AccentColor() => null;
}
