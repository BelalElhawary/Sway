using SkiaSharp;

namespace Sway.Widgets;

public sealed class SizedBox : StatelessWidget
{
    readonly float? _width, _height;
    readonly Widget? _child;

    public SizedBox(float? width = null, float? height = null, Widget? child = null, Key? key = null) : base(key)
    {
        _width = width; _height = height; _child = child;
    }

    public static SizedBox Expand(Widget? child = null) => new(float.PositiveInfinity, float.PositiveInfinity, child);
    public static SizedBox Shrink(Widget? child = null) => new(0, 0, child);
    public static SizedBox FromSize(Size size, Widget? child = null) => new(size.Width, size.Height, child);
    public static SizedBox Square(float dimension, Widget? child = null) => new(dimension, dimension, child);

    public override Widget Build(BuildContext context) =>
        new ConstrainedBox(BoxConstraints.TightFor(_width, _height), _child);
}
