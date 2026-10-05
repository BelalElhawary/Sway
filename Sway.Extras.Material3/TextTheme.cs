using SkiaSharp;

using Sway.Widgets;

namespace Sway.Extras.Material3;

/// <summary>The Material 3 type scale. Fonts fall back to Segoe UI when Roboto is not installed.</summary>
public sealed record TextTheme
{
    const string Family = "Roboto, sans-serif";

    static TextStyle S(float size, float line, int weight, float tracking) =>
        new(FontSize: size, Height: line / size, FontWeight: weight, LetterSpacing: tracking, FontFamily: Family);

    public TextStyle DisplayLarge { get; init; } = S(57, 64, 400, -0.25f);
    public TextStyle DisplayMedium { get; init; } = S(45, 52, 400, 0);
    public TextStyle DisplaySmall { get; init; } = S(36, 44, 400, 0);
    public TextStyle HeadlineLarge { get; init; } = S(32, 40, 400, 0);
    public TextStyle HeadlineMedium { get; init; } = S(28, 36, 400, 0);
    public TextStyle HeadlineSmall { get; init; } = S(24, 32, 400, 0);
    public TextStyle TitleLarge { get; init; } = S(22, 28, 400, 0);
    public TextStyle TitleMedium { get; init; } = S(16, 24, 500, 0.15f);
    public TextStyle TitleSmall { get; init; } = S(14, 20, 500, 0.1f);
    public TextStyle BodyLarge { get; init; } = S(16, 24, 400, 0.5f);
    public TextStyle BodyMedium { get; init; } = S(14, 20, 400, 0.25f);
    public TextStyle BodySmall { get; init; } = S(12, 16, 400, 0.4f);
    public TextStyle LabelLarge { get; init; } = S(14, 20, 500, 0.1f);
    public TextStyle LabelMedium { get; init; } = S(12, 16, 500, 0.5f);
    public TextStyle LabelSmall { get; init; } = S(11, 16, 500, 0.5f);

    /// <summary>Applies colour to every style (used so text defaults to on-surface).</summary>
    public TextTheme Apply(SKColor color, SKColor? displayColor = null)
    {
        TextStyle C(TextStyle s) => s with { Color = color };
        return this with
        {
            DisplayLarge = C(DisplayLarge), DisplayMedium = C(DisplayMedium), DisplaySmall = C(DisplaySmall),
            HeadlineLarge = C(HeadlineLarge), HeadlineMedium = C(HeadlineMedium), HeadlineSmall = C(HeadlineSmall),
            TitleLarge = C(TitleLarge), TitleMedium = C(TitleMedium), TitleSmall = C(TitleSmall),
            BodyLarge = C(BodyLarge), BodyMedium = C(BodyMedium), BodySmall = C(BodySmall),
            LabelLarge = C(LabelLarge), LabelMedium = C(LabelMedium), LabelSmall = C(LabelSmall),
        };
    }
}
