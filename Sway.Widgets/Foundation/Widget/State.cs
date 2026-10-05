namespace Sway.Widgets;

/// <summary>The persistent mutable half of a <see cref="StatefulWidget"/>.</summary>
public abstract class State
{
    internal StatefulElement? Element;
    internal StatefulWidget? WidgetInternal;

    public BuildContext Context => Element ?? throw new InvalidOperationException("State is not mounted.");
    public bool Mounted => Element is { Mounted: true };

    public virtual void InitState() { }
    public virtual void DidUpdateWidget(StatefulWidget oldWidget) { }
    public virtual void DidChangeDependencies() { }
    public virtual void Dispose() { }
    public abstract Widget Build(BuildContext context);

    /// <summary>Runs <paramref name="fn"/> then schedules a rebuild. Safe to call from any thread that owns the UI.</summary>
    public void SetState(Action? fn = null)
    {
        fn?.Invoke();
        if (Element is { Mounted: true } e) e.MarkNeedsBuild();
    }
}

public abstract class State<T> : State where T : StatefulWidget
{
    public T Widget => (T)WidgetInternal!;
    public sealed override void DidUpdateWidget(StatefulWidget oldWidget) => DidUpdateWidget((T)oldWidget);
    public virtual void DidUpdateWidget(T oldWidget) { }
}
