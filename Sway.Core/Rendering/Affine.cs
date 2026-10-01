using SkiaSharp;
using Sway.Core.Dom;
using Sway.Core.Styling;

namespace Sway.Core.Rendering;

/// <summary>
/// A 2D affine transform with CSS matrix(a, b, c, d, e, f) semantics:
/// x' = a*x + c*y + e, y' = b*x + d*y + f. Used for transforms and for mapping pointer positions back into layout space.
/// </summary>
public readonly record struct Affine(float A, float B, float C, float D, float E, float F)
{
    public static readonly Affine Identity = new(1, 0, 0, 1, 0, 0);

    public static Affine Translate(float x, float y) => new(1, 0, 0, 1, x, y);
    public static Affine Scale(float x, float y) => new(x, 0, 0, y, 0, 0);

    /// <summary>The transform that applies <paramref name="inner"/> first, then <paramref name="outer"/>.</summary>
    public static Affine Compose(Affine outer, Affine inner) => new(
        outer.A * inner.A + outer.C * inner.B,
        outer.B * inner.A + outer.D * inner.B,
        outer.A * inner.C + outer.C * inner.D,
        outer.B * inner.C + outer.D * inner.D,
        outer.A * inner.E + outer.C * inner.F + outer.E,
        outer.B * inner.E + outer.D * inner.F + outer.F);

    public SKPoint Map(float x, float y) => new(A * x + C * y + E, B * x + D * y + F);

    public bool TryInvert(out Affine inverse)
    {
        float det = A * D - B * C;
        if (MathF.Abs(det) < 1e-9f) { inverse = Identity; return false; }

        float inv = 1f / det;
        inverse = new Affine(D * inv, -B * inv, -C * inv, A * inv, (C * F - D * E) * inv, (B * E - A * F) * inv);
        return true;
    }

    public SKMatrix ToSkia() => new()
    {
        ScaleX = A, SkewX = C, TransX = E,
        SkewY = B, ScaleY = D, TransY = F,
        Persp0 = 0, Persp1 = 0, Persp2 = 1
    };

    /// <summary>The matrix for one transform function, for a box of the given size (percent translations need it).</summary>
    public static Affine FromOp(TransformOp op, float width, float height)
    {
        switch (op.Kind)
        {
            case TransformKind.Translate:
                return Translate(op.A + op.C / 100f * width, op.B + op.D / 100f * height);
            case TransformKind.Scale:
                return Scale(op.A, op.B);
            case TransformKind.Rotate:
            {
                float r = op.A * MathF.PI / 180f, cos = MathF.Cos(r), sin = MathF.Sin(r);
                return new Affine(cos, sin, -sin, cos, 0, 0);
            }
            case TransformKind.Skew:
                return new Affine(1, MathF.Tan(op.B * MathF.PI / 180f), MathF.Tan(op.A * MathF.PI / 180f), 1, 0, 0);
            default:
                return new Affine(op.A, op.B, op.C, op.D, op.E, op.F);
        }
    }

    /// <summary>
    /// The full transform of an element in layout space: its transform list applied around its transform-origin.
    /// </summary>
    public static Affine OfElement(ElementNode el)
    {
        var list = el.Style.Transform;
        if (list is null) return Identity;

        var box = el.BorderRect;
        float ox = box.Left + (el.Style.TransformOriginX.Resolve(box.Width) ?? box.Width / 2);
        float oy = box.Top + (el.Style.TransformOriginY.Resolve(box.Height) ?? box.Height / 2);

        var m = Identity;
        foreach (var op in list.Ops) m = Compose(m, FromOp(op, box.Width, box.Height));
        return Compose(Translate(ox, oy), Compose(m, Translate(-ox, -oy)));
    }
}
