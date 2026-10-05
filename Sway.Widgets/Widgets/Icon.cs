using SkiaSharp;

namespace Sway.Widgets;

public sealed class IconTheme(SKColor? color, float? size, Widget child, Key? key = null) : InheritedWidget(child, key)
{
    public SKColor? Color { get; } = color;
    public float? Size { get; } = size;
    public override bool UpdateShouldNotify(InheritedWidget old) => ((IconTheme)old).Color != Color || ((IconTheme)old).Size != Size;
    public static IconTheme? Of(BuildContext context) => context.DependOn<IconTheme>();
}

public sealed class Icon(IconData icon, float? size = null, SKColor? color = null, Key? key = null) : StatelessWidget(key)
{
    public override Widget Build(BuildContext context)
    {
        var theme = IconTheme.Of(context);
        float s = size ?? theme?.Size ?? 24;
        var c = color ?? theme?.Color ?? DefaultTextStyle.Of(context)?.Style.Color ?? Colors.Black;
        return new CustomPaint(new IconPainter(icon, c), size: new Size(s, s));
    }
}

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
