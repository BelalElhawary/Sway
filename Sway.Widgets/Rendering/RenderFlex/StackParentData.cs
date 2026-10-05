namespace Sway.Widgets;

public sealed class StackParentData : BoxParentData
{
    public float? Left, Top, Right, Bottom, Width, Height;
    public bool IsPositioned => Left is not null || Top is not null || Right is not null || Bottom is not null || Width is not null || Height is not null;
}
