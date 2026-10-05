using SkiaSharp;

namespace Sway.Widgets;

public sealed class Transform(SKMatrix matrix, Widget? child = null, Alignment? origin = null, Key? key = null) : SingleChildRenderObjectWidget(child, key)
{
    public static Transform Rotate(float radians, Widget? child = null, Alignment? origin = null) =>
        new(SKMatrix.CreateRotation(radians), child, origin);
    public static Transform Scale(float scale, Widget? child = null, Alignment? origin = null) =>
        new(SKMatrix.CreateScale(scale, scale), child, origin);
    public static Transform Translate(Offset offset, Widget? child = null) =>
        new(SKMatrix.CreateTranslation(offset.Dx, offset.Dy), child);

    public override RenderObject CreateRenderObject(BuildContext context) => new RenderTransform(matrix, origin);
    public override void UpdateRenderObject(BuildContext context, RenderObject ro) => ((RenderTransform)ro).Update(matrix, origin);
}
