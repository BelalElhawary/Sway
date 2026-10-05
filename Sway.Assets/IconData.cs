namespace Sway.Widgets;

/// <summary>A vector icon on a 24x24 grid, described by SVG path data. Secondary is an optional lighter layer, drawn at 30% opacity beneath Path (the two-tone style).</summary>
public sealed record IconData(string Path, bool EvenOdd = false, string? Secondary = null);
