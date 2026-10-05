using SkiaSharp;

namespace Sway.Widgets;

public readonly record struct Border(BorderSide Left, BorderSide Top, BorderSide Right, BorderSide Bottom)
{
    public static Border All(SKColor color, float width = 1) { var s = new BorderSide(color, width); return new(s, s, s, s); }
    public static Border Only(BorderSide? left = null, BorderSide? top = null, BorderSide? right = null, BorderSide? bottom = null) =>
        new(left ?? BorderSide.None, top ?? BorderSide.None, right ?? BorderSide.None, bottom ?? BorderSide.None);
    public EdgeInsets Dimensions => new(Left.Width, Top.Width, Right.Width, Bottom.Width);
    public bool IsUniform => Left == Top && Top == Right && Right == Bottom;
}
