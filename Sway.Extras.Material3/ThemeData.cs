using SkiaSharp;

using Sway.Widgets;

namespace Sway.Extras.Material3;

public sealed record ThemeData
{
    public ColorScheme ColorScheme { get; init; } = ColorScheme.Light;
    public TextTheme TextTheme { get; init; } = new TextTheme().Apply(ColorScheme.Light.OnSurface);
    public bool UseMaterial3 { get; init; } = true;
    public Brightness Brightness => ColorScheme.Brightness;

    /// <summary>App-defined theme data, looked up by type with <see cref="Extension{T}"/>.</summary>
    public IReadOnlyList<ThemeExtension> Extensions { get; init; } = Array.Empty<ThemeExtension>();

    /// <summary>The extension of type <typeparamref name="T"/>, or null if the theme has none.</summary>
    public T? Extension<T>() where T : ThemeExtension => Extensions.OfType<T>().FirstOrDefault();

    /// <summary>A copy with <paramref name="extensions"/> added; an extension replaces any existing one of the same type.</summary>
    public ThemeData WithExtensions(params ThemeExtension[] extensions) =>
        this with { Extensions = Extensions.Where(e => extensions.All(n => n.GetType() != e.GetType())).Concat(extensions).ToList() };

    public SKColor ScaffoldBackground => ColorScheme.Surface;

    /// <summary>The default Material 3 light theme (baseline purple).</summary>
    public static ThemeData Light() => FromScheme(ColorScheme.Light);

    /// <summary>The default Material 3 dark theme.</summary>
    public static ThemeData Dark() => FromScheme(ColorScheme.Dark);

    public static ThemeData FromScheme(ColorScheme scheme) =>
        new() { ColorScheme = scheme, TextTheme = new TextTheme().Apply(scheme.OnSurface) };

    public static ThemeData FromSeed(SKColor seed, Brightness brightness = Brightness.Light) =>
        FromScheme(ColorScheme.FromSeed(seed, brightness));

    /// <summary>A theme seeded from the user's system accent colour, or the default seed where the platform reports none.</summary>
    public static ThemeData FromSystemAccent(Brightness brightness = Brightness.Light) =>
        SystemTheme.AccentColor() is { } accent ? FromSeed(accent, brightness) : brightness == Brightness.Dark ? Dark() : Light();
}
