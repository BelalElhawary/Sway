using SkiaSharp;

namespace Sway.Widgets;

public sealed class MatrixTween(SKMatrix begin, SKMatrix end) : Tween<SKMatrix>(begin, end, Lerps.Matrix);
