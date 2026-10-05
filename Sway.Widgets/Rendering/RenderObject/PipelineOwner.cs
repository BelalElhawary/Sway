using SkiaSharp;

namespace Sway.Widgets;

/// <summary>Owns the render tree's dirty state and asks the host for a frame when something changes.</summary>
public sealed class PipelineOwner
{
    readonly HashSet<RenderObject> _layoutRoots = new();
    bool _framePending;

    public Action? OnNeedsFrame { get; set; }
    public bool NeedsLayout => _layoutRoots.Count > 0;

    /// <summary>True when a layout pass ran since the flag was last cleared (widgets may have moved under the pointer).</summary>
    internal bool DidLayout { get; set; }

    internal void RequestLayout(RenderObject root)
    {
        _layoutRoots.Add(root);
        RequestFrame();
    }

    public void RequestFrame()
    {
        if (_framePending) return;
        _framePending = true;
        OnNeedsFrame?.Invoke();
    }

    public void FlushLayout()
    {
        // A layout pass can dirty more roots (e.g. LayoutBuilder), so loop until stable.
        for (int guard = 0; _layoutRoots.Count > 0 && guard < 16; guard++)
        {
            DidLayout = true;
            var roots = _layoutRoots.ToArray();
            _layoutRoots.Clear();
            foreach (var r in roots) r.LayoutAsRoot();
        }
    }

    internal void FrameDone() => _framePending = false;
}
