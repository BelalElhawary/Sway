using SkiaSharp;

namespace Sway.Widgets;

public sealed record PhysicalInsets(EdgeInsets Insets) : IEdgeInsetsLike { public EdgeInsets Resolve(TextDirection d) => Insets; }
