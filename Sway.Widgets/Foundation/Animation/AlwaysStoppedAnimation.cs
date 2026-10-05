using SkiaSharp;

namespace Sway.Widgets;

public sealed class AlwaysStoppedAnimation<T>(T value) : Animation<T>
{
    public override T Value => value;
    public override AnimationStatus Status => AnimationStatus.Forward;
}
