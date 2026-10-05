using SkiaSharp;

namespace Sway.Widgets;

/// <summary>Applies a <see cref="Curve"/> to another animation (with an optional different curve when reversing).</summary>
public sealed class CurvedAnimation : Animation<float>
{
    readonly Animation<float> _parent;
    readonly Action _onParent;
    readonly Action<AnimationStatus> _onStatus;

    public CurvedAnimation(Animation<float> parent, Curve curve, Curve? reverseCurve = null)
    {
        _parent = parent;
        Curve = curve;
        ReverseCurve = reverseCurve;
        _onParent = NotifyListeners;
        _onStatus = NotifyStatus;
        parent.AddListener(_onParent);
        parent.AddStatusListener(_onStatus);
    }

    public Curve Curve { get; set; }
    public Curve? ReverseCurve { get; set; }
    public override AnimationStatus Status => _parent.Status;

    public override float Value
    {
        get
        {
            bool reversing = ReverseCurve is not null && _parent.Status is AnimationStatus.Reverse or AnimationStatus.Dismissed;
            return (reversing ? ReverseCurve! : Curve).Transform(_parent.Value);
        }
    }

    public void Dispose()
    {
        _parent.RemoveListener(_onParent);
        _parent.RemoveStatusListener(_onStatus);
    }
}
