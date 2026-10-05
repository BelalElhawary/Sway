using SkiaSharp;

namespace Sway.Widgets;

/// <summary>Blends two colour ramps that may have different numbers of stops by sampling both at the union of their stop positions.</summary>
static class GradientMath
{
    static float[] Positions(int count, IReadOnlyList<float>? stops)
    {
        if (stops is not null && stops.Count == count) return stops.ToArray();
        var even = new float[count];
        for (int i = 0; i < count; i++) even[i] = count == 1 ? 0 : i / (float)(count - 1);
        return even;
    }

    static SKColor ColorAt(IReadOnlyList<SKColor> colors, float[] positions, float p)
    {
        if (p <= positions[0]) return colors[0];
        for (int i = 1; i < positions.Length; i++)
        {
            if (p > positions[i]) continue;
            float span = positions[i] - positions[i - 1];
            return span <= 0 ? colors[i] : Lerps.Color(colors[i - 1], colors[i], (p - positions[i - 1]) / span);
        }
        return colors[^1];
    }

    public static (List<SKColor> colors, List<float> stops) Blend(IReadOnlyList<SKColor> a, IReadOnlyList<float>? stopsA,
        IReadOnlyList<SKColor> b, IReadOnlyList<float>? stopsB, float t)
    {
        var pa = Positions(a.Count, stopsA);
        var pb = Positions(b.Count, stopsB);
        var union = pa.Concat(pb).Distinct().OrderBy(p => p).ToList();
        var colors = union.Select(p => Lerps.Color(ColorAt(a, pa, p), ColorAt(b, pb, p), t)).ToList();
        return (colors, union);
    }
}
