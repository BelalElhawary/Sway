using Android.Content;
using Android.Content.Res;
using SkiaSharp;

namespace Sway.Widgets;

/// <summary>Reads the light/dark preference from the activity's configuration.</summary>
public sealed class AndroidThemeSource : ISystemThemeSource
{
    readonly Context _context;

    public AndroidThemeSource(Context context) => _context = context;

    // Reading the configuration is cheap.
    public TimeSpan PollInterval => TimeSpan.FromSeconds(1);

    public Brightness Brightness() => IsNight(_context) ? Widgets.Brightness.Dark : Widgets.Brightness.Light;

    public SKColor? AccentColor() => null;

    static bool IsNight(Context context) =>
        ((context.Resources?.Configuration?.UiMode ?? 0) & UiMode.NightMask) == UiMode.NightYes;
}
