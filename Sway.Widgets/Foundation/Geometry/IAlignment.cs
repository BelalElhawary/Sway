using SkiaSharp;

namespace Sway.Widgets;

/// <summary>A point inside a rectangle: (-1,-1) is top-left, (1,1) is bottom-right.</summary>
public interface IAlignment { Alignment Resolve(TextDirection direction); }
