using SkiaSharp;

namespace Sway.Widgets;

public sealed class EdgeInsetsTween(EdgeInsets begin, EdgeInsets end) : Tween<EdgeInsets>(begin, end, Lerps.EdgeInsets);
