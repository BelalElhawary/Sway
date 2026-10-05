using SkiaSharp;

namespace Sway.Widgets;

public sealed class BorderRadiusTween(BorderRadius begin, BorderRadius end) : Tween<BorderRadius>(begin, end, Lerps.BorderRadius);
