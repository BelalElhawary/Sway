using SkiaSharp;

namespace Sway.Widgets;

/// <summary>Render object that holds one box child.</summary>
/// <summary>Anything that hosts at most one box child.</summary>
public interface IRenderChildHolder { RenderBox? Child { get; set; } }
