using SkiaSharp;

namespace Sway.Widgets;

public sealed class DecorationTween(BoxDecoration begin, BoxDecoration end) : Tween<BoxDecoration>(begin, end, (a, b, t) => Lerps.BoxDecoration(a, b, t));
