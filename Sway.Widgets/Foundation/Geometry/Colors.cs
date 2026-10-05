using SkiaSharp;

namespace Sway.Widgets;

public static class Colors
{
    public static readonly SKColor Transparent = SKColors.Transparent;
    public static readonly SKColor Black = SKColors.Black;
    public static readonly SKColor White = SKColors.White;
    public static readonly SKColor Red = new(0xF4, 0x43, 0x36);
    public static readonly SKColor Pink = new(0xE9, 0x1E, 0x63);
    public static readonly SKColor Purple = new(0x9C, 0x27, 0xB0);
    public static readonly SKColor Indigo = new(0x3F, 0x51, 0xB5);
    public static readonly SKColor Blue = new(0x21, 0x96, 0xF3);
    public static readonly SKColor Teal = new(0x00, 0x96, 0x88);
    public static readonly SKColor Green = new(0x4C, 0xAF, 0x50);
    public static readonly SKColor Amber = new(0xFF, 0xC1, 0x07);
    public static readonly SKColor Orange = new(0xFF, 0x98, 0x00);
    public static readonly SKColor Grey = new(0x9E, 0x9E, 0x9E);
    public static readonly SKColor BlueGrey = new(0x60, 0x7D, 0x8B);

    public static SKColor FromRgb(uint rgb) => new((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);
    public static SKColor FromArgb(uint argb) => new((byte)(argb >> 16), (byte)(argb >> 8), (byte)argb, (byte)(argb >> 24));
    public static SKColor WithOpacity(this SKColor c, float opacity) => c.WithAlpha((byte)Math.Clamp(opacity * 255, 0, 255));
}
