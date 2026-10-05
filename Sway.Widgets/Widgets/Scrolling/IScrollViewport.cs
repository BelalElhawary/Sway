using SkiaSharp;

namespace Sway.Widgets;

/// <summary>A render object that scrolls its content, exposed so focus can bring a widget into view.</summary>
interface IScrollViewport
{
    Axis ScrollAxis { get; }
    ScrollPosition Position { get; }

    /// <summary>How far children are shifted at paint time beyond their layout offsets (zero when the offsets already include scrolling).</summary>
    Offset PaintShift { get; }
}
