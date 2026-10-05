using SkiaSharp;

namespace Sway.Widgets;

public sealed class ColorTween(SKColor begin, SKColor end) : Tween<SKColor>(begin, end, Lerps.Color);
