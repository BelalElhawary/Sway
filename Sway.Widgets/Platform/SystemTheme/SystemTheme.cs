using SkiaSharp;

namespace Sway.Widgets;

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
