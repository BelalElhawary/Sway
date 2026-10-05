using SkiaSharp;

namespace Sway.Widgets;

/// <summary>
/// Reads the operating system's appearance preferences. Each platform package supplies an implementation
/// (desktop, Android, ...) and installs it through <see cref="SystemTheme.Source"/>.
/// </summary>
public interface ISystemThemeSource
{
    /// <summary>How often a host should re-read <see cref="Brightness"/>: a registry read is cheap, a helper process is not.</summary>
    TimeSpan PollInterval { get; }

    /// <summary>The OS light/dark preference; light when it cannot be determined.</summary>
    Brightness Brightness();

    /// <summary>The user's accent colour; null when the platform has none or it is unavailable.</summary>
    SKColor? AccentColor();
}

/// <summary>The active <see cref="ISystemThemeSource"/>. Without a platform package it reports a light theme and no accent.</summary>
public static class SystemTheme
{
    sealed class Fallback : ISystemThemeSource
    {
        public TimeSpan PollInterval => TimeSpan.FromSeconds(5);
        public Brightness Brightness() => Widgets.Brightness.Light;
        public SKColor? AccentColor() => null;
    }

    /// <summary>Set by the platform host at startup.</summary>
    public static ISystemThemeSource Source { get; set; } = new Fallback();

    public static TimeSpan PollInterval => Source.PollInterval;

    public static Brightness Brightness() => Source.Brightness();

    public static SKColor? AccentColor() => Source.AccentColor();
}
