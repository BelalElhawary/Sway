using SkiaSharp;

namespace Sway.Widgets;

sealed class Reversed(Animation<float> parent) : Animation<float>
{
    bool _hooked;
    public override float Value => 1 - parent.Value;
    public override AnimationStatus Status => parent.Status;
    public override void AddListener(Action l)
    {
        if (!_hooked) { _hooked = true; parent.AddListener(NotifyListeners); }
        base.AddListener(l);
    }
}
