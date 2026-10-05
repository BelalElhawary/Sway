using SkiaSharp;

namespace Sway.Widgets;

public sealed class AlignmentTween(Alignment begin, Alignment end) : Tween<Alignment>(begin, end, Lerps.Alignment);
