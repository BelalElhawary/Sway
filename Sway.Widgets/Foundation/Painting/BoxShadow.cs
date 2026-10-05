using SkiaSharp;

namespace Sway.Widgets;

public readonly record struct BoxShadow(SKColor Color, Offset Offset = default, float BlurRadius = 0, float SpreadRadius = 0);
