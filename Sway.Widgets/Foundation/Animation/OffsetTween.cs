using SkiaSharp;

namespace Sway.Widgets;

public sealed class OffsetTween(Offset begin, Offset end) : Tween<Offset>(begin, end, Lerps.Offset);
