using SkiaSharp;

namespace Sway.Widgets;

/// <summary>Draws directly onto the canvas.</summary>
public abstract class CustomPainter
{
    public abstract void Paint(SKCanvas canvas, Size size);
    public virtual bool ShouldRepaint(CustomPainter oldDelegate) => true;
    public virtual bool HitTest(Offset position) => false;
}
