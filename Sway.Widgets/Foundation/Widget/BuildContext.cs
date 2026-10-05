namespace Sway.Widgets;

/// <summary>A handle to a widget's location in the element tree.</summary>
public interface BuildContext
{
    Widget Widget { get; }
    bool Mounted { get; }
    /// <summary>The laid-out size of the nearest render box at or below this element.</summary>
    Size? Size { get; }
    /// <summary>Finds the nearest ancestor inherited widget of type T and registers for rebuilds when it changes.</summary>
    T? DependOn<T>() where T : InheritedWidget;
    /// <summary>Like <see cref="DependOn{T}"/> but does not rebuild when the widget changes.</summary>
    T? Get<T>() where T : InheritedWidget;
    RenderObject? FindRenderObject();
}
