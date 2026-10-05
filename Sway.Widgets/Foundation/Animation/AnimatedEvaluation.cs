using SkiaSharp;

namespace Sway.Widgets;

sealed class AnimatedEvaluation<T>(Animation<float> parent, Animatable<T> animatable) : Animation<T> where T : notnull
{
    bool _hooked;

    public override T Value => animatable.Transform(parent.Value);
    public override AnimationStatus Status => parent.Status;

    // Subscribe to the parent lazily so unused evaluations do not leak listeners.
    public override void AddListener(Action l) { Hook(); base.AddListener(l); }
    void Hook()
    {
        if (_hooked) return;
        _hooked = true;
        parent.AddListener(NotifyListeners);
        parent.AddStatusListener(NotifyStatus);
    }
}
