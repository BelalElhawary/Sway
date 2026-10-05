using SkiaSharp;

namespace Sway.Widgets;

/// <summary>Flows children along an axis and starts a new run when they no longer fit.</summary>
public sealed class RenderWrap : RenderBoxContainer
{
    Axis _direction;
    WrapAlignment _alignment, _runAlignment;
    WrapCrossAlignment _crossAlignment;
    float _spacing, _runSpacing;
    TextDirection _textDirection;
    VerticalDirection _verticalDirection;

    public RenderWrap(Axis direction, WrapAlignment alignment, float spacing, WrapAlignment runAlignment, float runSpacing,
        WrapCrossAlignment crossAlignment, TextDirection textDirection, VerticalDirection verticalDirection)
    {
        _direction = direction; _alignment = alignment; _spacing = spacing; _runAlignment = runAlignment; _runSpacing = runSpacing;
        _crossAlignment = crossAlignment; _textDirection = textDirection; _verticalDirection = verticalDirection;
    }

    public void Update(Axis direction, WrapAlignment alignment, float spacing, WrapAlignment runAlignment, float runSpacing,
        WrapCrossAlignment crossAlignment, TextDirection textDirection, VerticalDirection verticalDirection)
    {
        _direction = direction; _alignment = alignment; _spacing = spacing; _runAlignment = runAlignment; _runSpacing = runSpacing;
        _crossAlignment = crossAlignment; _textDirection = textDirection; _verticalDirection = verticalDirection;
        MarkNeedsLayout();
    }

    bool Horizontal => _direction == Axis.Horizontal;
    float Main(Size s) => Horizontal ? s.Width : s.Height;
    float Cross(Size s) => Horizontal ? s.Height : s.Width;

    sealed class Run
    {
        public readonly List<RenderBox> Items = new();
        public float Main, Cross;
    }

    static (float leading, float between) Distribute(WrapAlignment a, float free, int count)
    {
        free = Math.Max(0, free);
        return a switch
        {
            WrapAlignment.End => (free, 0),
            WrapAlignment.Center => (free / 2, 0),
            WrapAlignment.SpaceBetween => (0, count > 1 ? free / (count - 1) : 0),
            WrapAlignment.SpaceAround => (count > 0 ? free / count / 2 : 0, count > 0 ? free / count : 0),
            WrapAlignment.SpaceEvenly => (free / (count + 1), free / (count + 1)),
            _ => (0, 0),
        };
    }

    protected override void PerformLayout()
    {
        float maxMain = Horizontal ? Constraints.MaxWidth : Constraints.MaxHeight;
        var childC = Horizontal ? new BoxConstraints(0, maxMain, 0, float.PositiveInfinity) : new BoxConstraints(0, float.PositiveInfinity, 0, maxMain);

        var runs = new List<Run>();
        var run = new Run();
        foreach (var c in Children)
        {
            c.Layout(childC);
            float m = Main(c.Size);
            float needed = run.Items.Count == 0 ? m : run.Main + _spacing + m;
            if (run.Items.Count > 0 && needed > maxMain)
            {
                runs.Add(run);
                run = new Run();
                needed = m;
            }
            run.Items.Add(c);
            run.Main = needed;
            run.Cross = Math.Max(run.Cross, Cross(c.Size));
        }
        if (run.Items.Count > 0) runs.Add(run);

        float contentMain = runs.Count == 0 ? 0 : runs.Max(r => r.Main);
        float contentCross = runs.Sum(r => r.Cross) + _runSpacing * Math.Max(0, runs.Count - 1);
        Size = Constraints.Constrain(Horizontal ? new Size(contentMain, contentCross) : new Size(contentCross, contentMain));

        float containerMain = Main(Size), containerCross = Cross(Size);
        bool flipMain = Horizontal ? _textDirection == TextDirection.Rtl : _verticalDirection == VerticalDirection.Up;
        bool flipCross = Horizontal ? _verticalDirection == VerticalDirection.Up : _textDirection == TextDirection.Rtl;

        var (runLead, runBetween) = Distribute(_runAlignment, containerCross - contentCross, runs.Count);
        float crossPos = runLead;
        foreach (var r in runs)
        {
            var (lead, between) = Distribute(_alignment, containerMain - r.Main, r.Items.Count);
            float mainPos = lead;
            foreach (var c in r.Items)
            {
                float cm = Main(c.Size), cc = Cross(c.Size);
                float off = _crossAlignment switch
                {
                    WrapCrossAlignment.End => r.Cross - cc,
                    WrapCrossAlignment.Center => (r.Cross - cc) / 2,
                    _ => 0,
                };
                float x = flipMain ? containerMain - mainPos - cm : mainPos;
                float y = flipCross ? containerCross - (crossPos + off) - cc : crossPos + off;
                SetOffset(c, Horizontal ? new Offset(x, y) : new Offset(y, x));
                mainPos += cm + _spacing + between;
            }
            crossPos += r.Cross + _runSpacing + runBetween;
        }
    }

    public override void Paint(PaintingContext context, Offset offset)
    {
        foreach (var c in Children) context.PaintChild(c, offset + OffsetOf(c));
    }

    protected override bool HitTestChildren(HitTestResult result, Offset position)
    {
        for (int i = Children.Count - 1; i >= 0; i--)
            if (HitTestChild(Children[i], result, position)) return true;
        return false;
    }

    public override float MaxIntrinsicWidth(float h) => Horizontal ? Children.Sum(c => c.MaxIntrinsicWidth(h)) + _spacing * Math.Max(0, Children.Count - 1) : Children.Select(c => c.MaxIntrinsicWidth(h)).DefaultIfEmpty(0).Max();
    public override float MinIntrinsicWidth(float h) => Children.Select(c => c.MinIntrinsicWidth(h)).DefaultIfEmpty(0).Max();
}
