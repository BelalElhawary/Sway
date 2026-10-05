using SkiaSharp;

namespace Sway.Widgets;

/// <summary>A pointer action in global (window) coordinates. <see cref="LocalPosition"/> is rewritten per target by the router.</summary>
public sealed record PointerEvent(PointerEventKind Kind, int Pointer, Offset Position, Offset Delta = default, Offset ScrollDelta = default, long TimestampMs = 0)
{
    /// <summary>Position in the receiving render object's own coordinates.</summary>
    public Offset LocalPosition { get; init; } = Position;
}
