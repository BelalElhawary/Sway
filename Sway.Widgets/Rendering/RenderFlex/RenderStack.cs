namespace Sway.Widgets;

/// <summary>Overlaps children; positioned ones are placed by their edges, the rest by <c>alignment</c>.</summary>
public sealed class RenderStack(Alignment alignment, StackFit fit, bool clip) : RenderBoxContainer
{
    Alignment _alignment = alignment;
    StackFit _fit = fit;
    bool _clip = clip;

    public void Update(Alignment alignment, StackFit fit, bool clip)
    {
        if (_alignment == alignment && _fit == fit && _clip == clip) return;
        _alignment = alignment; _fit = fit; _clip = clip;
        MarkNeedsLayout();
    }

    protected override void SetupParentData(RenderObject child)
    {
        if (child.ParentData is not StackParentData) child.ParentData = new StackParentData();
    }

    protected override void PerformLayout()
    {
        bool hasNonPositioned = false;
        float width = Constraints.MinWidth, height = Constraints.MinHeight;
        var nonPositioned = _fit switch
        {
            StackFit.Loose => Constraints.Loosen(),
            StackFit.Expand => BoxConstraints.Tight(Constraints.Biggest),
            _ => Constraints,
        };

        foreach (var c in Children)
        {
            var pd = (StackParentData)c.ParentData!;
            if (pd.IsPositioned) continue;
            hasNonPositioned = true;
            c.Layout(nonPositioned);
            width = Math.Max(width, c.Size.Width);
            height = Math.Max(height, c.Size.Height);
        }

        Size = hasNonPositioned ? Constraints.Constrain(new Size(width, height)) : Constraints.Biggest;

        foreach (var c in Children)
        {
            var pd = (StackParentData)c.ParentData!;
            if (!pd.IsPositioned)
            {
                pd.Offset = _alignment.AlongSize(Size, c.Size);
                continue;
            }

            var cc = new BoxConstraints(0, float.PositiveInfinity, 0, float.PositiveInfinity);
            if (pd.Left is { } l && pd.Right is { } r) cc = cc.Tighten(width: Math.Max(0, Size.Width - l - r));
            else if (pd.Width is { } w) cc = cc.Tighten(width: w);
            if (pd.Top is { } t && pd.Bottom is { } b) cc = cc.Tighten(height: Math.Max(0, Size.Height - t - b));
            else if (pd.Height is { } h) cc = cc.Tighten(height: h);
            c.Layout(cc);

            float x = pd.Left ?? (pd.Right is { } rr ? Size.Width - rr - c.Size.Width : _alignment.AlongSize(Size, c.Size).Dx);
            float y = pd.Top ?? (pd.Bottom is { } bb ? Size.Height - bb - c.Size.Height : _alignment.AlongSize(Size, c.Size).Dy);
            pd.Offset = new Offset(x, y);
        }
    }

    public override void Paint(PaintingContext context, Offset offset)
    {
        void PaintAll(PaintingContext ctx, Offset o)
        {
            foreach (var c in Children) ctx.PaintChild(c, o + OffsetOf(c));
        }
        if (_clip) context.PushClipRect(offset, Size.ToRect(), PaintAll);
        else PaintAll(context, offset);
    }

    protected override bool HitTestChildren(HitTestResult result, Offset position)
    {
        for (int i = Children.Count - 1; i >= 0; i--)
            if (HitTestChild(Children[i], result, position)) return true;
        return false;
    }

    public override float MinIntrinsicWidth(float h) => Children.Count == 0 ? 0 : Children.Max(c => c.MinIntrinsicWidth(h));
    public override float MaxIntrinsicWidth(float h) => Children.Count == 0 ? 0 : Children.Max(c => c.MaxIntrinsicWidth(h));
    public override float MinIntrinsicHeight(float w) => Children.Count == 0 ? 0 : Children.Max(c => c.MinIntrinsicHeight(w));
    public override float MaxIntrinsicHeight(float w) => Children.Count == 0 ? 0 : Children.Max(c => c.MaxIntrinsicHeight(w));
}
