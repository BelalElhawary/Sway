using SkiaSharp;

namespace Sway.Widgets;

public readonly record struct InteractionState(bool Hover, bool Pressed, bool Focused, bool FocusVisible);
