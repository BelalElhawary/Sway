using SkiaSharp;

namespace Sway.Widgets;

/// <summary>Root of the render tree; sized to the window.</summary>
public sealed class RenderView : RenderObjectWithChild
{
    public Size WindowSize { get; private set; }

    public void Configure(Size size)
    {
        if (WindowSize == size) return;
        WindowSize = size;
        MarkNeedsLayout();
    }

    internal override void LayoutAsRoot()
    {
        ClearNeedsLayout();
        Child?.Layout(BoxConstraints.Tight(WindowSize));
    }

    public override void Paint(PaintingContext context, Offset offset)
    {
        if (Child is not null) context.PaintChild(Child, offset);
    }

    public HitTestResult HitTestAt(Offset position)
    {
        var result = new HitTestResult();
        Child?.HitTest(result, position);
        return result;
    }
}
