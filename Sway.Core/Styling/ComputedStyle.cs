using SkiaSharp;

namespace Sway.Core.Styling;

public enum Display { Inline, Block, InlineBlock, Flex, InlineFlex, Grid, InlineGrid, None }
public enum FlexDirection { Row, RowReverse, Column, ColumnReverse }
public enum Position { Static, Relative, Absolute, Fixed, Sticky }
public enum Overflow { Visible, Hidden, Scroll, Auto }
public enum BorderLineStyle { Solid, Dashed, Dotted, Double }
public enum TextTransform { None, Uppercase, Lowercase, Capitalize }
public enum AnimationDirection { Normal, Reverse, Alternate, AlternateReverse }
public enum AnimationFill { None, Forwards, Backwards, Both }

/// <summary>One resolved transition: the property name ("all" covers everything), timing in ms, and easing.</summary>
public sealed record TransitionSpec(string Property, float DurationMs, float DelayMs, TimingFunction Timing);

/// <summary>One resolved animation from the animation-* lists.</summary>
public sealed record AnimationSpec(string Name, float DurationMs, float DelayMs, TimingFunction Timing,
    float Iterations, AnimationDirection Direction, AnimationFill Fill, bool Paused);

[Flags]
public enum TextDecoration { None = 0, Underline = 1, LineThrough = 2 }

/// <summary>Shared by justify-content, align-items/self/content and justify-items/self.</summary>
public enum Align { Auto, Stretch, Start, End, Center, Baseline, SpaceBetween, SpaceAround, SpaceEvenly }

public enum TrackKind { Px, Percent, Fr, Auto, MinContent, MaxContent }

public readonly record struct TrackSize(TrackKind Kind, float Value)
{
    public static readonly TrackSize Auto = new(TrackKind.Auto, 0);
}

/// <summary>A grid track: a plain size has Min == Max (fr and auto use an auto minimum).</summary>
public readonly record struct Track(TrackSize Min, TrackSize Max)
{
    public static readonly Track Auto = new(TrackSize.Auto, TrackSize.Auto);
}

/// <summary>Explicit tracks, plus an optional repeat(auto-fill | auto-fit, ...) block whose count depends on the container size.</summary>
public sealed class TrackTemplate
{
    public List<Track> Before { get; } = new();
    public List<Track>? AutoPattern { get; set; }
    public List<Track> After { get; } = new();

    public static readonly TrackTemplate Empty = new();

    public List<Track> Resolve(float? available, float gap)
    {
        var tracks = new List<Track>(Before);
        if (AutoPattern is { Count: > 0 } pattern)
        {
            int repeats = 1;
            if (available is { } space)
            {
                // Each repetition needs the pattern's minimum sizes; fixed tracks outside the repeat count too.
                int outside = Before.Count + After.Count;
                float fixedSize = Before.Concat(After).Sum(MinimumPx) + gap * outside;
                float one = pattern.Sum(MinimumPx) + gap * pattern.Count;
                repeats = one <= 0 ? 1 : Math.Max(1, (int)Math.Floor((space - fixedSize + gap) / one));
            }
            for (int i = 0; i < repeats; i++) tracks.AddRange(pattern);
        }
        tracks.AddRange(After);
        return tracks;
    }

    static float MinimumPx(Track t) => t.Min.Kind == TrackKind.Px ? t.Min.Value : t.Max.Kind == TrackKind.Px ? t.Max.Value : 0;
}

/// <summary>One edge of a grid item's placement: auto, a line number, or a span.</summary>
public readonly record struct GridLine(bool IsSpan, int Value)
{
    public static readonly GridLine Auto = new(false, 0);
    public bool IsAuto => !IsSpan && Value == 0;
}
public enum Direction { Ltr, Rtl }
public enum TextAlign { Start, Left, Center, Right, End }
public enum LengthUnit { Px, Percent, Auto }

public readonly record struct Length(float Value, LengthUnit Unit)
{
    public static readonly Length Auto = new(0, LengthUnit.Auto);
    public static readonly Length Zero = new(0, LengthUnit.Px);

    public bool IsAuto => Unit == LengthUnit.Auto;
    public static Length Px(float v) => new(v, LengthUnit.Px);

    /// <summary>Resolves against a containing size; null when auto.</summary>
    public float? Resolve(float containing) => Unit switch
    {
        LengthUnit.Px => Value,
        LengthUnit.Percent => Value / 100f * containing,
        _ => null
    };
}

/// <summary>Sides are indexed top, right, bottom, left.</summary>
public sealed class ComputedStyle
{
    public const int Top = 0, Right = 1, Bottom = 2, Left = 3;

    // Inherited.
    public Direction Direction = Direction.Ltr;
    public SKColor Color = SKColors.Black;
    public string FontFamily = "Segoe UI";
    public float FontSize = 16;
    public int FontWeight = 400;
    public bool Italic;
    /// <summary>Multiplier when <see cref="LineHeightIsMultiplier"/>, otherwise pixels; null means "normal".</summary>
    public float? LineHeight;
    public bool LineHeightIsMultiplier = true;
    public TextAlign TextAlign = TextAlign.Start;
    public TextAlign EffectiveTextAlign => TextAlign switch
    {
        TextAlign.Start => Direction == Direction.Rtl ? TextAlign.Right : TextAlign.Left,
        TextAlign.End => Direction == Direction.Rtl ? TextAlign.Left : TextAlign.Right,
        _ => TextAlign
    };
    public string Cursor = "auto";
    public List<Shadow> TextShadows = NoShadows;
    public TextDecoration TextDecoration;
    public TextTransform TextTransform;
    /// <summary>Tint for checkboxes and radios; null uses the default blue.</summary>
    public SKColor? AccentColor;
    public bool VisibilityHidden;
    public bool PointerEventsNone;
    public Dictionary<string, string> CustomProperties = new();

    // Not inherited.
    public Display Display = Display.Inline;
    public SKColor BackgroundColor = SKColors.Transparent;
    public Length Width = Length.Auto, Height = Length.Auto;
    public Length MinWidth = Length.Auto, MinHeight = Length.Auto;
    public Length MaxWidth = Length.Auto, MaxHeight = Length.Auto;
    public Length[] Margin = { Length.Zero, Length.Zero, Length.Zero, Length.Zero };
    public Length[] Padding = { Length.Zero, Length.Zero, Length.Zero, Length.Zero };
    public float[] BorderWidth = new float[4];
    /// <summary>Null means currentColor.</summary>
    public SKColor?[] BorderColor = new SKColor?[4];
    public BorderLineStyle[] BorderStyle = { BorderLineStyle.Solid, BorderLineStyle.Solid, BorderLineStyle.Solid, BorderLineStyle.Solid };
    /// <summary>Corner radii in px: top-left, top-right, bottom-right, bottom-left.</summary>
    public float[] Radii = new float[4];

    /// <summary>Percentage radii (border-radius: 50%), added to <see cref="Radii"/> once the box size is known.</summary>
    public float[] RadiiPercent = new float[4];

    /// <summary>Radii for a box of the given size. Percentages use the smaller side, so 50% gives a circle or a pill.</summary>
    public float[] EffectiveRadii(float width, float height)
    {
        float basis = Math.Min(width, height);
        var result = new float[4];
        for (int i = 0; i < 4; i++) result[i] = Radii[i] + RadiiPercent[i] / 100f * basis;
        return result;
    }

    // Effects.
    public List<Shadow> BoxShadows = NoShadows;
    public List<Gradient> BackgroundGradients = NoGradients;
    public TransformList? Transform;
    public Length TransformOriginX = new(50, LengthUnit.Percent), TransformOriginY = new(50, LengthUnit.Percent);
    public List<FilterOp> Filters = NoFilters;

    public static readonly List<Shadow> NoShadows = new();
    public static readonly List<Gradient> NoGradients = new();
    public static readonly List<FilterOp> NoFilters = new();
    public bool BorderBox;
    public float Opacity = 1;

    // Positioning. Inset sides are indexed top, right, bottom, left.
    public Position Position = Position.Static;
    public Length[] Inset = { Length.Auto, Length.Auto, Length.Auto, Length.Auto };
    /// <summary>Null means auto.</summary>
    public int? ZIndex;
    public Overflow OverflowX = Overflow.Visible, OverflowY = Overflow.Visible;
    public float OutlineWidth, OutlineOffset;
    /// <summary>Null means currentColor.</summary>
    public SKColor? OutlineColor;

    public bool ClipsContent => OverflowX != Overflow.Visible || OverflowY != Overflow.Visible;
    public bool IsOutOfFlow => Position is Position.Absolute or Position.Fixed;

    // Transitions and animations. Longhand lists cycle to the length of the property or name list, as in CSS.
    public List<string> TransitionProperty = DefaultTransitionProperty;
    public List<float> TransitionDuration = DefaultZero, TransitionDelay = DefaultZero;
    public List<TimingFunction> TransitionTiming = DefaultEase;
    public List<string> AnimationName = DefaultNone;
    public List<float> AnimationDuration = DefaultZero, AnimationDelay = DefaultZero, AnimationIterations = DefaultOne;
    public List<TimingFunction> AnimationTiming = DefaultEase;
    public List<AnimationDirection> AnimationDirections = new() { AnimationDirection.Normal };
    public List<AnimationFill> AnimationFills = new() { AnimationFill.None };
    public List<bool> AnimationPaused = new() { false };

    static readonly List<string> DefaultTransitionProperty = new() { "all" };
    static readonly List<string> DefaultNone = new() { "none" };
    static readonly List<float> DefaultZero = new() { 0f };
    static readonly List<float> DefaultOne = new() { 1f };
    static readonly List<TimingFunction> DefaultEase = new() { TimingFunction.Ease };

    public List<TransitionSpec> TransitionSpecs()
    {
        var specs = new List<TransitionSpec>();
        for (int i = 0; i < TransitionProperty.Count; i++)
            specs.Add(new TransitionSpec(TransitionProperty[i], TransitionDuration[i % TransitionDuration.Count],
                TransitionDelay[i % TransitionDelay.Count], TransitionTiming[i % TransitionTiming.Count]));
        return specs;
    }

    public List<AnimationSpec> AnimationSpecs()
    {
        var specs = new List<AnimationSpec>();
        for (int i = 0; i < AnimationName.Count; i++)
        {
            if (AnimationName[i] == "none") continue;
            specs.Add(new AnimationSpec(AnimationName[i], AnimationDuration[i % AnimationDuration.Count],
                AnimationDelay[i % AnimationDelay.Count], AnimationTiming[i % AnimationTiming.Count],
                AnimationIterations[i % AnimationIterations.Count], AnimationDirections[i % AnimationDirections.Count],
                AnimationFills[i % AnimationFills.Count], AnimationPaused[i % AnimationPaused.Count]));
        }
        return specs;
    }

    /// <summary>
    /// True when two styles lay out identically. Colors, shadows, transforms, cursors and the like are
    /// paint-only, so a style change confined to them does not need a relayout.
    /// </summary>
    public bool LayoutEquals(ComputedStyle o) =>
        Direction == o.Direction
        && Display == o.Display && Position == o.Position && OverflowX == o.OverflowX && OverflowY == o.OverflowY
        && Width == o.Width && Height == o.Height && MinWidth == o.MinWidth && MinHeight == o.MinHeight
        && MaxWidth == o.MaxWidth && MaxHeight == o.MaxHeight && BorderBox == o.BorderBox
        && Same(Margin, o.Margin) && Same(Padding, o.Padding) && Same(Inset, o.Inset) && Same(BorderWidth, o.BorderWidth)
        && FontFamily == o.FontFamily && FontSize == o.FontSize && FontWeight == o.FontWeight && Italic == o.Italic
        && LineHeight == o.LineHeight && LineHeightIsMultiplier == o.LineHeightIsMultiplier && TextAlign == o.TextAlign
        && FlexDirection == o.FlexDirection && FlexWrap == o.FlexWrap && FlexGrow == o.FlexGrow && FlexShrink == o.FlexShrink
        && FlexBasis == o.FlexBasis && Order == o.Order && RowGap == o.RowGap && ColumnGap == o.ColumnGap
        && JustifyContent == o.JustifyContent && AlignContent == o.AlignContent && AlignItems == o.AlignItems
        && AlignSelf == o.AlignSelf && JustifyItems == o.JustifyItems && JustifySelf == o.JustifySelf
        && ReferenceEquals(GridColumns, o.GridColumns) && ReferenceEquals(GridRows, o.GridRows)
        && GridAutoColumns == o.GridAutoColumns && GridAutoRows == o.GridAutoRows
        && ColumnStart == o.ColumnStart && ColumnEnd == o.ColumnEnd && RowStart == o.RowStart && RowEnd == o.RowEnd;

    static bool Same<T>(T[] a, T[] b) where T : IEquatable<T>
    {
        for (int i = 0; i < a.Length; i++)
            if (!a[i].Equals(b[i])) return false;
        return true;
    }

    /// <summary>A deep copy, used to evaluate keyframe declarations without disturbing the real style.</summary>
    public ComputedStyle Clone()
    {
        var copy = (ComputedStyle)MemberwiseClone();
        copy.Margin = (Length[])Margin.Clone();
        copy.Padding = (Length[])Padding.Clone();
        copy.Inset = (Length[])Inset.Clone();
        copy.BorderWidth = (float[])BorderWidth.Clone();
        copy.BorderColor = (SKColor?[])BorderColor.Clone();
        copy.BorderStyle = (BorderLineStyle[])BorderStyle.Clone();
        copy.Radii = (float[])Radii.Clone();
        copy.RadiiPercent = (float[])RadiiPercent.Clone();
        return copy;
    }

    // Flexbox.
    public FlexDirection FlexDirection = FlexDirection.Row;
    public bool FlexWrap;
    public float FlexGrow, FlexShrink = 1;
    public Length FlexBasis = Length.Auto;
    public int Order;

    // Alignment (flex and grid).
    /// <summary>Stretch stands for the initial value "normal": start for flex, stretched auto tracks for grid.</summary>
    public Align JustifyContent = Align.Stretch;
    public Align AlignContent = Align.Stretch;
    public Align AlignItems = Align.Stretch;
    public Align AlignSelf = Align.Auto;
    public Align JustifyItems = Align.Stretch;
    public Align JustifySelf = Align.Auto;
    public float RowGap, ColumnGap;

    // Grid.
    public TrackTemplate GridColumns = TrackTemplate.Empty, GridRows = TrackTemplate.Empty;
    public Track GridAutoColumns = Track.Auto, GridAutoRows = Track.Auto;
    public GridLine ColumnStart = GridLine.Auto, ColumnEnd = GridLine.Auto;
    public GridLine RowStart = GridLine.Auto, RowEnd = GridLine.Auto;

    public static ComputedStyle CreateRoot() => new();

    /// <summary>A fresh style that carries over only the inherited properties.</summary>
    public ComputedStyle InheritFrom(ComputedStyle parent) => new()
    {
        Direction = parent.Direction,
        Color = parent.Color,
        FontFamily = parent.FontFamily,
        FontSize = parent.FontSize,
        FontWeight = parent.FontWeight,
        Italic = parent.Italic,
        LineHeight = parent.LineHeight,
        LineHeightIsMultiplier = parent.LineHeightIsMultiplier,
        TextAlign = parent.TextAlign,
        Cursor = parent.Cursor,
        TextShadows = parent.TextShadows,
        TextDecoration = parent.TextDecoration,
        TextTransform = parent.TextTransform,
        AccentColor = parent.AccentColor,
        VisibilityHidden = parent.VisibilityHidden,
        PointerEventsNone = parent.PointerEventsNone,
        CustomProperties = parent.CustomProperties,
    };

    public SKColor BorderColorOf(int side) => BorderColor[side] ?? Color;
}
