using SkiaSharp;

namespace Sway.Widgets;

public interface IListenable
{
    void AddListener(Action listener);
    void RemoveListener(Action listener);
}
