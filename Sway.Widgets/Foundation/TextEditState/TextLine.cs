using System.Globalization;

namespace Sway.Widgets;

public readonly record struct TextLine(int Start, int End, bool HardBreak);
