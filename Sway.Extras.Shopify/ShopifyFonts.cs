using SkiaSharp;
using Sway.Widgets;

namespace Sway.Extras.Shopify;

/// <summary>
/// The Inter faces shipped inside this assembly (SIL Open Font License, see Fonts/OFL.txt). Inter is the design's body face; Inter Display,
/// its large-size cut, stands in for the proprietary Neue Haas Grotesk Display. If Neue Haas is installed on the machine it is used instead.
/// </summary>
public static class ShopifyFonts
{
    public const string Ui = "Inter";
    public const string Display = "Inter Display";

    static readonly (string File, string Family, int Weight)[] Faces =
    [
        ("InterDisplay-Light.ttf", Display, 300),
        ("InterDisplay-Regular.ttf", Display, 400),
        ("InterDisplay-Medium.ttf", Display, 500),
        ("Inter-Regular.ttf", Ui, 400),
        ("Inter-Medium.ttf", Ui, 500),
        ("Inter-SemiBold.ttf", Ui, 600),
    ];

    static readonly object Gate = new();
    static bool _registered;

    /// <summary>Registers the faces with the font cache. Safe to call repeatedly; the theme factories call it for you.</summary>
    public static void Register()
    {
        lock (Gate)
        {
            if (_registered) return;
            _registered = true;
            var assembly = typeof(ShopifyFonts).Assembly;
            foreach (var (file, family, weight) in Faces)
            {
                using var stream = assembly.GetManifestResourceStream("Sway.Extras.Shopify.Fonts." + file);
                if (stream is null) continue;
                var face = SKTypeface.FromData(SKData.Create(stream));
                if (face is not null) FontCache.RegisterFace(family, weight, false, face);
            }
        }
    }
}
