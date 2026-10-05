using SkiaSharp;

namespace Sway.Widgets;

public sealed class FloatTween(float begin, float end) : Tween<float>(begin, end, Lerps.Float);
