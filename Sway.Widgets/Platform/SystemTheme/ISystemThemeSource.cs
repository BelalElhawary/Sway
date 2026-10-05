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
