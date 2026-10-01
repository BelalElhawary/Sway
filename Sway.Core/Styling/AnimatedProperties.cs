using SkiaSharp;

namespace Sway.Core.Styling;

public enum AnimKind { Float, Length, Color, Transform, Shadows, Filters }

/// <summary>An animatable CSS property: how to read it from and write it to a computed style.</summary>
public sealed class AnimatedProperty
{
    public required string Name { get; init; }
    public required AnimKind Kind { get; init; }
    public required bool AffectsLayout { get; init; }
    public required Func<ComputedStyle, object?> Get { get; init; }
    public required Action<ComputedStyle, object?> Set { get; init; }
}

/// <summary>
/// The registry of properties that can transition or animate, plus interpolation and equality for their values.
/// Values are boxed: float, Length, SKColor, TransformList (null for none), List&lt;Shadow&gt;, List&lt;FilterOp&gt;.
/// </summary>
public static class AnimatedProperties
{
    static readonly Dictionary<string, AnimatedProperty> ByName = new();
    static readonly string[] Sides = { "top", "right", "bottom", "left" };
    static readonly string[] Corners = { "top-left", "top-right", "bottom-right", "bottom-left" };

    public static IReadOnlyCollection<AnimatedProperty> All => ByName.Values;

    public static AnimatedProperty? Find(string name) => ByName.TryGetValue(name, out var p) ? p : null;

    /// <summary>Longhand names covered by a property name, expanding shorthands like margin or border-radius.</summary>
    public static IEnumerable<string> Expand(string name)
    {
        switch (name)
        {
            case "margin": return Sides.Select(s => $"margin-{s}");
            case "padding": return Sides.Select(s => $"padding-{s}");
            case "inset": return Sides;
            case "border-width": return Sides.Select(s => $"border-{s}-width");
            case "border-color": return Sides.Select(s => $"border-{s}-color");
            case "border-radius": return Corners.Select(c => $"border-{c}-radius");
            case "border": return Sides.Select(s => $"border-{s}-width").Concat(Sides.Select(s => $"border-{s}-color"));
            case "gap": return new[] { "row-gap", "column-gap" };
            case "background": return new[] { "background-color" };
            case "outline": return new[] { "outline-color", "outline-width", "outline-offset" };
            default: return new[] { name };
        }
    }

    static AnimatedProperties()
    {
        AddFloat("opacity", false, s => s.Opacity, (s, v) => s.Opacity = Math.Clamp(v, 0, 1));
        AddFloat("font-size", true, s => s.FontSize, (s, v) => s.FontSize = Math.Max(0, v));
        AddFloat("flex-grow", true, s => s.FlexGrow, (s, v) => s.FlexGrow = Math.Max(0, v));
        AddFloat("flex-shrink", true, s => s.FlexShrink, (s, v) => s.FlexShrink = Math.Max(0, v));
        AddFloat("row-gap", true, s => s.RowGap, (s, v) => s.RowGap = Math.Max(0, v));
        AddFloat("column-gap", true, s => s.ColumnGap, (s, v) => s.ColumnGap = Math.Max(0, v));
        AddFloat("outline-width", false, s => s.OutlineWidth, (s, v) => s.OutlineWidth = Math.Max(0, v));
        AddFloat("outline-offset", false, s => s.OutlineOffset, (s, v) => s.OutlineOffset = v);

        for (int i = 0; i < 4; i++)
        {
            int side = i, corner = i;
            AddLength($"margin-{Sides[i]}", true, s => s.Margin[side], (s, v) => s.Margin[side] = v);
            AddLength($"padding-{Sides[i]}", true, s => s.Padding[side], (s, v) => s.Padding[side] = v);
            AddLength(Sides[i], true, s => s.Inset[side], (s, v) => s.Inset[side] = v);
            AddFloat($"border-{Sides[i]}-width", true, s => s.BorderWidth[side], (s, v) => s.BorderWidth[side] = Math.Max(0, v));
            AddColor($"border-{Sides[i]}-color", s => s.BorderColorOf(side), (s, v) => s.BorderColor[side] = v);
            AddFloat($"border-{Corners[i]}-radius", false, s => s.Radii[corner], (s, v) => s.Radii[corner] = Math.Max(0, v));
        }

        AddLength("width", true, s => s.Width, (s, v) => s.Width = v);
        AddLength("height", true, s => s.Height, (s, v) => s.Height = v);
        AddLength("min-width", true, s => s.MinWidth, (s, v) => s.MinWidth = v);
        AddLength("min-height", true, s => s.MinHeight, (s, v) => s.MinHeight = v);
        AddLength("max-width", true, s => s.MaxWidth, (s, v) => s.MaxWidth = v);
        AddLength("max-height", true, s => s.MaxHeight, (s, v) => s.MaxHeight = v);
        AddLength("flex-basis", true, s => s.FlexBasis, (s, v) => s.FlexBasis = v);

        AddColor("color", s => s.Color, (s, v) => s.Color = v);
        AddColor("background-color", s => s.BackgroundColor, (s, v) => s.BackgroundColor = v);
        AddColor("outline-color", s => s.OutlineColor ?? s.Color, (s, v) => s.OutlineColor = v);

        Add(new AnimatedProperty
        {
            Name = "transform", Kind = AnimKind.Transform, AffectsLayout = false,
            Get = s => s.Transform, Set = (s, v) => s.Transform = (TransformList?)v
        });
        Add(new AnimatedProperty
        {
            Name = "box-shadow", Kind = AnimKind.Shadows, AffectsLayout = false,
            Get = s => s.BoxShadows, Set = (s, v) => s.BoxShadows = (List<Shadow>)v!
        });
        Add(new AnimatedProperty
        {
            Name = "text-shadow", Kind = AnimKind.Shadows, AffectsLayout = false,
            Get = s => s.TextShadows, Set = (s, v) => s.TextShadows = (List<Shadow>)v!
        });
        Add(new AnimatedProperty
        {
            Name = "filter", Kind = AnimKind.Filters, AffectsLayout = false,
            Get = s => s.Filters, Set = (s, v) => s.Filters = (List<FilterOp>)v!
        });
    }

    static void Add(AnimatedProperty p) => ByName[p.Name] = p;

    static void AddFloat(string name, bool layout, Func<ComputedStyle, float> get, Action<ComputedStyle, float> set) =>
        Add(new AnimatedProperty { Name = name, Kind = AnimKind.Float, AffectsLayout = layout, Get = s => get(s), Set = (s, v) => set(s, (float)v!) });

    static void AddLength(string name, bool layout, Func<ComputedStyle, Length> get, Action<ComputedStyle, Length> set) =>
        Add(new AnimatedProperty { Name = name, Kind = AnimKind.Length, AffectsLayout = layout, Get = s => get(s), Set = (s, v) => set(s, (Length)v!) });

    static void AddColor(string name, Func<ComputedStyle, SKColor> get, Action<ComputedStyle, SKColor> set) =>
        Add(new AnimatedProperty { Name = name, Kind = AnimKind.Color, AffectsLayout = false, Get = s => get(s), Set = (s, v) => set(s, (SKColor)v!) });

    // ---- equality ----

    public static bool Equal(AnimKind kind, object? a, object? b)
    {
        switch (kind)
        {
            case AnimKind.Float: return MathF.Abs((float)a! - (float)b!) < 1e-4f;
            case AnimKind.Length: return (Length)a! == (Length)b!;
            case AnimKind.Color: return (SKColor)a! == (SKColor)b!;
            case AnimKind.Transform: return Equals((TransformList?)a, (TransformList?)b);
            case AnimKind.Shadows: return ((List<Shadow>)a!).SequenceEqual((List<Shadow>)b!);
            default: return ((List<FilterOp>)a!).SequenceEqual((List<FilterOp>)b!);
        }
    }

    // ---- interpolation ----

    /// <summary>Blends two values; t may leave 0..1 (easing can overshoot). Incompatible values switch at the midpoint.</summary>
    public static object? Interpolate(AnimKind kind, object? from, object? to, float t)
    {
        switch (kind)
        {
            case AnimKind.Float:
                return Lerp((float)from!, (float)to!, t);

            case AnimKind.Length:
            {
                var a = (Length)from!;
                var b = (Length)to!;
                if (a.Unit == b.Unit && a.Unit != LengthUnit.Auto) return new Length(Lerp(a.Value, b.Value, t), a.Unit);
                return t < 0.5f ? a : b;
            }

            case AnimKind.Color:
                return LerpColor((SKColor)from!, (SKColor)to!, t);

            case AnimKind.Transform:
                return LerpTransform((TransformList?)from, (TransformList?)to, t);

            case AnimKind.Shadows:
                return LerpShadows((List<Shadow>)from!, (List<Shadow>)to!, t);

            default:
                return LerpFilters((List<FilterOp>)from!, (List<FilterOp>)to!, t);
        }
    }

    static float Lerp(float a, float b, float t) => a + (b - a) * t;

    // CSS interpolates colors in premultiplied space, so a fade to transparent does not pass through grey.
    static SKColor LerpColor(SKColor a, SKColor b, float t)
    {
        float aa = a.Alpha / 255f, ba = b.Alpha / 255f;
        float alpha = Lerp(aa, ba, t);
        if (alpha <= 0) return SKColors.Transparent;

        float Channel(byte ca, byte cb) => Lerp(ca * aa, cb * ba, t) / alpha;
        byte ToByte(float v) => (byte)Math.Clamp((int)MathF.Round(v), 0, 255);
        return new SKColor(ToByte(Channel(a.Red, b.Red)), ToByte(Channel(a.Green, b.Green)), ToByte(Channel(a.Blue, b.Blue)), ToByte(alpha * 255f));
    }

    static TransformList? LerpTransform(TransformList? from, TransformList? to, float t)
    {
        if (from is null && to is null) return null;
        // "none" is compatible with any list: it stands for the same functions at their neutral values.
        var a = from ?? to!.Identity();
        var b = to ?? from!.Identity();

        if (a.Ops.Count != b.Ops.Count || a.Ops.Zip(b.Ops).Any(pair => pair.First.Kind != pair.Second.Kind))
            return t < 0.5f ? from : to;

        var ops = a.Ops.Zip(b.Ops).Select(pair =>
        {
            var (x, y) = pair;
            return new TransformOp(x.Kind, Lerp(x.A, y.A, t), Lerp(x.B, y.B, t), Lerp(x.C, y.C, t),
                Lerp(x.D, y.D, t), Lerp(x.E, y.E, t), Lerp(x.F, y.F, t));
        }).ToList();
        return new TransformList(ops);
    }

    static List<Shadow> LerpShadows(List<Shadow> from, List<Shadow> to, float t)
    {
        // Lists of different length or mixed inset-ness cannot be paired layer by layer.
        if (from.Count != to.Count || from.Zip(to).Any(p => p.First.Inset != p.Second.Inset))
            return t < 0.5f ? from : to;

        return from.Zip(to).Select(p => new Shadow(
            Lerp(p.First.X, p.Second.X, t), Lerp(p.First.Y, p.Second.Y, t),
            Math.Max(0, Lerp(p.First.Blur, p.Second.Blur, t)), Lerp(p.First.Spread, p.Second.Spread, t),
            LerpColor(p.First.Color, p.Second.Color, t), p.First.Inset)).ToList();
    }

    static List<FilterOp> LerpFilters(List<FilterOp> from, List<FilterOp> to, float t)
    {
        if (from.Count != to.Count || from.Zip(to).Any(p => p.First.Kind != p.Second.Kind))
            return t < 0.5f ? from : to;

        return from.Zip(to).Select(p => new FilterOp(p.First.Kind, Lerp(p.First.Amount, p.Second.Amount, t),
            Lerp(p.First.X, p.Second.X, t), Lerp(p.First.Y, p.Second.Y, t), LerpColor(p.First.Color, p.Second.Color, t))).ToList();
    }
}
