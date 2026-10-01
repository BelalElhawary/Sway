using SkiaSharp;

namespace Sway.Core.Styling;

/// <summary>One box-shadow or text-shadow layer. Spread and Inset only apply to box shadows.</summary>
public readonly record struct Shadow(float X, float Y, float Blur, float Spread, SKColor Color, bool Inset);

public readonly record struct ColorStop(SKColor Color, Length Position)
{
    public bool HasPosition => !Position.IsAuto;
}

public enum RadialSize { ClosestSide, ClosestCorner, FarthestSide, FarthestCorner, Explicit }

/// <summary>A linear or radial gradient background image.</summary>
public sealed class Gradient
{
    public bool IsRadial { get; init; }
    public bool Repeating { get; init; }

    // Linear: an angle in degrees (0 = up, clockwise), or a corner direction whose angle depends on the box.
    public float AngleDegrees { get; init; } = 180;
    public int CornerX { get; init; }
    public int CornerY { get; init; }

    // Radial.
    public bool IsCircle { get; init; }
    public RadialSize Size { get; init; } = RadialSize.FarthestCorner;
    public Length RadiusX { get; init; } = Length.Auto;
    public Length RadiusY { get; init; } = Length.Auto;
    public Length CenterX { get; init; } = new(50, LengthUnit.Percent);
    public Length CenterY { get; init; } = new(50, LengthUnit.Percent);

    public required List<ColorStop> Stops { get; init; }
}

public enum TransformKind { Translate, Scale, Rotate, Skew, Matrix }

/// <summary>
/// One transform function. Translate: A,B in px and C,D in percent of the box; Scale: A,B;
/// Rotate: A degrees; Skew: A,B degrees; Matrix: A..F as in CSS matrix().
/// </summary>
public readonly record struct TransformOp(TransformKind Kind, float A, float B = 0, float C = 0, float D = 0, float E = 0, float F = 0);

/// <summary>An ordered list of transform functions with value equality, so style changes can be detected.</summary>
public sealed class TransformList : IEquatable<TransformList>
{
    public TransformList(IReadOnlyList<TransformOp> ops) => Ops = ops;

    public IReadOnlyList<TransformOp> Ops { get; }

    public bool Equals(TransformList? other) => other is not null && Ops.SequenceEqual(other.Ops);
    public override bool Equals(object? obj) => Equals(obj as TransformList);
    public override int GetHashCode() => Ops.Aggregate(17, (h, op) => HashCode.Combine(h, op));

    /// <summary>The same function kinds with neutral values, the starting point when animating from "none".</summary>
    public TransformList Identity() => new(Ops.Select(op => op.Kind switch
    {
        TransformKind.Scale => new TransformOp(TransformKind.Scale, 1, 1),
        TransformKind.Matrix => new TransformOp(TransformKind.Matrix, 1, 0, 0, 1, 0, 0),
        _ => new TransformOp(op.Kind, 0)
    }).ToList());
}

public enum FilterKind { Blur, Brightness, Contrast, Grayscale, Sepia, Saturate, HueRotate, Invert, Opacity, DropShadow }

/// <summary>One filter function. DropShadow uses X, Y, Amount (blur) and Color; others use Amount only.</summary>
public readonly record struct FilterOp(FilterKind Kind, float Amount, float X = 0, float Y = 0, SKColor Color = default);
