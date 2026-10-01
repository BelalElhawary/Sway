namespace Sway.Core.Platform;

/// <summary>How long each phase of the last frame took, in milliseconds.</summary>
public sealed class FrameStats
{
    public double StyleMs { get; set; }
    public double AnimateMs { get; set; }
    public double LayoutMs { get; set; }
    public double ListMs { get; set; }
    public double PaintMs { get; set; }
    public double TotalMs { get; set; }

    public bool StyleRan { get; set; }
    public bool LayoutRan { get; set; }
    public int OpCount { get; set; }
}
