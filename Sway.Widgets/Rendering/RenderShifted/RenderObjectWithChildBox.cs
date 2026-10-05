using SkiaSharp;

namespace Sway.Widgets;

/// <summary>A <see cref="RenderBox"/> with one optional box child (the box-typed counterpart of <see cref="RenderObjectWithChild"/>).</summary>
public abstract class RenderObjectWithChildBox : RenderBox, IRenderChildHolder
{
    RenderBox? _child;

    public RenderBox? Child
    {
        get => _child;
        set
        {
            if (ReferenceEquals(_child, value)) return;
            if (_child is not null) DropChild(_child);
            _child = value;
            if (_child is not null) AdoptChild(_child);
        }
    }

    public override void VisitChildren(Action<RenderObject> visitor)
    {
        if (_child is not null) visitor(_child);
    }
}
