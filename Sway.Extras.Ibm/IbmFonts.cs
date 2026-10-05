using SkiaSharp;
using Sway.Widgets;

namespace Sway.Extras.Ibm;

/// <summary>The IBM Plex faces shipped inside this assembly (SIL Open Font License, see Fonts/OFL.txt).</summary>
public static class IbmFonts
{
    public const string Sans = "IBM Plex Sans";
    public const string Mono = "IBM Plex Mono";

    static readonly (string File, string Family, int Weight, bool Italic)[] Faces =
    [
        ("IBMPlexSans-Light.ttf", Sans, 300, false),
        ("IBMPlexSans-Regular.ttf", Sans, 400, false),
        ("IBMPlexSans-Italic.ttf", Sans, 400, true),
        ("IBMPlexSans-Medium.ttf", Sans, 500, false),
        ("IBMPlexSans-SemiBold.ttf", Sans, 600, false),
        ("IBMPlexMono-Regular.ttf", Mono, 400, false),
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
            var assembly = typeof(IbmFonts).Assembly;
            foreach (var (file, family, weight, italic) in Faces)
            {
                using var stream = assembly.GetManifestResourceStream("Sway.Extras.Ibm.Fonts." + file);
                if (stream is null) continue;
                var face = SKTypeface.FromData(SKData.Create(stream));
                if (face is not null) FontCache.RegisterFace(family, weight, italic, face);
            }
        }
    }
}
