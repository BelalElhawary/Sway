using SkiaSharp;

using Sway.Widgets;

namespace Sway.Extras.Material3;

/// <summary>Helpers for Material 3 interaction "state layers": a translucent on-colour overlay for hover, focus and press.</summary>
public static class StateLayer
{
    public static float Opacity(InteractionState s) => s.Pressed ? 0.10f : s.FocusVisible ? 0.10f : s.Hover ? 0.08f : 0f;

    /// <summary>Composites <paramref name="layer"/> at <paramref name="opacity"/> over <paramref name="baseColor"/>.</summary>
    public static SKColor Blend(SKColor baseColor, SKColor layer, float opacity)
    {
        if (opacity <= 0) return baseColor;
        // A transparent base keeps its transparency so a text button only shows the tint.
        if (baseColor.Alpha == 0) return layer.WithOpacity(opacity);
        return Lerps.Color(baseColor, layer.WithAlpha(baseColor.Alpha), opacity);
    }
}
