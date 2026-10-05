namespace Sway.Widgets;

/// <summary>
/// Keeps a child alive but out of sight: while offstage it takes no space, paints nothing, ignores the pointer, and its
/// animations are paused (see <see cref="TickerMode"/>).
/// </summary>
public sealed class Offstage(Widget? child = null, bool offstage = true, Key? key = null) : StatelessWidget(key)
{
    public override Widget Build(BuildContext context) =>
        new TickerMode(!offstage, new OffstageBox(child, offstage));
}

sealed class OffstageBox(Widget? child, bool offstage) : SingleChildRenderObjectWidget(child)
{
    public override RenderObject CreateRenderObject(BuildContext context) => new RenderOffstage(offstage);
    public override void UpdateRenderObject(BuildContext context, RenderObject ro) => ((RenderOffstage)ro).Offstage = offstage;
}

sealed class RenderOffstage(bool offstage) : RenderProxyBox
{
    bool _offstage = offstage;

    public bool Offstage
    {
        get => _offstage;
        set
        {
            if (_offstage == value) return;
            _offstage = value;
            MarkNeedsLayout();
        }
    }

    protected override void PerformLayout()
    {
        if (_offstage)
        {
            // Lay the child out anyway so it is ready, with its state intact, when it comes back.
            Child?.Layout(Constraints);
            Size = Constraints.Smallest;
            return;
        }
        base.PerformLayout();
    }

    public override void Paint(PaintingContext context, Offset offset)
    {
        if (!_offstage) base.Paint(context, offset);
    }

    public override bool HitTest(HitTestResult result, Offset position) => !_offstage && base.HitTest(result, position);

    public override float? GetDistanceToBaseline() => _offstage ? null : base.GetDistanceToBaseline();
}
