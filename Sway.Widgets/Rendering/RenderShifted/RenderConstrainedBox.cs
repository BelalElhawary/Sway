using SkiaSharp;

namespace Sway.Widgets;

/// <summary>Imposes additional constraints on its child (SizedBox, ConstrainedBox).</summary>
public sealed class RenderConstrainedBox(BoxConstraints additional) : RenderProxyBox
{
    BoxConstraints _additional = additional;

    public BoxConstraints AdditionalConstraints
    {
        get => _additional;
        set { if (_additional == value) return; _additional = value; MarkNeedsLayout(); }
    }

    protected override void PerformLayout()
    {
        var effective = _additional.Enforce(Constraints);
        if (Child is { } c)
        {
            c.Layout(effective);
            Size = c.Size;
        }
        else Size = effective.Smallest;
    }

    public override float MinIntrinsicWidth(float h) => _additional.HasTightWidth ? _additional.MinWidth : _additional.Enforce(new(0, float.PositiveInfinity, 0, float.PositiveInfinity)).ConstrainWidth(base.MinIntrinsicWidth(h));
    public override float MaxIntrinsicWidth(float h) => _additional.HasTightWidth ? _additional.MinWidth : _additional.Enforce(new(0, float.PositiveInfinity, 0, float.PositiveInfinity)).ConstrainWidth(base.MaxIntrinsicWidth(h));
    public override float MinIntrinsicHeight(float w) => _additional.HasTightHeight ? _additional.MinHeight : _additional.ConstrainHeight(base.MinIntrinsicHeight(w));
    public override float MaxIntrinsicHeight(float w) => _additional.HasTightHeight ? _additional.MinHeight : _additional.ConstrainHeight(base.MaxIntrinsicHeight(w));
}
