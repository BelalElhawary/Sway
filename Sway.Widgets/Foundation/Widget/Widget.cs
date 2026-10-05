namespace Sway.Widgets;

/// <summary>An immutable description of part of the UI. Widgets are cheap; elements and render objects are not.</summary>
public abstract class Widget(Key? key = null)
{
    public Key? Key { get; } = key;

    public abstract Element CreateElement();

    public static bool CanUpdate(Widget oldWidget, Widget newWidget) =>
        oldWidget.GetType() == newWidget.GetType() && Equals(oldWidget.Key, newWidget.Key);
}
