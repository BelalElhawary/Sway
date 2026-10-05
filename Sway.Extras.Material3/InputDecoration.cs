using SkiaSharp;

using Sway.Widgets;

namespace Sway.Extras.Material3;

public sealed record InputDecoration(
    string? HintText = null,
    string? LabelText = null,
    string? HelperText = null,
    string? ErrorText = null,
    Widget? Prefix = null,
    Widget? Suffix = null,
    bool Filled = false,
    SKColor? FillColor = null,
    EdgeInsets? ContentPadding = null);
