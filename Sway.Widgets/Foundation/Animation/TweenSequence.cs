using SkiaSharp;

namespace Sway.Widgets;

/// <summary>Plays several tweens one after another, each taking a share of the time proportional to its weight (keyframes).</summary>
public sealed class TweenSequence<T> : Animatable<T> where T : notnull
{
    readonly List<(Animatable<T> item, float start, float end)> _items = new();

    public TweenSequence(IEnumerable<(Animatable<T> item, float weight)> items)
    {
        var list = items.ToList();
        float total = list.Sum(i => i.weight), acc = 0;
        foreach (var (item, weight) in list)
        {
            _items.Add((item, acc / total, (acc + weight) / total));
            acc += weight;
        }
    }

    public override T Transform(float t)
    {
        foreach (var (item, start, end) in _items)
            if (t <= end) return item.Transform(end == start ? 1 : (t - start) / (end - start));
        return _items[^1].item.Transform(1);
    }
}
