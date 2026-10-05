using SkiaSharp;

namespace Sway.Widgets;

/// <summary>Colour filters equivalent to the CSS filter functions (grayscale, sepia, saturate, brightness, contrast, hue-rotate, invert).</summary>
public static class ColorFilters
{
    static SKColorFilter M(params float[] m) => SKColorFilter.CreateColorMatrix(m);
    static float L(float a, float b, float t) => a + (b - a) * t;

    public static SKColorFilter Grayscale(float amount = 1)
    {
        float t = Math.Clamp(amount, 0, 1);
        float r = L(1, 0.2126f, t), g = L(0, 0.7152f, t), b = L(0, 0.0722f, t);
        return M(L(1, 0.2126f, t), L(0, 0.7152f, t), L(0, 0.0722f, t), 0, 0,
                 L(0, 0.2126f, t), L(1, 0.7152f, t), L(0, 0.0722f, t), 0, 0,
                 L(0, 0.2126f, t), L(0, 0.7152f, t), L(1, 0.0722f, t), 0, 0,
                 0, 0, 0, 1, 0);
    }

    public static SKColorFilter Sepia(float amount = 1)
    {
        float t = Math.Clamp(amount, 0, 1);
        return M(L(1, 0.393f, t), L(0, 0.769f, t), L(0, 0.189f, t), 0, 0,
                 L(0, 0.349f, t), L(1, 0.686f, t), L(0, 0.168f, t), 0, 0,
                 L(0, 0.272f, t), L(0, 0.534f, t), L(1, 0.131f, t), 0, 0,
                 0, 0, 0, 1, 0);
    }

    public static SKColorFilter Saturate(float amount)
    {
        float s = amount;
        return M(0.213f + 0.787f * s, 0.715f - 0.715f * s, 0.072f - 0.072f * s, 0, 0,
                 0.213f - 0.213f * s, 0.715f + 0.285f * s, 0.072f - 0.072f * s, 0, 0,
                 0.213f - 0.213f * s, 0.715f - 0.715f * s, 0.072f + 0.928f * s, 0, 0,
                 0, 0, 0, 1, 0);
    }

    public static SKColorFilter Brightness(float amount) => M(amount, 0, 0, 0, 0, 0, amount, 0, 0, 0, 0, 0, amount, 0, 0, 0, 0, 0, 1, 0);

    public static SKColorFilter Contrast(float amount)
    {
        float o = 0.5f * (1 - amount);
        return M(amount, 0, 0, 0, o, 0, amount, 0, 0, o, 0, 0, amount, 0, o, 0, 0, 0, 1, 0);
    }

    public static SKColorFilter Invert(float amount = 1)
    {
        float t = Math.Clamp(amount, 0, 1), s = 1 - 2 * t;
        float o = t;
        return M(s, 0, 0, 0, o, 0, s, 0, 0, o, 0, 0, s, 0, o, 0, 0, 0, 1, 0);
    }

    public static SKColorFilter HueRotate(float degrees)
    {
        float a = degrees * MathF.PI / 180, c = MathF.Cos(a), s = MathF.Sin(a);
        return M(0.213f + c * 0.787f - s * 0.213f, 0.715f - c * 0.715f - s * 0.715f, 0.072f - c * 0.072f + s * 0.928f, 0, 0,
                 0.213f - c * 0.213f + s * 0.143f, 0.715f + c * 0.285f + s * 0.140f, 0.072f - c * 0.072f - s * 0.283f, 0, 0,
                 0.213f - c * 0.213f - s * 0.787f, 0.715f - c * 0.715f + s * 0.715f, 0.072f + c * 0.928f + s * 0.072f, 0, 0,
                 0, 0, 0, 1, 0);
    }
}
