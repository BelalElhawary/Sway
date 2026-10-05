using SkiaSharp;

namespace Sway.Widgets;

static class FloatExt
{
    public static bool IsFinite(this float f) => float.IsFinite(f);
}
