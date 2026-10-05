using SkiaSharp;

namespace Sway.Widgets;

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
