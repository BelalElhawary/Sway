using SkiaSharp;

namespace Sway.Widgets;

public sealed class RichText(TextSpan text, TextStyle style, TextAlign textAlign, TextDirection textDirection,
    bool softWrap, TextOverflow overflow, int? maxLines, Key? key = null) : LeafRenderObjectWidget(key)
{
    public override RenderObject CreateRenderObject(BuildContext context) =>
        new RenderParagraph(text, style, textAlign, textDirection, softWrap, overflow, maxLines);

    public override void UpdateRenderObject(BuildContext context, RenderObject ro) =>
        ((RenderParagraph)ro).Update(text, style, textAlign, textDirection, softWrap, overflow, maxLines);
}
