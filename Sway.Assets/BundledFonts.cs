namespace Sway.Assets;

/// <summary>The font files shipped inside this assembly.</summary>
public static class BundledFonts
{
    /// <summary>Opens a bundled font such as "Roboto-Regular.ttf", or returns null if there is no such file.</summary>
    public static Stream? Open(string file) =>
        typeof(BundledFonts).Assembly.GetManifestResourceStream("Sway.Fonts." + file);
}
