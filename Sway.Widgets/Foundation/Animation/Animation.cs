using SkiaSharp;

namespace Sway.Widgets;

/// <summary>A value that changes over time and notifies listeners.</summary>
public abstract class Animation<T> : IListenable
{
    event Action? _changed;
    event Action<AnimationStatus>? _statusChanged;

    public abstract T Value { get; }
    public abstract AnimationStatus Status { get; }

    public virtual void AddListener(Action listener) => _changed += listener;
    public void RemoveListener(Action listener) => _changed -= listener;
    public void AddStatusListener(Action<AnimationStatus> listener) => _statusChanged += listener;
    public void RemoveStatusListener(Action<AnimationStatus> listener) => _statusChanged -= listener;

    protected void NotifyListeners() => _changed?.Invoke();
    protected void NotifyStatus(AnimationStatus s) => _statusChanged?.Invoke(s);

    public bool IsCompleted => Status == AnimationStatus.Completed;
    public bool IsDismissed => Status == AnimationStatus.Dismissed;
    public bool IsAnimating => Status is AnimationStatus.Forward or AnimationStatus.Reverse;

    public Animation<U> Drive<U>(Animatable<U> child) where U : notnull => child.Animate(AsFloat());

    Animation<float> AsFloat() => this as Animation<float> ?? throw new InvalidOperationException("Only Animation<float> can drive a tween.");
}
