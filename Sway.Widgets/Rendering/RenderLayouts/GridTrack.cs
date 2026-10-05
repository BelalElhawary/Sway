using SkiaSharp;

namespace Sway.Widgets;

/// <summary>A grid track size: fixed pixels, a flexible share (fr) of the remaining space, or sized by its content.</summary>
public readonly record struct GridTrack(TrackKind Kind, float Value = 0)
{
    public static GridTrack Px(float v) => new(TrackKind.Px, v);
    public static GridTrack Fr(float v = 1) => new(TrackKind.Fr, v);
    public static readonly GridTrack Auto = new(TrackKind.Auto);
    public static implicit operator GridTrack(float px) => Px(px);
}
