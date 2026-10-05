using SkiaSharp;

namespace Sway.Widgets;

public readonly record struct BorderRadius(Radius TopLeft, Radius TopRight, Radius BottomRight, Radius BottomLeft)
{
    public static readonly BorderRadius Zero = new(Radius.Zero, Radius.Zero, Radius.Zero, Radius.Zero);
    public static BorderRadius Circular(float r) { var c = Radius.Circular(r); return new(c, c, c, c); }
    public static BorderRadius Only(float topLeft = 0, float topRight = 0, float bottomRight = 0, float bottomLeft = 0) =>
        new(Radius.Circular(topLeft), Radius.Circular(topRight), Radius.Circular(bottomRight), Radius.Circular(bottomLeft));
    public static implicit operator BorderRadius(float r) => Circular(r);

    public bool IsZero => this == Zero;

    public SKRoundRect ToRoundRect(Rect rect)
    {
        var rr = new SKRoundRect();
        rr.SetRectRadii(rect.ToSk(), new[]
        {
            new SKPoint(TopLeft.X, TopLeft.Y), new SKPoint(TopRight.X, TopRight.Y),
            new SKPoint(BottomRight.X, BottomRight.Y), new SKPoint(BottomLeft.X, BottomLeft.Y),
        });
        return rr;
    }

    public BorderRadius Mirrored() => new(TopRight, TopLeft, BottomLeft, BottomRight);
}
