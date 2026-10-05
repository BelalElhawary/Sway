using SkiaSharp;

namespace Sway.Widgets;

public sealed record DirectionalInsets(EdgeInsetsDirectional Insets) : IEdgeInsetsLike { public EdgeInsets Resolve(TextDirection d) => Insets.Resolve(d); }
