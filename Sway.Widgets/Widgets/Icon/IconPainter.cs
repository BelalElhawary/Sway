using SkiaSharp;

namespace Sway.Widgets;

sealed class IconPainter(IconData icon, SKColor color) : CustomPainter
{
    static readonly Dictionary<IconData, (SKPath? Primary, SKPath? Secondary)> Cache = new();

    static SKPath? Parse(string? data, bool evenOdd)
    {
        if (string.IsNullOrEmpty(data)) return null;
        var path = SKPath.ParseSvgPathData(data);
        if (path is not null && evenOdd) path.FillType = SKPathFillType.EvenOdd;
        return path;
    }

    public override void Paint(SKCanvas canvas, Size size)
    {
        if (!Cache.TryGetValue(icon, out var paths))
            Cache[icon] = paths = (Parse(icon.Path, icon.EvenOdd), Parse(icon.Secondary, icon.EvenOdd));
        canvas.Save();
        canvas.Scale(size.Width / 24f, size.Height / 24f);
        if (paths.Secondary is not null)
        {
            using var faint = new SKPaint { Color = color.WithAlpha((byte)(color.Alpha * 0.3f)), IsAntialias = true, Style = SKPaintStyle.Fill };
            canvas.DrawPath(paths.Secondary, faint);
        }
        if (paths.Primary is not null)
        {
            using var paint = new SKPaint { Color = color, IsAntialias = true, Style = SKPaintStyle.Fill };
            canvas.DrawPath(paths.Primary, paint);
        }
        canvas.Restore();
    }

    public override bool ShouldRepaint(CustomPainter old) => old is not IconPainter p || p.GetHashCode() != GetHashCode() || true;
}
