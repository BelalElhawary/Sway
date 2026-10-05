using SkiaSharp;

namespace Sway.Widgets;

public interface ITweenVisitor
{
    Tween<T>? Visit<T>(Tween<T>? tween, T? target, Func<T, Tween<T>> create) where T : class;
    Tween<T>? VisitValue<T>(Tween<T>? tween, T? target, Func<T, Tween<T>> create) where T : struct;
}
