using Sway.Widgets;

namespace Sway.Extras.Material3;

public sealed record DropdownMenuItem<T>(T Value, Widget Child, bool Enabled = true);
