using SkiaSharp;
using Sway.Core.Styling;

namespace Sway.Core.Rendering;

/// <summary>Turns a parsed CSS gradient into a Skia shader for a given box.</summary>
public static class GradientShader
{
    public static SKShader? Create(Gradient g, SKRect box)
    {
        if (box.Width <= 0 || box.Height <= 0) return null;
        return g.IsRadial ? CreateRadial(g, box) : CreateLinear(g, box);
    }

    static SKShader? CreateLinear(Gradient g, SKRect box)
    {
        float w = box.Width, h = box.Height;

        // Unit direction of the gradient line.
        float dx, dy;
        if (g.CornerX != 0 && g.CornerY != 0)
        {
            // "to top right" and friends: perpendicular to the diagonal through the other two corners.
            dx = g.CornerX * h;
            dy = g.CornerY * w;
            float len = MathF.Sqrt(dx * dx + dy * dy);
            dx /= len;
            dy /= len;
        }
        else
        {
            float rad = g.AngleDegrees * MathF.PI / 180f;
            dx = MathF.Sin(rad);
            dy = -MathF.Cos(rad);
        }

        // The line is long enough that the corners nearest each end get exactly the first and last colors.
        float length = MathF.Abs(w * dx) + MathF.Abs(h * dy);
        if (length <= 0) return null;

        var (colors, positions) = ResolveStops(g, length);
        if (colors.Length < 2) return null;
        if (!g.Repeating) positions = positions.Select(p => Math.Clamp(p, 0f, 1f)).ToArray(); // Skia only accepts 0..1

        float cx = box.MidX, cy = box.MidY;
        var tile = g.Repeating ? SKShaderTileMode.Repeat : SKShaderTileMode.Clamp;
        float first = 0, span = 1;

        if (g.Repeating)
        {
            // Skia repeats the whole 0..1 range, so stretch it to cover just the first-to-last stop span.
            first = positions[0];
            span = positions[^1] - positions[0];
            if (span <= 0) return null;
            positions = positions.Select(p => (p - first) / span).ToArray();
        }

        var start = new SKPoint(cx + dx * length * (first - 0.5f), cy + dy * length * (first - 0.5f));
        var end = new SKPoint(start.X + dx * length * span, start.Y + dy * length * span);
        return SKShader.CreateLinearGradient(start, end, colors, positions, tile);
    }

    static SKShader? CreateRadial(Gradient g, SKRect box)
    {
        float w = box.Width, h = box.Height;
        float cx = box.Left + (g.CenterX.Resolve(w) ?? w / 2);
        float cy = box.Top + (g.CenterY.Resolve(h) ?? h / 2);

        float left = cx - box.Left, right = box.Right - cx, top = cy - box.Top, bottom = box.Bottom - cy;
        float closestX = MathF.Min(left, right), farthestX = MathF.Max(left, right);
        float closestY = MathF.Min(top, bottom), farthestY = MathF.Max(top, bottom);

        float rx, ry;
        switch (g.Size)
        {
            case RadialSize.Explicit:
                rx = g.RadiusX.Resolve(w) ?? 0;
                ry = g.IsCircle ? rx : g.RadiusY.Resolve(h) ?? 0;
                break;
            case RadialSize.ClosestSide:
                rx = closestX; ry = closestY;
                if (g.IsCircle) rx = ry = MathF.Min(rx, ry);
                break;
            case RadialSize.FarthestSide:
                rx = farthestX; ry = farthestY;
                if (g.IsCircle) rx = ry = MathF.Max(rx, ry);
                break;
            case RadialSize.ClosestCorner:
                rx = closestX * MathF.Sqrt(2); ry = closestY * MathF.Sqrt(2);
                if (g.IsCircle) rx = ry = MathF.Sqrt(closestX * closestX + closestY * closestY);
                break;
            default: // farthest-corner
                rx = farthestX * MathF.Sqrt(2); ry = farthestY * MathF.Sqrt(2);
                if (g.IsCircle) rx = ry = MathF.Sqrt(farthestX * farthestX + farthestY * farthestY);
                break;
        }
        if (rx <= 0 || ry <= 0) return null;

        var (colors, positions) = ResolveStops(g, rx);
        if (colors.Length < 2) return null;
        if (!g.Repeating) positions = positions.Select(p => Math.Clamp(p, 0f, 1f)).ToArray();

        var tile = g.Repeating ? SKShaderTileMode.Repeat : SKShaderTileMode.Clamp;
        float radius = rx;
        if (g.Repeating)
        {
            float span = positions[^1] - positions[0];
            if (span <= 0) return null;
            float first = positions[0];
            positions = positions.Select(p => (p - first) / span).ToArray();
            // A repeating radial gradient starts at its first stop, which may not be the centre.
            radius = rx * span;
        }

        // Ellipses are circles squashed along y, so scale the shader around the centre.
        var local = SKMatrix.CreateScale(1, ry / rx, cx, cy);
        return SKShader.CreateRadialGradient(new SKPoint(cx, cy), radius, colors, positions, tile, local);
    }

    /// <summary>
    /// Resolves stop positions to fractions of <paramref name="length"/>: explicit lengths and
    /// percentages first, then evenly distributing the stops that have none, and keeping them non-decreasing.
    /// </summary>
    static (SKColor[] colors, float[] positions) ResolveStops(Gradient g, float length)
    {
        int n = g.Stops.Count;
        var pos = new float?[n];
        for (int i = 0; i < n; i++)
        {
            var p = g.Stops[i].Position;
            if (p.IsAuto) continue;
            pos[i] = p.Unit == LengthUnit.Percent ? p.Value / 100f : p.Value / length;
        }

        pos[0] ??= 0;
        pos[n - 1] ??= 1;

        // Spread unpositioned stops evenly between the nearest positioned ones.
        for (int i = 1; i < n - 1; i++)
        {
            if (pos[i] is not null) continue;
            int next = i + 1;
            while (pos[next] is null) next++;
            int prev = i - 1;
            float from = pos[prev]!.Value, to = pos[next]!.Value;
            for (int k = i; k < next; k++) pos[k] = from + (to - from) * (k - prev) / (next - prev);
        }

        var positions = new float[n];
        float last = float.NegativeInfinity;
        for (int i = 0; i < n; i++)
        {
            last = MathF.Max(last, pos[i]!.Value);
            positions[i] = last;
        }

        var colors = g.Stops.Select(s => s.Color).ToArray();

        // "transparent" is rgba(0,0,0,0); borrowing a neighbour's RGB avoids a grey fringe when interpolating.
        for (int i = 0; i < n; i++)
        {
            if (colors[i].Alpha != 0) continue;
            int source = i > 0 && colors[i - 1].Alpha != 0 ? i - 1 : i + 1 < n && colors[i + 1].Alpha != 0 ? i + 1 : i;
            colors[i] = colors[source].WithAlpha(0);
        }

        return (colors, positions);
    }
}
