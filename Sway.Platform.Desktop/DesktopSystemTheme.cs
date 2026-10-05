using System.Diagnostics;
using System.Runtime.Versioning;
using SkiaSharp;

namespace Sway.Widgets;

/// <summary>Picks the <see cref="ISystemThemeSource"/> for the operating system the desktop host is running on.</summary>
public static class DesktopSystemTheme
{
    public static ISystemThemeSource ForCurrentOS() =>
        OperatingSystem.IsWindows() ? new WindowsThemeSource()
        : OperatingSystem.IsMacOS() ? new MacThemeSource()
        : new LinuxThemeSource();

    /// <summary>Runs a helper tool and returns its stdout, or null on failure or after one second.</summary>
    internal static string? Run(string file, string arguments)
    {
        using var process = Process.Start(new ProcessStartInfo(file, arguments)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        });
        if (process is null) return null;
        string output = process.StandardOutput.ReadToEnd();
        if (!process.WaitForExit(1000))
        {
            process.Kill();
            return null;
        }
        return process.ExitCode == 0 ? output : null;
    }
}

/// <summary>Reads the light/dark preference and accent colour from the registry.</summary>
public sealed class WindowsThemeSource : ISystemThemeSource
{
    // A registry read is cheap.
    public TimeSpan PollInterval => TimeSpan.FromSeconds(1);

    [SupportedOSPlatform("windows")]
    public Brightness Brightness()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int v && v == 0 ? Widgets.Brightness.Dark : Widgets.Brightness.Light;
        }
        catch
        {
            return Widgets.Brightness.Light; // a locked registry: keep the default
        }
    }

    [SupportedOSPlatform("windows")]
    public SKColor? AccentColor()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\DWM");
            return key?.GetValue("AccentColor") is int abgr ? FromAbgr(unchecked((uint)abgr)) : null;
        }
        catch
        {
            return null;
        }
    }

    // Windows stores colours as 0xAABBGGRR.
    internal static SKColor FromAbgr(uint abgr) => new((byte)abgr, (byte)(abgr >> 8), (byte)(abgr >> 16), 255);
}

/// <summary>Reads the appearance through the <c>defaults</c> tool.</summary>
public sealed class MacThemeSource : ISystemThemeSource
{
    // A helper process is not cheap.
    public TimeSpan PollInterval => TimeSpan.FromSeconds(5);

    public Brightness Brightness()
    {
        try { return ParseInterfaceStyle(DesktopSystemTheme.Run("defaults", "read -g AppleInterfaceStyle")); }
        catch { return Widgets.Brightness.Light; } // missing tools, sandboxing
    }

    public SKColor? AccentColor() => null;

    // `defaults read -g AppleInterfaceStyle` prints "Dark" in dark mode and fails (no output) in light mode.
    internal static Brightness ParseInterfaceStyle(string? output) =>
        output?.Trim().Equals("Dark", StringComparison.OrdinalIgnoreCase) == true ? Widgets.Brightness.Dark : Widgets.Brightness.Light;
}

/// <summary>Reads the GNOME appearance settings through <c>gsettings</c>.</summary>
public sealed class LinuxThemeSource : ISystemThemeSource
{
    public TimeSpan PollInterval => TimeSpan.FromSeconds(5);

    public Brightness Brightness()
    {
        try
        {
            var scheme = ParseColorScheme(DesktopSystemTheme.Run("gsettings", "get org.gnome.desktop.interface color-scheme"));
            if (scheme is { } s) return s;
            return ParseThemeName(DesktopSystemTheme.Run("gsettings", "get org.gnome.desktop.interface gtk-theme"));
        }
        catch
        {
            return Widgets.Brightness.Light; // gsettings not installed, or not GNOME
        }
    }

    public SKColor? AccentColor() => null;

    // GNOME 42+: 'default', 'prefer-dark' or 'prefer-light'. Null when the key is missing so older setups can fall back.
    internal static Brightness? ParseColorScheme(string? output)
    {
        var value = output?.Trim().Trim('\'', '"');
        return value switch
        {
            "prefer-dark" => Widgets.Brightness.Dark,
            "prefer-light" or "default" => Widgets.Brightness.Light,
            _ => null,
        };
    }

    internal static Brightness ParseThemeName(string? output) =>
        output?.Contains("dark", StringComparison.OrdinalIgnoreCase) == true ? Widgets.Brightness.Dark : Widgets.Brightness.Light;
}
