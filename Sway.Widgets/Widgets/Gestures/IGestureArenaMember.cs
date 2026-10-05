namespace Sway.Widgets;

public interface IGestureArenaMember
{
    void AcceptGesture(int pointer);
    void RejectGesture(int pointer);
}
