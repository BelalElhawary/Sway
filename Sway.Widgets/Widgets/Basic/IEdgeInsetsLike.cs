using SkiaSharp;

namespace Sway.Widgets;

public interface IEdgeInsetsLike { EdgeInsets Resolve(TextDirection d); }
