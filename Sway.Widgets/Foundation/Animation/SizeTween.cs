using SkiaSharp;

namespace Sway.Widgets;

public sealed class SizeTween(Size begin, Size end) : Tween<Size>(begin, end, Lerps.Size);
