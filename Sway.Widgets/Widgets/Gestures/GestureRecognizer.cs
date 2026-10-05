namespace Sway.Widgets;

public abstract class GestureRecognizer : IGestureArenaMember
{
    protected static GestureBinding Gestures => WidgetsBinding.Instance.Gestures;

    public abstract void AddPointer(PointerEvent down);
    public abstract void AcceptGesture(int pointer);
    public abstract void RejectGesture(int pointer);
    public virtual void Dispose() { }
}
