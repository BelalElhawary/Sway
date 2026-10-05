using SkiaSharp;

namespace Sway.Widgets;

public sealed record HitTestEntry(RenderObject Target, SKMatrix GlobalToLocal)
{
    public Offset ToLocal(Offset global)
    {
        var p = GlobalToLocal.MapPoint(global.Dx, global.Dy);
        return new(p.X, p.Y);
    }
}
