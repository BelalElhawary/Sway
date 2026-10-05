using SkiaSharp;

namespace Sway.Widgets;

public sealed class BoxConstraintsTween(BoxConstraints begin, BoxConstraints end) : Tween<BoxConstraints>(begin, end, Lerps.BoxConstraints);
