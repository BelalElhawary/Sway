using System.Diagnostics;
using SkiaSharp;

namespace Sway.Widgets;

/// <summary>Reads the operating system's light/dark preference and accent colour.</summary>
public static class SystemTheme
{
    /// <summary>How often a host should re-read <see cref="Brightness"/>: a registry read is cheap, a helper process is not.</summary>
    public static TimeSpan PollInterval => OperatingSystem.IsWindows() ? TimeSpan.FromSeconds(1) : TimeSpan.FromSeconds(5);

    /// <summary>The OS preference; light when it cannot be determined.</summary>
    public static Brightness Brightness()
    {
        try
        {
            if (OperatingSystem.IsWindows()) return WindowsBrightness();
            if (OperatingSystem.IsMacOS()) return ParseMacInterfaceStyle(Run("defaults", "read -g AppleInterfaceStyle"));
            if (OperatingSystem.IsLinux())
            {
                var scheme = ParseGnomeColorScheme(Run("gsettings", "get org.gnome.desktop.interface color-scheme"));
                if (scheme is { } s) return s;
                return ParseGnomeThemeName(Run("gsettings", "get org.gnome.desktop.interface gtk-theme"));
            }
        }
        catch
        {
            // Missing tools, sandboxing, a locked registry: keep the default.
        }
        return Widgets.Brightness.Light;
    }

    /// <summary>The user's accent colour on Windows; null elsewhere or when unavailable.</summary>
    public static SKColor? AccentColor()
    {
        if (!OperatingSystem.IsWindows()) return null;
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

    static Brightness WindowsBrightness()
    {
        using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
        return key?.GetValue("AppsUseLightTheme") is int v && v == 0 ? Widgets.Brightness.Dark : Widgets.Brightness.Light;
    }

    // `defaults read -g AppleInterfaceStyle` prints "Dark" in dark mode and fails (no output) in light mode.
    internal static Brightness ParseMacInterfaceStyle(string? output) =>
        output?.Trim().Equals("Dark", StringComparison.OrdinalIgnoreCase) == true ? Widgets.Brightness.Dark : Widgets.Brightness.Light;

    // GNOME 42+: 'default', 'prefer-dark' or 'prefer-light'. Null when the key is missing so older setups can fall back.
    internal static Brightness? ParseGnomeColorScheme(string? output)
    {
        var value = output?.Trim().Trim('\'', '"');
        return value switch
        {
            "prefer-dark" => Widgets.Brightness.Dark,
            "prefer-light" or "default" => Widgets.Brightness.Light,
            _ => null,
        };
    }

    internal static Brightness ParseGnomeThemeName(string? output) =>
        output?.Contains("dark", StringComparison.OrdinalIgnoreCase) == true ? Widgets.Brightness.Dark : Widgets.Brightness.Light;

    static string? Run(string file, string arguments)
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
