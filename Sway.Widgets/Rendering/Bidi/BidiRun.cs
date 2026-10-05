using System.Text;

namespace Sway.Widgets;

public readonly record struct BidiRun(int Start, int Length, TextDirection Direction, int Level);
