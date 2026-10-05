namespace Sway.Widgets;

/// <summary>Anything that can wake the host and ask for a frame.</summary>
public sealed class BuildOwner
{
    readonly List<Element> _dirty = new();
    readonly List<Element> _batch = new();
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
            if (_dirty.Count == 1)
            {
                var only = _dirty[0];
                _dirty.Clear();
                if (only.Mounted) only.RebuildIfNeeded();
                continue;
            }

            // Rebuilds below may dirty more elements, so work from a reusable snapshot rather than the live list.
            _dirty.Sort((a, b) => a.Depth.CompareTo(b.Depth));
            _batch.Clear();
            _batch.AddRange(_dirty);
            _dirty.Clear();
            for (int i = 0; i < _batch.Count; i++)
                if (_batch[i].Mounted) _batch[i].RebuildIfNeeded();
        }
        _scheduled = false;
    }
}
