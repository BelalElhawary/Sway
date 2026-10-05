namespace Sway.Widgets;

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
