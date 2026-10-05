using SkiaSharp;

using Sway.Widgets;

namespace Sway.Extras.Material3;

/// <summary>
/// A tone ramp for one hue, generated in CIE L*C*h so tone N has lightness N. This approximates Material's HCT
/// palettes closely enough for seeded schemes; the default baseline schemes use the published exact values.
/// </summary>
public sealed class TonalPalette(float hue, float chroma)
{
    public SKColor Tone(int tone)
    {
        if (tone <= 0) return SKColors.Black;
        if (tone >= 100) return SKColors.White;
        // Reduce chroma until the colour fits in sRGB so tones stay on-hue instead of clipping.
        for (float c = chroma; c >= 0; c -= 1)
            if (TryLab(tone, c, hue, out var color)) return color;
        return LabToColor(tone, 0, hue);
    }

    static bool TryLab(float l, float c, float h, out SKColor color)
    {
        var (r, g, b) = LabToLinear(l, c, h);
        const float eps = 0.0005f;
        bool inGamut = r >= -eps && r <= 1 + eps && g >= -eps && g <= 1 + eps && b >= -eps && b <= 1 + eps;
        color = FromLinear(r, g, b);
        return inGamut;
    }

    static SKColor LabToColor(float l, float c, float h)
    {
        var (r, g, b) = LabToLinear(l, c, h);
        return FromLinear(r, g, b);
    }

    static (float r, float g, float b) LabToLinear(float l, float c, float hDeg)
    {
        float hr = hDeg * MathF.PI / 180, a = c * MathF.Cos(hr), bb = c * MathF.Sin(hr);
        float fy = (l + 16) / 116, fx = fy + a / 500, fz = fy - bb / 200;
        static float Inv(float t) => t * t * t > 0.008856f ? t * t * t : (t - 16f / 116) / 7.787f;
        float x = 0.95047f * Inv(fx), y = Inv(fy), z = 1.08883f * Inv(fz);
        return (3.2404542f * x - 1.5371385f * y - 0.4985314f * z,
                -0.9692660f * x + 1.8760108f * y + 0.0415560f * z,
                0.0556434f * x - 0.2040259f * y + 1.0572252f * z);
    }

    static SKColor FromLinear(float r, float g, float b)
    {
        static byte Enc(float v)
        {
            v = Math.Clamp(v, 0, 1);
            float s = v <= 0.0031308f ? 12.92f * v : 1.055f * MathF.Pow(v, 1 / 2.4f) - 0.055f;
            return (byte)MathF.Round(Math.Clamp(s, 0, 1) * 255);
        }
        return new SKColor(Enc(r), Enc(g), Enc(b));
    }

    /// <summary>The hue (degrees) and chroma of a colour in CIE L*C*h.</summary>
    public static (float hue, float chroma) HueChroma(SKColor color)
    {
        static float Lin(byte v) { float s = v / 255f; return s <= 0.04045f ? s / 12.92f : MathF.Pow((s + 0.055f) / 1.055f, 2.4f); }
        float r = Lin(color.Red), g = Lin(color.Green), b = Lin(color.Blue);
        float x = (0.4124564f * r + 0.3575761f * g + 0.1804375f * b) / 0.95047f;
        float y = 0.2126729f * r + 0.7151522f * g + 0.0721750f * b;
        float z = (0.0193339f * r + 0.1191920f * g + 0.9503041f * b) / 1.08883f;
        static float F(float t) => t > 0.008856f ? MathF.Cbrt(t) : 7.787f * t + 16f / 116;
        float fx = F(x), fy = F(y), fz = F(z);
        float a = 500 * (fx - fy), bb = 200 * (fy - fz);
        float h = MathF.Atan2(bb, a) * 180 / MathF.PI;
        return ((h + 360) % 360, MathF.Sqrt(a * a + bb * bb));
    }
}
