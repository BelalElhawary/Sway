using SkiaSharp;

namespace Sway.Widgets;

/// <summary>
/// Glues the three trees to a host: owns the build and pipeline owners, runs frames, routes pointer input,
/// and schedules frame callbacks and timers on a clock the host (or a headless test) can drive.
/// </summary>
public sealed class WidgetsBinding
{
    public static WidgetsBinding Instance { get; private set; } = null!;

    readonly System.Diagnostics.Stopwatch _stopwatch = System.Diagnostics.Stopwatch.StartNew();
    List<Action<TimeSpan>> _frameCallbacks = new();
    List<Action<TimeSpan>> _spareCallbacks = new();
    readonly List<(TimeSpan due, Action action)> _timers = new();
    readonly List<(TimeSpan due, Action action)> _dueTimers = new();
    TimeSpan _earliestDue = TimeSpan.MaxValue; // earliest due time among _timers, so idle polling is O(1)
    readonly System.Collections.Concurrent.ConcurrentQueue<Action> _posted = new();
    TimeSpan? _manualClock;
    bool _frameRequested = true;

    public BuildOwner BuildOwner { get; } = new();
    public PipelineOwner PipelineOwner { get; } = new();
    public GestureBinding Gestures { get; }
    public FocusManager Focus { get; } = new();

    /// <summary>Clipboard access supplied by the host (the headless host keeps it in memory).</summary>
    public Func<string?> GetClipboard { get; set; } = () => _memoryClipboard;
    public Action<string> SetClipboard { get; set; } = text => _memoryClipboard = text;
    static string? _memoryClipboard;

    public bool Ctrl, Shift, Alt, Meta;

    Brightness _platformBrightness = Brightness.Light;

    /// <summary>The operating system's light/dark preference; the host sets it. A design system's app widget can follow it.</summary>
    public Brightness PlatformBrightness
    {
        get => _platformBrightness;
        set
        {
            if (_platformBrightness == value) return;
            _platformBrightness = value;
            PlatformBrightnessChanged?.Invoke();
            RequestFrame();
        }
    }

    public event Action? PlatformBrightnessChanged;
    public RenderView RenderView { get; private set; } = null!;
    Element? _root;

    /// <summary>Set by the host; called whenever something needs another frame (from any thread that owns the UI).</summary>
    public Action? OnFrameRequested { get; set; }

    public WidgetsBinding()
    {
        Instance = this;
        Gestures = new GestureBinding(this);
        BuildOwner.OnBuildScheduled = RequestFrame;
        PipelineOwner.OnNeedsFrame = RequestFrame;
    }

    public void RequestFrame()
    {
        _frameRequested = true;
        OnFrameRequested?.Invoke();
    }

    // ---- clock ----

    public TimeSpan Now => _manualClock ?? _stopwatch.Elapsed;

    /// <summary>Freezes real time; <see cref="AdvanceClock"/> becomes the only thing that moves animations and timers.</summary>
    public void UseManualClock() => _manualClock = _stopwatch.Elapsed;

    public void AdvanceClock(TimeSpan by)
    {
        if (_manualClock is null) throw new InvalidOperationException("Call UseManualClock first.");
        // Step in frame-sized increments so animations see intermediate frames.
        var target = _manualClock.Value + by;
        var step = TimeSpan.FromMilliseconds(16);
        while (_manualClock < target)
        {
            _manualClock = TimeSpan.FromTicks(Math.Min(target.Ticks, (_manualClock.Value + step).Ticks));
            RunScheduled();
        }
        RequestFrame();
    }

    /// <summary>Runs <paramref name="callback"/> once at the start of the next frame, with the frame time.</summary>
    public void ScheduleFrameCallback(Action<TimeSpan> callback)
    {
        _frameCallbacks.Add(callback);
        RequestFrame();
    }

    public void ScheduleTimer(TimeSpan delay, Action action)
    {
        var due = Now + delay;
        _timers.Add((due, action));
        if (due < _earliestDue) _earliestDue = due;
        RequestFrame();
    }

    /// <summary>
    /// Queues <paramref name="action"/> to run on the UI thread at the start of the next frame. This is the one
    /// member that is safe to call from any thread: use it to deliver results from dialogs, network and media threads.
    /// </summary>
    public void Post(Action action)
    {
        _posted.Enqueue(action);
        RequestFrame();
    }

    public bool CancelTimer(Action action)
    {
        bool removed = _timers.RemoveAll(t => t.action == action) > 0;
        if (removed) RecomputeEarliestDue();
        return removed;
    }

    void RecomputeEarliestDue()
    {
        var earliest = TimeSpan.MaxValue;
        foreach (var t in _timers)
            if (t.due < earliest) earliest = t.due;
        _earliestDue = earliest;
    }

    void RunScheduled()
    {
        while (_posted.TryDequeue(out var posted)) posted();

        var now = Now;
        if (_earliestDue <= now)
        {
            // Due timers run oldest first (ties in the order they were scheduled).
            _dueTimers.Clear();
            for (int i = 0; i < _timers.Count; i++)
                if (_timers[i].due <= now) _dueTimers.Add(_timers[i]);
            _timers.RemoveAll(t => t.due <= now);
            RecomputeEarliestDue();
            InsertionSortByDue(_dueTimers);
            // An action may schedule more timers; _dueTimers is only touched here, so iterate a copy-free snapshot by index.
            int count = _dueTimers.Count;
            for (int i = 0; i < count; i++) _dueTimers[i].action();
        }

        if (_frameCallbacks.Count > 0)
        {
            // Callbacks scheduled while these run belong to the next frame: swap in an empty list instead of copying.
            var callbacks = _frameCallbacks;
            _frameCallbacks = _spareCallbacks;
            try { for (int i = 0; i < callbacks.Count; i++) callbacks[i](now); }
            finally
            {
                // Even if a callback throws, the rest of this frame's callbacks are dropped and the two lists stay distinct.
                callbacks.Clear();
                _spareCallbacks = callbacks;
            }
        }
    }

    static void InsertionSortByDue(List<(TimeSpan due, Action action)> timers)
    {
        // Stable, and the list is almost always tiny and already ordered.
        for (int i = 1; i < timers.Count; i++)
        {
            var item = timers[i];
            int j = i - 1;
            while (j >= 0 && timers[j].due > item.due) { timers[j + 1] = timers[j]; j--; }
            timers[j + 1] = item;
        }
    }

    // ---- keyboard ----

    public bool KeyDown(string key, string code, bool repeat = false) => HandleKey(key, code, true, repeat);
    public bool KeyUp(string key, string code) => HandleKey(key, code, false, false);

    bool HandleKey(string key, string code, bool down, bool repeat)
    {
        bool handled = Focus.HandleKey(new KeyEvent(key, code, down, Ctrl, Shift, Alt, Meta, repeat));
        RequestFrame();
        return handled;
    }

    public void TextInput(string text)
    {
        Focus.HandleText(text);
        RequestFrame();
    }

    // ---- back navigation ----

    readonly List<Func<bool>> _backHandlers = new();

    /// <summary>Registers a handler for the platform's back action; the most recently added one is asked first. Return true to consume it.</summary>
    public void AddBackHandler(Func<bool> handler) => _backHandlers.Add(handler);

    public void RemoveBackHandler(Func<bool> handler) => _backHandlers.Remove(handler);

    /// <summary>
    /// The host calls this for its back action (Android back button or gesture, Alt+Left or the mouse's back button on desktop).
    /// Returns false when nothing consumed it, and the host should apply its own default (finish the activity).
    /// </summary>
    public bool HandleBack()
    {
        for (int i = _backHandlers.Count - 1; i >= 0; i--)
        {
            if (!_backHandlers[i]()) continue;
            RequestFrame();
            return true;
        }
        return false;
    }

    // ---- app root ----

    public void AttachRoot(Widget app)
    {
        var root = new RootWidget(new Directionality(TextDirection.Ltr, new Overlay(app))).CreateElement();
        root.Owner = BuildOwner;
        root.Mount(null);
        _root = root;
        RenderView = (RenderView)root.FindRenderObject()!;
        RenderView.Attach(PipelineOwner);
    }

    /// <summary>Replaces the root widget (hot reload / headless re-render).</summary>
    public void ReassembleRoot(Widget app) => _root?.Update(new RootWidget(new Directionality(TextDirection.Ltr, new Overlay(app))));

    // ---- frames ----

    /// <summary>True when drawing now would produce a different image than the last frame.</summary>
    public bool NeedsFrame(float width, float height)
    {
        if (_frameRequested || _frameCallbacks.Count > 0 || !_posted.IsEmpty) return true;
        if (RenderView is not null && RenderView.WindowSize != new Size(width, height)) return true;
        return _earliestDue <= Now;
    }

    /// <summary>Debug mode: draws the frame rate over the app. Toggle at runtime with F3 in a windowed host.</summary>
    public bool ShowFps
    {
        get => _fps is not null;
        set
        {
            if (value == ShowFps) return;
            _fps = value ? new FpsOverlay() : null;
            RequestFrame();
        }
    }

    FpsOverlay? _fps;

    public void DrawFrame(SKCanvas canvas, float width, float height)
    {
        _fps?.BeginFrame();
        DrawFrameCore(canvas, width, height);
        if (_fps is null) return;
        _fps.EndFrame();
        canvas.Save();
        _fps.Paint(canvas);
        canvas.Restore();
    }

    void DrawFrameCore(SKCanvas canvas, float width, float height)
    {
        _frameRequested = false;
        RunScheduled();
        RenderView.Configure(new Size(width, height));
        BuildOwner.BuildScope();
        PipelineOwner.FlushLayout();
        // Layout may have scheduled more builds (LayoutBuilder); settle them before painting.
        for (int i = 0; i < 4 && BuildOwner.HasDirty; i++)
        {
            BuildOwner.BuildScope();
            PipelineOwner.FlushLayout();
        }

        canvas.Clear(SKColors.White);
        RenderView.Paint(new PaintingContext(canvas), Offset.Zero);
        PipelineOwner.FrameDone();
        Gestures.AfterFrame();
    }

    /// <summary>Renders one frame to an image without a window.</summary>
    public SKBitmap RenderToBitmap(int width, int height)
    {
        var bitmap = new SKBitmap(width, height);
        using var canvas = new SKCanvas(bitmap);
        DrawFrame(canvas, width, height);
        return bitmap;
    }
}
