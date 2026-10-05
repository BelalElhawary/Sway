namespace Sway.Widgets;

/// <summary>Anything that can wake the host and ask for a frame.</summary>
public sealed class BuildOwner
{
    readonly List<Element> _dirty = new();
    bool _scheduled;

    public Action? OnBuildScheduled { get; set; }
    public bool HasDirty => _dirty.Count > 0;

    internal void ScheduleBuildFor(Element element)
    {
        _dirty.Add(element);
        if (!_scheduled) { _scheduled = true; OnBuildScheduled?.Invoke(); }
    }

    /// <summary>Rebuilds dirty elements shallowest first; rebuilds that dirty more elements are folded into the same pass.</summary>
    public void BuildScope()
    {
        while (_dirty.Count > 0)
        {
            _dirty.Sort((a, b) => a.Depth.CompareTo(b.Depth));
            var batch = _dirty.ToArray();
            _dirty.Clear();
            foreach (var e in batch)
                if (e.Mounted) e.RebuildIfNeeded();
        }
        _scheduled = false;
    }
}
