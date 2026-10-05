using SkiaSharp;

namespace Sway.Widgets;

public sealed class HitTestResult
{
    readonly List<HitTestEntry> _path = new();
    SKMatrix _current = SKMatrix.Identity;

    public IReadOnlyList<HitTestEntry> Path => _path;

    internal void Add(RenderObject target) => _path.Add(new HitTestEntry(target, _current));

    /// <summary>Tests a child whose origin sits at <paramref name="offset"/> inside the current object.</summary>
    public bool AddWithPaintOffset(Offset offset, Offset position, Func<HitTestResult, Offset, bool> hitTest)
    {
        var saved = _current;
        _current = SKMatrix.Concat(SKMatrix.CreateTranslation(-offset.Dx, -offset.Dy), _current);
        bool hit = hitTest(this, position - offset);
        _current = saved;
        return hit;
    }

    /// <summary>Tests a child painted under <paramref name="forward"/> (child-local to parent-local).</summary>
    public bool AddWithTransform(SKMatrix forward, Offset position, Func<HitTestResult, Offset, bool> hitTest)
    {
        if (!forward.TryInvert(out var inverse)) return false;
        var saved = _current;
        _current = SKMatrix.Concat(inverse, _current);
        var p = inverse.MapPoint(position.Dx, position.Dy);
        bool hit = hitTest(this, new Offset(p.X, p.Y));
        _current = saved;
        return hit;
    }
}
