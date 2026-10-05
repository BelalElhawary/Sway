using SkiaSharp;

namespace Sway.Widgets;

public sealed class TextStyleTween(TextStyle begin, TextStyle end) : Tween<TextStyle>(begin, end, Lerps.TextStyle);
