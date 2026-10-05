namespace Sway.Widgets;

public readonly record struct DragDetails(Offset GlobalPosition, Offset LocalPosition, Offset Delta, Offset Velocity = default);
