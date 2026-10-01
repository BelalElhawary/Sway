using System.Diagnostics;
using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SkiaSharp;
using Sway.Core.Dom;
using Sway.Core.Layout;
using Sway.Core.Rendering;
using Sway.Core.Styling;

namespace Sway.Core.Platform;

/// <summary>
/// Everything except the OS window: document, renderer, style, layout, paint and input routing.
/// Keeping it window-free lets the same pipeline render to a PNG for testing.
/// </summary>
public sealed class UiHost : IDisposable
{
    const float WheelPixelsPerNotch = 80;
    const float ArrowKeyScroll = 40;
    const long MultiClickMs = 500;
    const float MultiClickSlop = 4;
    const long CaretBlinkMs = 530;

    // An event ready to be delivered once the document lock is released.
    readonly record struct PendingEvent(List<ulong> Handlers, EventArgs Args);

    readonly ServiceProvider _services;
    readonly StyleResolver _styles = new();
    readonly LayoutEngine _layout = new();
    readonly Painter _painter = new();
    readonly ILogger _logger;
    readonly Stopwatch _clock = Stopwatch.StartNew();
    double _manualClockMs = -1;

    IReadOnlyList<PaintOp> _ops = Array.Empty<PaintOp>();
    ElementNode? _hovered;
    ElementNode? _pressed;
    ElementNode? _focused;
    float _lastWidth, _lastHeight;
    float _pointerX, _pointerY;

    // Text editing.
    string _changeBaseline = "";
    long _lastEditTick;
    ElementNode? _dragEdit;
    Affine _dragInverse = Affine.Identity;
    int _clickCount;
    long _lastClickTick = -MultiClickMs * 2;
    float _lastClickX, _lastClickY;

    // Dragging a scrollbar thumb.
    ElementNode? _dragScrollbar;
    ScrollbarPart _dragScrollbarPart;
    Affine _dragScrollbarInverse = Affine.Identity;
    float _dragScrollbarOriginLocal;
    float _dragScrollbarOriginScroll;
    float _dragScrollbarRange;
    float _dragScrollbarMax;

    // Open select dropdown.
    ElementNode? _openSelect;
    int _popupHover = -1;
    float _popupScroll;
    bool _popupPressed;
    bool _swallowPointer;

    public UiHost(IEnumerable<string> stylesheetPaths)
    {
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddSimpleConsole().SetMinimumLevel(LogLevel.Information));
        _services = services.BuildServiceProvider();

        var loggerFactory = _services.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger<UiHost>();
        Document = new Document();
        Renderer = new SkiaRenderer(Document, _services, loggerFactory);

        UseMemoryClipboard();
        Document.StyleInvalidation = _styles;
        foreach (var path in stylesheetPaths) AddStylesheet(path);
    }

    public Document Document { get; }
    public SkiaRenderer Renderer { get; }

    /// <summary>Modifier keys as of the last keyboard event; stamped onto pointer and key events.</summary>
    public bool Ctrl { get; set; }
    public bool Shift { get; set; }
    public bool Alt { get; set; }
    public bool Meta { get; set; }

    /// <summary>Clipboard access, supplied by the window layer. Headless runs use an in-memory clipboard.</summary>
    public Func<string?> GetClipboard { get; set; } = null!;
    public Action<string> SetClipboard { get; set; } = null!;

    string _memoryClipboard = "";

    /// <summary>The time transitions and animations see. Real time unless a test has taken manual control.</summary>
    public double NowMs => _manualClockMs >= 0 ? _manualClockMs : _clock.Elapsed.TotalMilliseconds;

    /// <summary>Freezes the animation clock so tests can step it deterministically with <see cref="AdvanceClock"/>.</summary>
    public void UseManualClock() => _manualClockMs = _clock.Elapsed.TotalMilliseconds;

    public void AdvanceClock(double ms) { if (_manualClockMs >= 0) _manualClockMs += ms; }

    public bool HasActiveAnimations => _styles.Animator.HasActive;

    /// <summary>The CSS cursor keyword under the pointer.</summary>
    public string Cursor => _hovered?.Style.Cursor ?? "auto";

    public ElementNode? FocusedElement => _focused;

    public ColorScheme ColorScheme
    {
        get => _styles.ColorScheme;
        set { lock (Document.SyncRoot) { _styles.ColorScheme = value; Document.InvalidateStyle(); } }
    }

    // Defaults for the clipboard hooks must be set after construction because they capture instance state.
    public void UseMemoryClipboard()
    {
        GetClipboard = () => _memoryClipboard;
        SetClipboard = text => _memoryClipboard = text;
    }

    public void AddStylesheet(string path)
    {
        string full = Path.IsPathRooted(path) ? path : Path.Combine(AppContext.BaseDirectory, path);
        AddStylesheetText(File.ReadAllText(full), Path.GetDirectoryName(full)!);
    }

    /// <summary>Adds CSS from a string. Relative font URLs resolve against <paramref name="baseDirectory"/>.</summary>
    public void AddStylesheetText(string css, string? baseDirectory = null)
    {
        var sheet = CssParser.Parse(css);
        RegisterFontFaces(sheet, baseDirectory ?? AppContext.BaseDirectory);
        lock (Document.SyncRoot)
        {
            _styles.AddStyleSheet(sheet);
            Document.InvalidateStyle();
            Document.InvalidateAllLayout(); // new fonts change text metrics without changing any style value
        }
    }

    // Each @font-face lists sources in preference order; the first one that loads is used.
    void RegisterFontFaces(StyleSheet sheet, string baseDirectory)
    {
        foreach (var face in sheet.FontFaces)
        {
            SKTypeface? typeface = null;
            foreach (var source in face.Sources)
            {
                typeface = source.IsLocal ? LoadLocalFont(source.Value) : LoadFontFile(source.Value, baseDirectory);
                if (typeface is not null) break;
            }

            if (typeface is null) _logger.LogWarning("@font-face {Family}: no usable source", face.Family);
            else FontCache.RegisterFace(face.Family, face.Weight, face.Italic, typeface);
        }
    }

    static SKTypeface? LoadLocalFont(string name)
    {
        var typeface = SKTypeface.FromFamilyName(name);
        // FromFamilyName silently substitutes a default face when the family is not installed.
        return typeface is not null && typeface.FamilyName.Equals(name, StringComparison.OrdinalIgnoreCase) ? typeface : null;
    }

    SKTypeface? LoadFontFile(string url, string baseDirectory)
    {
        string path = url.StartsWith("file:///", StringComparison.OrdinalIgnoreCase) ? url[8..] : url;
        path = Uri.UnescapeDataString(path);
        if (!Path.IsPathRooted(path)) path = Path.Combine(baseDirectory, path);

        string extension = Path.GetExtension(path).ToLowerInvariant();
        if (extension is ".woff" or ".woff2")
        {
            _logger.LogWarning("Font {Path}: WOFF is not supported, use TTF or OTF", path);
            return null;
        }
        if (!File.Exists(path)) return null;
        return SKTypeface.FromFile(path);
    }

    public Task MountAsync<TComponent>() where TComponent : IComponent => Renderer.AddRootComponentAsync<TComponent>();

    bool _forceFrame = true;
    long _lastBlinkPhase = -1;

    /// <summary>Ask for one more frame, for example after the window was uncovered.</summary>
    public void RequestFrame() => _forceFrame = true;

    /// <summary>
    /// Whether anything visible could have changed since the last frame. The window loop skips rendering
    /// (and sleeps) when this is false, so an idle application uses almost no CPU or GPU.
    /// </summary>
    public bool NeedsFrame(float width, float height)
    {
        lock (Document.SyncRoot)
        {
            if (_forceFrame || Document.StyleDirty || Document.LayoutDirty || _styles.Animator.HasActive) return true;
            if (width != _lastWidth || height != _lastHeight) return true;

            // A blinking caret is the one thing that changes on its own.
            if (_focused is { } f && Controls.IsTextEditable(f) && !f.IsDisabled)
                return (_clock.ElapsedMilliseconds - _lastEditTick) / CaretBlinkMs != _lastBlinkPhase;
            return false;
        }
    }

    /// <summary>Timings of the most recent frame, for profiling and the benchmark runner.</summary>
    public FrameStats LastFrame { get; } = new();

    static double Ms(long from, long to) => (to - from) * 1000.0 / Stopwatch.Frequency;

    /// <summary>Restyles and relays out if anything changed, then paints one frame.</summary>
    public void Frame(SKCanvas canvas, float width, float height)
    {
        lock (Document.SyncRoot)
        {
            long t0 = Stopwatch.GetTimestamp();
            if (width != _lastWidth || height != _lastHeight)
            {
                _lastWidth = width;
                _lastHeight = height;
                _styles.Context = new StyleContext(width, height);
                Document.InvalidateStyle(); // media queries and viewport units depend on the size
                Document.LayoutDirty = true;
            }

            double now = NowMs;
            _styles.NowMs = now;
            LastFrame.StyleRan = Document.StyleDirty;
            if (Document.StyleDirty) _styles.Resolve(Document);
            long t1 = Stopwatch.GetTimestamp();
            _styles.Animator.Tick(Document, now);
            long t2 = Stopwatch.GetTimestamp();
            LastFrame.LayoutRan = Document.LayoutDirty;
            if (Document.LayoutDirty) _layout.Layout(Document, width, height);
            long t3 = Stopwatch.GetTimestamp();

            DropDetachedState();
            _ops = DisplayListBuilder.Build(Document, _openSelect);
            long t4 = Stopwatch.GetTimestamp();

            _forceFrame = false;
            _lastBlinkPhase = (_clock.ElapsedMilliseconds - _lastEditTick) / CaretBlinkMs;

            var context = new PaintContext
            {
                Focused = _focused,
                CaretOn = (_clock.ElapsedMilliseconds - _lastEditTick) % (CaretBlinkMs * 2) < CaretBlinkMs,
                OpenSelect = _openSelect,
                PopupHover = _popupHover,
                PopupScroll = _popupScroll,
            };
            _painter.Paint(canvas, Document, _ops, context);
            long t5 = Stopwatch.GetTimestamp();

            LastFrame.StyleMs = Ms(t0, t1);
            LastFrame.AnimateMs = Ms(t1, t2);
            LastFrame.LayoutMs = Ms(t2, t3);
            LastFrame.ListMs = Ms(t3, t4);
            LastFrame.PaintMs = Ms(t4, t5);
            LastFrame.TotalMs = Ms(t0, t5);
            LastFrame.OpCount = _ops.Count;
        }
    }

    /// <summary>
    /// Checks the incremental layout against a from-scratch one: forces a full uncached layout and compares
    /// every element's box and text positions with what the cache produced. Returns null when they match,
    /// otherwise a description of the first difference.
    /// </summary>
    public string? VerifyLayout()
    {
        lock (Document.SyncRoot)
        {
            var cached = GeometrySnapshot();

            _layout.CacheEnabled = false;
            Document.InvalidateAllLayout();
            Document.Root.MarkLayoutDirty();
            _layout.Layout(Document, _lastWidth, _lastHeight);
            _layout.CacheEnabled = true;

            var full = GeometrySnapshot();
            if (cached.Count != full.Count) return $"element count differs: cached {cached.Count}, full {full.Count}";
            for (int i = 0; i < cached.Count; i++)
                if (cached[i] != full[i]) return $"element {i}: cached [{cached[i]}] vs full [{full[i]}]";
            return null;
        }
    }

    /// <summary>
    /// Checks incremental restyling against a full one: forces every element to be restyled from scratch and
    /// compares each computed style (every public field) with what the incremental path produced.
    /// Returns null when they match. Not meaningful while animations are running, since those overlay values.
    /// </summary>
    public string? VerifyStyles()
    {
        lock (Document.SyncRoot)
        {
            if (Document.StyleDirty) _styles.Resolve(Document); // apply anything still pending first
            var incremental = StyleSnapshot();

            Document.InvalidateStyle();
            _styles.Resolve(Document);
            var full = StyleSnapshot();

            if (incremental.Count != full.Count) return $"element count differs: {incremental.Count} vs {full.Count}";
            for (int i = 0; i < incremental.Count; i++)
                if (incremental[i] != full[i]) return $"element {i}: incremental [{incremental[i]}] vs full [{full[i]}]";
            return null;
        }
    }

    List<string> StyleSnapshot()
    {
        var lines = new List<string>();
        void Walk(ElementNode el)
        {
            lines.Add($"{el.Tag}#{el.Id} {Describe(el.Style)}");
            foreach (var child in el.PhysicalChildren)
                if (child is ElementNode e) Walk(e);
        }
        Walk(Document.Root);
        return lines;
    }

    // Serialises every public field of a style, flattening arrays and lists.
    static string Describe(ComputedStyle style)
    {
        static string Value(object? v) => v switch
        {
            null => "null",
            string s => s,
            System.Collections.IDictionary d => $"dict({d.Count})",
            System.Collections.IEnumerable items => "[" + string.Join(",", items.Cast<object?>().Select(Value)) + "]",
            _ => v.ToString() ?? ""
        };

        return string.Join("|", typeof(ComputedStyle).GetFields().Where(f => !f.IsStatic)
            .Select(f => f.Name + "=" + (f.Name is "GridColumns" or "GridRows" ? "grid" : Value(f.GetValue(style)))));
    }

    List<string> GeometrySnapshot()
    {
        var lines = new List<string>();
        void Walk(ElementNode el)
        {
            if (el.Style.Display == Display.None) return;
            var b = el.BorderRect;
            var text = string.Join(";", el.PhysicalChildren.OfType<TextNode>().SelectMany(t => t.Runs)
                .Select(r => $"{r.Text}@{r.X:F1},{r.Baseline:F1}"));
            lines.Add($"{el.Tag} {b.Left:F1},{b.Top:F1},{b.Width:F1},{b.Height:F1} {text}");
            foreach (var child in el.PhysicalChildren)
                if (child is ElementNode e) Walk(e);
        }
        Walk(Document.Root);
        return lines;
    }

    public void SavePng(string path, int width, int height)
    {
        using var surface = SKSurface.Create(new SKImageInfo(width, height));
        Frame(surface.Canvas, width, height);
        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var file = File.Create(path);
        data.SaveTo(file);
    }

    // A component re-render can remove the element we were tracking.
    void DropDetachedState()
    {
        if (_hovered is not null && !IsAttached(_hovered)) _hovered = null;
        if (_pressed is not null && !IsAttached(_pressed)) _pressed = null;
        if (_focused is not null && !IsAttached(_focused)) _focused = null;
        if (_dragEdit is not null && !IsAttached(_dragEdit)) _dragEdit = null;
        if (_openSelect is not null && !IsAttached(_openSelect)) CloseSelect();
    }

    bool IsAttached(ElementNode el)
    {
        Node node = el;
        while (node.Parent is { } parent) node = parent;
        return node == Document.Root;
    }

    // ---- pointer ----

    HitResult HitTest(float x, float y) => DisplayListBuilder.HitTest(_ops, Document.Root, x, y);

    public void PointerMove(float x, float y)
    {
        _forceFrame = true; // any input may change what is drawn
        var events = new List<PendingEvent>();
        lock (Document.SyncRoot)
        {
            _pointerX = x;
            _pointerY = y;

            // Dragging a scrollbar thumb keeps working while the pointer leaves the track.
            if (_dragScrollbar is { } scrolling)
            {
                var local = _dragScrollbarInverse.Map(x, y);
                float pos = _dragScrollbarPart == ScrollbarPart.Vertical ? local.Y : local.X;
                float scroll = _dragScrollbarOriginScroll + (pos - _dragScrollbarOriginLocal) * (_dragScrollbarMax / _dragScrollbarRange);
                if (_dragScrollbarPart == ScrollbarPart.Vertical) scrolling.ScrollY = Math.Clamp(scroll, 0, _dragScrollbarMax);
                else scrolling.ScrollX = Math.Clamp(scroll, 0, _dragScrollbarMax);
                return;
            }

            // Dragging a selection keeps working while the pointer leaves the field.
            if (_dragEdit is { } dragging)
            {
                var edit = TextControls.GetEdit(dragging);
                var dragPoint = _dragInverse.Map(x, y);
                edit.MoveTo(TextControls.IndexFromPoint(dragging, edit, dragPoint.X, dragPoint.Y), extend: true);
                TextControls.EnsureCaretVisible(dragging, edit);
                _lastEditTick = _clock.ElapsedMilliseconds;
                return;
            }

            if (_openSelect is { } open)
            {
                var geometry = SelectPopup.Compute(open, Document.ViewportHeight);
                if (SelectPopup.Contains(geometry, open, x, y))
                {
                    _popupHover = SelectPopup.OptionAt(geometry, open, _popupScroll, x, y);
                    return;
                }
                _popupHover = -1;
            }

            var target = HitTest(x, y).Element;
            if (target != _hovered)
            {
                var previous = _hovered;
                _hovered = target;
                events.AddRange(HoverTransition(previous, target, x, y));
            }

            if (CollectHandlers(target, "mousemove", true, true, out _) is { Count: > 0 } move)
                events.Add(new PendingEvent(move, Mouse("mousemove", x, y)));
        }
        Fire(events);
    }

    List<PendingEvent> HoverTransition(ElementNode? from, ElementNode to, float x, float y)
    {
        var events = new List<PendingEvent>();
        var oldChain = Chain(from);
        var newChain = Chain(to);

        foreach (var el in oldChain.Where(e => !newChain.Contains(e)))
        {
            el.SetState(ElementState.Hover, false);
            if (el.Handlers.TryGetValue("mouseleave", out var id))
                events.Add(new PendingEvent(new List<ulong> { id }, Mouse("mouseleave", x, y)));
        }
        if (from is not null && CollectHandlers(from, "mouseout", true, true, out _) is { Count: > 0 } outIds)
            events.Add(new PendingEvent(outIds, Mouse("mouseout", x, y)));

        foreach (var el in Enumerable.Reverse(newChain).Where(e => !oldChain.Contains(e)))
        {
            el.SetState(ElementState.Hover, true);
            if (el.Handlers.TryGetValue("mouseenter", out var id))
                events.Add(new PendingEvent(new List<ulong> { id }, Mouse("mouseenter", x, y)));
        }
        if (CollectHandlers(to, "mouseover", true, true, out _) is { Count: > 0 } overIds)
            events.Add(new PendingEvent(overIds, Mouse("mouseover", x, y)));

        return events;
    }

    public void PointerDown(float x, float y)
    {
        _forceFrame = true; // any input may change what is drawn
        var events = new List<PendingEvent>();
        lock (Document.SyncRoot)
        {
            if (_openSelect is { } open)
            {
                var geometry = SelectPopup.Compute(open, Document.ViewportHeight);
                if (SelectPopup.Contains(geometry, open, x, y)) { _popupPressed = true; return; }

                // A press outside closes the list and is otherwise ignored, like a native dropdown.
                CloseSelect();
                _swallowPointer = true;
                return;
            }

            var hit = HitTest(x, y);
            if (hit.Scrollbar != ScrollbarPart.None) { BeginScrollbarDrag(hit, x, y); return; }

            var target = hit.Element;
            _pressed = target;
            foreach (var el in Chain(target)) el.SetState(ElementState.Active, true);

            if (CollectHandlers(target, "mousedown", true, true, out _) is { Count: > 0 } down)
                events.Add(new PendingEvent(down, Mouse("mousedown", x, y, buttons: 1)));

            // Pressing focuses the nearest focusable element, or clears focus.
            var focusTarget = Chain(target).FirstOrDefault(e => e.IsFocusable);
            events.AddRange(SetFocus(focusTarget, visible: focusTarget is not null && Controls.IsTextEditable(focusTarget)));

            if (focusTarget is not null && Controls.IsTextEditable(focusTarget) && !focusTarget.IsDisabled)
                BeginTextSelection(focusTarget, hit, x, y);
        }
        Fire(events);
    }

    void BeginTextSelection(ElementNode el, HitResult hit, float x, float y)
    {
        long now = _clock.ElapsedMilliseconds;
        bool repeated = now - _lastClickTick <= MultiClickMs
            && Math.Abs(x - _lastClickX) <= MultiClickSlop && Math.Abs(y - _lastClickY) <= MultiClickSlop;
        _clickCount = repeated ? _clickCount % 3 + 1 : 1;
        _lastClickTick = now;
        _lastClickX = x;
        _lastClickY = y;

        var edit = TextControls.GetEdit(el);
        var local = hit.ToLocal(x, y);
        int index = TextControls.IndexFromPoint(el, edit, local.X, local.Y);

        switch (_clickCount)
        {
            case 1: edit.MoveTo(index, extend: Shift); break;
            case 2: edit.SelectWordAt(index); break;
            default:
                if (edit.Multiline) edit.SelectLineAt(index); else edit.SelectAll();
                break;
        }

        _dragEdit = _clickCount == 1 ? el : null;
        _dragInverse = hit.Inverse;
        TextControls.EnsureCaretVisible(el, edit);
        _lastEditTick = now;
    }

    /// <summary>Starts dragging a scrollbar thumb, mapping pointer movement along the track to a scroll offset.</summary>
    void BeginScrollbarDrag(HitResult hit, float x, float y)
    {
        var el = hit.Element;
        var pad = el.PaddingBox;
        var local = hit.ToLocal(x, y);

        _dragScrollbar = el;
        _dragScrollbarPart = hit.Scrollbar;
        _dragScrollbarInverse = hit.Inverse;

        if (hit.Scrollbar == ScrollbarPart.Vertical)
        {
            float track = pad.Height;
            float thumb = Scrollbars.VerticalThumb(el)!.Value.Height + Scrollbars.Margin * 2;
            _dragScrollbarOriginLocal = local.Y;
            _dragScrollbarOriginScroll = el.ScrollY;
            _dragScrollbarRange = Math.Max(1, track - thumb);
            _dragScrollbarMax = el.MaxScrollY;
        }
        else
        {
            float track = pad.Width;
            float thumb = Scrollbars.HorizontalThumb(el)!.Value.Width + Scrollbars.Margin * 2;
            _dragScrollbarOriginLocal = local.X;
            _dragScrollbarOriginScroll = el.ScrollX;
            _dragScrollbarRange = Math.Max(1, track - thumb);
            _dragScrollbarMax = el.MaxScrollX;
        }
    }

    public void PointerUp(float x, float y)
    {
        _forceFrame = true; // any input may change what is drawn
        var events = new List<PendingEvent>();
        lock (Document.SyncRoot)
        {
            _dragEdit = null;
            _dragScrollbar = null;

            if (_swallowPointer) { _swallowPointer = false; return; }

            if (_popupPressed && _openSelect is { } open)
            {
                _popupPressed = false;
                var geometry = SelectPopup.Compute(open, Document.ViewportHeight);
                int index = SelectPopup.OptionAt(geometry, open, _popupScroll, x, y);
                if (index >= 0 && !geometry.Options[index].Disabled)
                {
                    events.AddRange(ChooseOption(open, geometry.Options[index]));
                    CloseSelect();
                }
                Fire(events);
                return;
            }

            var target = HitTest(x, y).Element;
            var pressed = _pressed;
            _pressed = null;
            foreach (var el in Chain(pressed)) el.SetState(ElementState.Active, false);

            if (CollectHandlers(target, "mouseup", true, true, out _) is { Count: > 0 } up)
                events.Add(new PendingEvent(up, Mouse("mouseup", x, y)));

            // A click lands on the closest element that contains both the press and the release.
            var clickTarget = pressed is null ? null : CommonAncestor(pressed, target);
            if (clickTarget is not null)
            {
                var ids = CollectHandlers(clickTarget, "click", true, true, out bool prevented);
                if (ids.Count > 0) events.Add(new PendingEvent(ids, Mouse("click", x, y, detail: Math.Max(1, _clickCount))));
                if (!prevented) events.AddRange(ActivateDefault(clickTarget));
            }
        }
        Fire(events);
    }

    public void Wheel(float x, float y, float notchesX, float notchesY)
    {
        _forceFrame = true; // any input may change what is drawn
        var events = new List<PendingEvent>();
        lock (Document.SyncRoot)
        {
            float dx = -notchesX * WheelPixelsPerNotch, dy = -notchesY * WheelPixelsPerNotch;

            if (_openSelect is { } open)
            {
                var geometry = SelectPopup.Compute(open, Document.ViewportHeight);
                if (SelectPopup.Contains(geometry, open, x, y))
                {
                    _popupScroll = Math.Clamp(_popupScroll + dy, 0, geometry.MaxScroll);
                    return;
                }
            }

            var target = HitTest(x, y).Element;

            var ids = CollectHandlers(target, "wheel", true, false, out bool prevented);
            if (ids.Count > 0)
            {
                // WheelEventArgs deltas follow the web convention: positive Y means scroll down.
                events.Add(new PendingEvent(ids, new WheelEventArgs
                {
                    Type = "wheel", ClientX = x, ClientY = y, DeltaX = -dx, DeltaY = -dy, DeltaMode = 0,
                    CtrlKey = Ctrl, ShiftKey = Shift, AltKey = Alt, MetaKey = Meta
                }));
            }

            if (!prevented && !ScrollTextArea(target, dy)) ScrollFrom(target, dx, dy);
        }
        Fire(events);

        // Content moved under a stationary pointer, so hover state may have changed.
        PointerMove(_pointerX, _pointerY);
    }

    /// <summary>A textarea scrolls its own text first; once it hits its limit the wheel moves on to ancestors.</summary>
    static bool ScrollTextArea(ElementNode? start, float dy)
    {
        if (dy == 0) return false;
        for (var el = start; el is not null; el = el.ParentElement)
        {
            if (!Controls.IsTextArea(el)) continue;

            var edit = TextControls.GetEdit(el);
            float max = TextControls.MaxScrollY(el, edit);
            if (max <= 0 || (dy < 0 ? edit.ScrollY <= 0 : edit.ScrollY >= max)) return false;
            edit.ScrollY = Math.Clamp(edit.ScrollY + dy, 0, max);
            return true;
        }
        return false;
    }

    /// <summary>Scrolls the nearest ancestor that can still move in each requested direction (scroll chaining).</summary>
    void ScrollFrom(ElementNode? start, float dx, float dy)
    {
        if (dy != 0)
        {
            for (var el = start; el is not null; el = el.ParentElement)
            {
                if (!el.ScrollsY || el.MaxScrollY <= 0) continue;
                if (dy < 0 ? el.ScrollY <= 0 : el.ScrollY >= el.MaxScrollY) continue;
                el.ScrollY = Math.Clamp(el.ScrollY + dy, 0, el.MaxScrollY);
                break;
            }
        }
        if (dx != 0)
        {
            for (var el = start; el is not null; el = el.ParentElement)
            {
                if (!el.ScrollsX || el.MaxScrollX <= 0) continue;
                if (dx < 0 ? el.ScrollX <= 0 : el.ScrollX >= el.MaxScrollX) continue;
                el.ScrollX = Math.Clamp(el.ScrollX + dx, 0, el.MaxScrollX);
                break;
            }
        }
    }

    // ---- focus ----

    /// <summary>Moves focus, updating :focus state and producing blur/focus/change events. Null clears focus.</summary>
    List<PendingEvent> SetFocus(ElementNode? next, bool visible)
    {
        var events = new List<PendingEvent>();
        var previous = _focused;

        if (previous == next)
        {
            // Re-focusing keeps focus but may upgrade it to keyboard-visible.
            if (next is not null) next.SetState(ElementState.FocusVisible, visible || next.State.HasFlag(ElementState.FocusVisible));
            return events;
        }

        if (previous is not null)
        {
            events.AddRange(CommitChange(previous));
            if (_openSelect == previous) CloseSelect();

            previous.SetState(ElementState.Focus, false);
            previous.SetState(ElementState.FocusVisible, false);
            if (previous.Handlers.TryGetValue("blur", out var blur))
                events.Add(new PendingEvent(new List<ulong> { blur }, new FocusEventArgs { Type = "blur" }));
            if (CollectHandlers(previous, "focusout", true, false, out _) is { Count: > 0 } outIds)
                events.Add(new PendingEvent(outIds, new FocusEventArgs { Type = "focusout" }));
        }

        _focused = next;
        if (next is not null)
        {
            next.SetState(ElementState.Focus, true);
            next.SetState(ElementState.FocusVisible, visible);
            if (Controls.IsTextEditable(next))
            {
                var edit = TextControls.GetEdit(next);
                _changeBaseline = edit.Value;
                _lastEditTick = _clock.ElapsedMilliseconds;
                // Tabbing into a field selects its text, as native fields do.
                if (visible && !_dragging && edit.Value.Length > 0) edit.SelectAll();
            }

            if (next.Handlers.TryGetValue("focus", out var focus))
                events.Add(new PendingEvent(new List<ulong> { focus }, new FocusEventArgs { Type = "focus" }));
            if (CollectHandlers(next, "focusin", true, false, out _) is { Count: > 0 } inIds)
                events.Add(new PendingEvent(inIds, new FocusEventArgs { Type = "focusin" }));
        }
        return events;
    }

    bool _dragging => _dragEdit is not null;

    /// <summary>Fires "change" for a text field whose value differs from when it was focused.</summary>
    List<PendingEvent> CommitChange(ElementNode el)
    {
        var events = new List<PendingEvent>();
        if (!Controls.IsTextEditable(el) || el.Edit is not { } edit || edit.Value == _changeBaseline) return events;

        _changeBaseline = edit.Value;
        if (CollectHandlers(el, "change", true, false, out _) is { Count: > 0 } ids)
            events.Add(new PendingEvent(ids, new ChangeEventArgs { Value = edit.Value }));
        return events;
    }

    /// <summary>Sequential focus order: positive tabindex values first (ascending), then the rest in document order.</summary>
    List<ElementNode> TabOrder()
    {
        var all = new List<ElementNode>();
        void Walk(ElementNode el)
        {
            if (el.Style.Display == Display.None) return;
            if (el.IsFocusable && el.TabIndex >= 0 && !el.Style.VisibilityHidden) all.Add(el);
            foreach (var child in el.PhysicalChildren)
                if (child is ElementNode e) Walk(e);
        }
        Walk(Document.Root);

        return all.Where(e => e.TabIndex > 0).OrderBy(e => e.TabIndex)
            .Concat(all.Where(e => e.TabIndex == 0)).ToList();
    }

    List<PendingEvent> MoveFocus(bool backwards)
    {
        var order = TabOrder();
        if (order.Count == 0) return new List<PendingEvent>();

        int current = _focused is null ? -1 : order.IndexOf(_focused);
        int next = current < 0
            ? (backwards ? order.Count - 1 : 0)
            : (current + (backwards ? -1 : 1) + order.Count) % order.Count;

        var events = SetFocus(order[next], visible: true);
        ScrollIntoView(order[next]);
        return events;
    }

    /// <summary>Scrolls ancestors the minimum amount needed to bring an element into view.</summary>
    void ScrollIntoView(ElementNode target)
    {
        var rect = target.BorderRect;
        float innerX = 0, innerY = 0; // scrolling already applied by scrollers nested inside the current one

        for (var a = target.ParentElement; a is not null; a = a.ParentElement)
        {
            if (!a.ClipsContent) continue;

            var pad = a.PaddingBox;
            var r = new SKRect(rect.Left - innerX, rect.Top - innerY, rect.Right - innerX, rect.Bottom - innerY);

            float sy = a.ScrollY, sx = a.ScrollX;
            if (r.Top - pad.Top < sy) sy = r.Top - pad.Top;
            else if (r.Bottom - pad.Bottom > sy) sy = r.Bottom - pad.Bottom;
            if (r.Left - pad.Left < sx) sx = r.Left - pad.Left;
            else if (r.Right - pad.Right > sx) sx = r.Right - pad.Right;

            a.ScrollY = Math.Clamp(sy, 0, a.MaxScrollY);
            a.ScrollX = Math.Clamp(sx, 0, a.MaxScrollX);
            innerX += a.ScrollX;
            innerY += a.ScrollY;
        }
    }

    // ---- keyboard ----

    public void KeyDown(string key, string code, float location, bool repeat)
    {
        _forceFrame = true; // any input may change what is drawn
        var events = new List<PendingEvent>();
        lock (Document.SyncRoot)
        {
            var target = _focused ?? Document.Root;
            var ids = CollectHandlers(target, "keydown", true, false, out bool prevented);
            if (ids.Count > 0) events.Add(new PendingEvent(ids, Keyboard("keydown", key, code, location, repeat)));

            if (!prevented) events.AddRange(DefaultKeyDown(key, repeat));
        }
        Fire(events);
    }

    public void KeyUp(string key, string code, float location)
    {
        _forceFrame = true; // any input may change what is drawn
        var events = new List<PendingEvent>();
        lock (Document.SyncRoot)
        {
            var target = _focused ?? Document.Root;
            var ids = CollectHandlers(target, "keyup", true, false, out bool prevented);
            if (ids.Count > 0) events.Add(new PendingEvent(ids, Keyboard("keyup", key, code, location, false)));

            // Space activates buttons, checkboxes and radios on release.
            if (!prevented && key == " " && _focused is { } f && IsSpaceActivated(f))
            {
                f.SetState(ElementState.Active, false);
                events.AddRange(ActivationClick(f));
            }
        }
        Fire(events);
    }

    static bool IsSpaceActivated(ElementNode el) =>
        !el.IsDisabled && (el.Tag == "button" || Controls.IsCheckable(el) || Controls.IsButtonInput(el));

    /// <summary>Characters produced by typing (after layout and IME), as opposed to key presses.</summary>
    public void TextInput(string text)
    {
        _forceFrame = true; // any input may change what is drawn
        var events = new List<PendingEvent>();
        lock (Document.SyncRoot)
        {
            if (_focused is not { } el || el.IsDisabled) return;

            if (Controls.IsSelect(el)) { SelectByTypeAhead(el, text, events); }
            else if (Controls.IsTextEditable(el) && !Controls.IsReadOnly(el))
            {
                if (Controls.InputType(el) == "number") text = new string(text.Where(c => "0123456789+-.eE".Contains(c)).ToArray());
                if (text.Length > 0) events.AddRange(EditText(el, e => e.Insert(text, "type")));
            }
        }
        Fire(events);
    }

    List<PendingEvent> DefaultKeyDown(string key, bool repeat)
    {
        if (key == "Tab") return MoveFocus(Shift);

        if (_focused is { } focused && !focused.IsDisabled)
        {
            if (Controls.IsTextEditable(focused)) return EditorKeyDown(focused, key, repeat);
            if (Controls.IsSelect(focused)) return SelectKeyDown(focused, key);
            if (Controls.IsRadio(focused) && key is "ArrowUp" or "ArrowDown" or "ArrowLeft" or "ArrowRight")
                return MoveRadio(focused, key is "ArrowUp" or "ArrowLeft" ? -1 : 1);

            switch (key)
            {
                case "Enter" when focused.Tag == "button" || Controls.IsButtonInput(focused)
                                  || (focused.Tag == "a" && focused.Attributes.ContainsKey("href")):
                    return repeat ? new List<PendingEvent>() : ActivationClick(focused);

                case " " when IsSpaceActivated(focused):
                    focused.SetState(ElementState.Active, true);
                    return new List<PendingEvent>();
            }
        }

        // Anything else scrolls the focused element's scroller, falling back to the page.
        var scroller = _focused ?? Document.Root;
        float page = Math.Max(1, Document.ViewportHeight * 0.9f);
        switch (key)
        {
            case "ArrowDown": ScrollFrom(scroller, 0, ArrowKeyScroll); break;
            case "ArrowUp": ScrollFrom(scroller, 0, -ArrowKeyScroll); break;
            case "ArrowRight": ScrollFrom(scroller, ArrowKeyScroll, 0); break;
            case "ArrowLeft": ScrollFrom(scroller, -ArrowKeyScroll, 0); break;
            case "PageDown": ScrollFrom(scroller, 0, page); break;
            case "PageUp": ScrollFrom(scroller, 0, -page); break;
            case " ": ScrollFrom(scroller, 0, Shift ? -page : page); break;
            case "Home": ScrollFrom(scroller, 0, -1e9f); break;
            case "End": ScrollFrom(scroller, 0, 1e9f); break;
        }
        return new List<PendingEvent>();
    }

    // ---- text editing ----

    /// <summary>Runs an edit on a text control, keeps the caret visible and reports an input event if the text changed.</summary>
    List<PendingEvent> EditText(ElementNode el, Func<TextEditState, bool> operation)
    {
        var events = new List<PendingEvent>();
        var edit = TextControls.GetEdit(el);

        bool changed = operation(edit);
        TextControls.EnsureCaretVisible(el, edit);
        _lastEditTick = _clock.ElapsedMilliseconds;

        if (changed && CollectHandlers(el, "input", true, false, out _) is { Count: > 0 } ids)
            events.Add(new PendingEvent(ids, new ChangeEventArgs { Value = edit.Value }));
        return events;
    }

    List<PendingEvent> EditorKeyDown(ElementNode el, string key, bool repeat)
    {
        var none = new List<PendingEvent>();
        var edit = TextControls.GetEdit(el);
        bool command = Ctrl || Meta;
        bool readOnly = Controls.IsReadOnly(el);
        bool password = Controls.IsPassword(el);
        float lineStep = Math.Max(1, (int)(el.ContentRect.Height / Math.Max(1, TextControls.LineHeight(el))));

        if (command)
        {
            switch (key.ToLowerInvariant())
            {
                case "a": return EditText(el, e => { e.SelectAll(); return false; });
                case "c":
                    if (edit.HasSelection && !password) SetClipboard(edit.SelectedText);
                    return none;
                case "x":
                    if (edit.HasSelection && !password && !readOnly)
                    {
                        SetClipboard(edit.SelectedText);
                        return EditText(el, e => e.Insert("", "cut"));
                    }
                    return none;
                case "v":
                    if (readOnly) return none;
                    string pasted = GetClipboard() ?? "";
                    if (Controls.InputType(el) == "number") pasted = new string(pasted.Where(c => "0123456789+-.eE".Contains(c)).ToArray());
                    return pasted.Length == 0 ? none : EditText(el, e => e.Insert(pasted, "paste"));
                case "z": return readOnly ? none : EditText(el, e => Shift ? e.Redo() : e.Undo());
                case "y": return readOnly ? none : EditText(el, e => e.Redo());
            }
        }

        switch (key)
        {
            case "Backspace" when !readOnly: return EditText(el, e => e.DeleteBackward(command));
            case "Delete" when !readOnly: return EditText(el, e => e.DeleteForward(command));

            case "ArrowLeft": return EditText(el, e => { e.MoveHorizontal(-1, command, Shift); return false; });
            case "ArrowRight": return EditText(el, e => { e.MoveHorizontal(1, command, Shift); return false; });

            case "ArrowUp" or "ArrowDown":
            {
                int direction = key == "ArrowUp" ? -1 : 1;
                if (Controls.InputType(el) == "number" && !readOnly) return StepNumber(el, -direction); // up increases
                return EditText(el, e =>
                {
                    int target = e.Multiline
                        ? TextControls.MoveVertical(el, e, e.Caret, direction)
                        : (direction < 0 ? 0 : e.Value.Length);
                    e.MoveTo(target, Shift);
                    return false;
                });
            }

            case "PageUp" or "PageDown" when edit.Multiline:
                return EditText(el, e =>
                {
                    e.MoveTo(TextControls.MoveVertical(el, e, e.Caret, (key == "PageUp" ? -1 : 1) * (int)lineStep), Shift);
                    return false;
                });

            case "Home":
                return EditText(el, e => { e.MoveTo(command ? 0 : TextControls.LineStartIndex(el, e, e.Caret), Shift); return false; });
            case "End":
                return EditText(el, e => { e.MoveTo(command ? e.Value.Length : TextControls.LineEndIndex(el, e, e.Caret), Shift); return false; });

            case "Enter":
                if (edit.Multiline) return readOnly ? none : EditText(el, e => e.Insert("\n", "enter"));
                if (repeat) return none;
                var events = CommitChange(el);
                events.AddRange(SubmitFormOf(el));
                return events;
        }
        return none;
    }

    List<PendingEvent> StepNumber(ElementNode el, int direction)
    {
        double step = double.TryParse(el.GetAttribute("step"), NumberStyles.Float, CultureInfo.InvariantCulture, out var s) && s > 0 ? s : 1;
        var edit = TextControls.GetEdit(el);
        double.TryParse(edit.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var current);

        double next = current + direction * step;
        if (double.TryParse(el.GetAttribute("min"), NumberStyles.Float, CultureInfo.InvariantCulture, out var min)) next = Math.Max(min, next);
        if (double.TryParse(el.GetAttribute("max"), NumberStyles.Float, CultureInfo.InvariantCulture, out var max)) next = Math.Min(max, next);

        string text = next.ToString("0.##########", CultureInfo.InvariantCulture);
        return EditText(el, e => { e.SelectAll(); return e.Insert(text, "step"); });
    }

    // ---- checkbox, radio, label, button, form ----

    /// <summary>Default behaviour after a click: toggle a checkable, open a select, follow a label, submit a form.</summary>
    List<PendingEvent> ActivateDefault(ElementNode target)
    {
        foreach (var el in Chain(target))
        {
            if (el.IsDisabled) return new List<PendingEvent>();

            if (Controls.IsCheckable(el)) return ToggleCheckable(el);
            if (Controls.IsSelect(el)) { ToggleSelect(el); return new List<PendingEvent>(); }
            if (el.Tag == "button" || Controls.IsButtonInput(el)) return SubmitIfSubmitButton(el);

            if (el.Tag == "label" && FindLabelControl(el) is { } control && !Chain(target).Contains(control))
                return ActivateViaLabel(control);
        }
        return new List<PendingEvent>();
    }

    List<PendingEvent> ActivateViaLabel(ElementNode control)
    {
        var events = new List<PendingEvent>();
        if (control.IsDisabled) return events;

        // The control sees a click of its own, then performs its normal activation.
        if (CollectHandlers(control, "click", false, true, out _) is { Count: > 0 } ids)
            events.Add(new PendingEvent(ids, Mouse("click", control.BorderRect.MidX, control.BorderRect.MidY, detail: 1)));

        events.AddRange(SetFocus(control, visible: Controls.IsTextEditable(control)));
        if (Controls.IsCheckable(control)) events.AddRange(ToggleCheckable(control));
        else if (Controls.IsSelect(control)) ToggleSelect(control);
        else if (control.Tag == "button" || Controls.IsButtonInput(control)) events.AddRange(SubmitIfSubmitButton(control));
        return events;
    }

    ElementNode? FindLabelControl(ElementNode label)
    {
        if (label.GetAttribute("for") is { } id)
        {
            ElementNode? found = null;
            void Find(ElementNode e)
            {
                if (found is not null) return;
                if (e.Id == id) { found = e; return; }
                foreach (var c in e.PhysicalChildren) if (c is ElementNode ce) Find(ce);
            }
            Find(Document.Root);
            return found;
        }

        ElementNode? first = null;
        void Walk(ElementNode e)
        {
            foreach (var child in e.PhysicalChildren)
            {
                if (first is not null) return;
                if (child is not ElementNode ce) continue;
                if (IsLabelable(ce)) { first = ce; return; }
                Walk(ce);
            }
        }
        Walk(label);
        return first;
    }

    static bool IsLabelable(ElementNode el) =>
        el.Tag is "textarea" or "select" or "button" || (el.Tag == "input" && Controls.InputType(el) != "hidden");

    List<PendingEvent> ToggleCheckable(ElementNode el)
    {
        var events = new List<PendingEvent>();
        bool isChecked = el.Attributes.ContainsKey("checked");

        if (Controls.IsRadio(el))
        {
            if (isChecked) return events;
            UncheckRadioGroup(el);
            el.SetAttribute("checked", "");
            AddChangeEvents(events, el, el.GetAttribute("value") ?? "on");
            return events;
        }

        if (isChecked) el.RemoveAttribute("checked"); else el.SetAttribute("checked", "");
        AddChangeEvents(events, el, !isChecked);
        return events;
    }

    void AddChangeEvents(List<PendingEvent> events, ElementNode el, object value)
    {
        if (CollectHandlers(el, "input", true, false, out _) is { Count: > 0 } input)
            events.Add(new PendingEvent(input, new ChangeEventArgs { Value = value }));
        if (CollectHandlers(el, "change", true, false, out _) is { Count: > 0 } change)
            events.Add(new PendingEvent(change, new ChangeEventArgs { Value = value }));
    }

    List<ElementNode> RadioGroup(ElementNode radio)
    {
        var group = new List<ElementNode>();
        string? name = radio.GetAttribute("name");
        var form = Chain(radio).FirstOrDefault(e => e.Tag == "form");

        void Walk(ElementNode e)
        {
            if (Controls.IsRadio(e) && !e.IsDisabled
                && (name is null ? e == radio : e.GetAttribute("name") == name)
                && Chain(e).FirstOrDefault(a => a.Tag == "form") == form)
                group.Add(e);
            foreach (var c in e.PhysicalChildren) if (c is ElementNode ce) Walk(ce);
        }
        Walk(Document.Root);
        return group;
    }

    void UncheckRadioGroup(ElementNode radio)
    {
        foreach (var other in RadioGroup(radio))
            if (other != radio) other.RemoveAttribute("checked");
    }

    List<PendingEvent> MoveRadio(ElementNode current, int direction)
    {
        var group = RadioGroup(current);
        int index = group.IndexOf(current);
        if (index < 0 || group.Count < 2) return new List<PendingEvent>();

        var next = group[(index + direction + group.Count) % group.Count];
        var events = SetFocus(next, visible: true);
        events.AddRange(ToggleCheckable(next));
        return events;
    }

    List<PendingEvent> SubmitIfSubmitButton(ElementNode button)
    {
        string type = (button.GetAttribute("type") ?? "submit").ToLowerInvariant();
        return type == "submit" ? SubmitFormOf(button) : new List<PendingEvent>();
    }

    List<PendingEvent> SubmitFormOf(ElementNode el)
    {
        var events = new List<PendingEvent>();
        var form = Chain(el).FirstOrDefault(e => e.Tag == "form");
        if (form is not null && CollectHandlers(form, "submit", false, false, out _) is { Count: > 0 } ids)
            events.Add(new PendingEvent(ids, new EventArgs()));
        return events;
    }

    // ---- select ----

    void ToggleSelect(ElementNode select)
    {
        if (_openSelect == select) { CloseSelect(); return; }
        OpenSelect(select);
    }

    void OpenSelect(ElementNode select)
    {
        _openSelect = select;
        var options = Controls.Options(select);
        _popupHover = Controls.SelectedIndex(select, options);
        _popupScroll = 0;

        // Start with the current choice in view.
        var geometry = SelectPopup.Compute(select, Document.ViewportHeight);
        if (_popupHover >= 0) ScrollPopupTo(geometry, _popupHover);
    }

    void CloseSelect()
    {
        _openSelect = null;
        _popupHover = -1;
        _popupPressed = false;
    }

    void ScrollPopupTo(PopupGeometry g, int index)
    {
        float top = index * g.ItemHeight, bottom = top + g.ItemHeight;
        float viewHeight = g.VisibleCount * g.ItemHeight;
        if (top < _popupScroll) _popupScroll = top;
        else if (bottom > _popupScroll + viewHeight) _popupScroll = bottom - viewHeight;
        _popupScroll = Math.Clamp(_popupScroll, 0, g.MaxScroll);
    }

    List<PendingEvent> ChooseOption(ElementNode select, Controls.SelectOption option)
    {
        var events = new List<PendingEvent>();
        var options = Controls.Options(select);
        bool changed = Controls.SelectedIndex(select, options) != options.FindIndex(o => o.Element == option.Element);

        select.SetAttribute("value", option.Value);
        if (changed) AddChangeEvents(events, select, option.Value);
        return events;
    }

    List<PendingEvent> SelectKeyDown(ElementNode select, string key)
    {
        var none = new List<PendingEvent>();
        var options = Controls.Options(select);
        if (options.Count == 0) return none;

        if (_openSelect == select)
        {
            var geometry = SelectPopup.Compute(select, Document.ViewportHeight);
            switch (key)
            {
                case "ArrowDown": _popupHover = NextEnabled(options, _popupHover, 1); ScrollPopupTo(geometry, _popupHover); break;
                case "ArrowUp": _popupHover = NextEnabled(options, _popupHover, -1); ScrollPopupTo(geometry, _popupHover); break;
                case "Home": _popupHover = NextEnabled(options, -1, 1); ScrollPopupTo(geometry, _popupHover); break;
                case "End": _popupHover = NextEnabled(options, options.Count, -1); ScrollPopupTo(geometry, _popupHover); break;
                case "Escape": CloseSelect(); break;
                case "Enter" or " ":
                {
                    var events = _popupHover >= 0 && !options[_popupHover].Disabled
                        ? ChooseOption(select, options[_popupHover]) : none;
                    CloseSelect();
                    return events;
                }
            }
            return none;
        }

        int current = Controls.SelectedIndex(select, options);
        switch (key)
        {
            case "ArrowDown" when Alt: OpenSelect(select); return none;
            case "Enter" or " ": OpenSelect(select); return none;
            case "ArrowDown" or "ArrowRight": return StepSelect(select, options, current, 1);
            case "ArrowUp" or "ArrowLeft": return StepSelect(select, options, current, -1);
            case "Home": return StepSelect(select, options, -1, 1);
            case "End": return StepSelect(select, options, options.Count, -1);
        }
        return none;
    }

    List<PendingEvent> StepSelect(ElementNode select, List<Controls.SelectOption> options, int from, int direction)
    {
        int next = NextEnabled(options, from, direction);
        return next >= 0 && next != from ? ChooseOption(select, options[next]) : new List<PendingEvent>();
    }

    static int NextEnabled(List<Controls.SelectOption> options, int from, int direction)
    {
        for (int i = from + direction; i >= 0 && i < options.Count; i += direction)
            if (!options[i].Disabled) return i;
        return from >= 0 && from < options.Count ? from : -1;
    }

    // Typing a letter jumps to the next option that starts with it.
    void SelectByTypeAhead(ElementNode select, string typed, List<PendingEvent> events)
    {
        var options = Controls.Options(select);
        if (options.Count == 0 || typed.Length == 0 || char.IsWhiteSpace(typed[0])) return;

        int start = _openSelect == select ? _popupHover : Controls.SelectedIndex(select, options);
        for (int step = 1; step <= options.Count; step++)
        {
            int i = (start + step) % options.Count;
            if (options[i].Disabled || !options[i].Label.StartsWith(typed, StringComparison.CurrentCultureIgnoreCase)) continue;

            if (_openSelect == select)
            {
                _popupHover = i;
                ScrollPopupTo(SelectPopup.Compute(select, Document.ViewportHeight), i);
            }
            else events.AddRange(ChooseOption(select, options[i]));
            return;
        }
    }

    // ---- event plumbing ----

    List<PendingEvent> ActivationClick(ElementNode el)
    {
        var events = new List<PendingEvent>();
        var center = new SKPoint(el.BorderRect.MidX, el.BorderRect.MidY);

        var ids = CollectHandlers(el, "click", true, true, out bool prevented);
        if (ids.Count > 0) events.Add(new PendingEvent(ids, Mouse("click", center.X, center.Y, detail: 0)));
        if (!prevented) events.AddRange(ActivateDefault(el));
        return events;
    }

    /// <summary>
    /// Gathers handler ids from the target outward. Stops after an element with stopPropagation and
    /// reports whether any element in the path asked for preventDefault. Mouse events on a disabled
    /// control are not delivered at all, matching browsers.
    /// </summary>
    static List<ulong> CollectHandlers(ElementNode? target, string name, bool bubbles, bool mouseLike, out bool prevented)
    {
        var ids = new List<ulong>();
        prevented = false;

        for (var el = target; el is not null; el = el.ParentElement)
        {
            if (mouseLike && el.IsDisabled) { ids.Clear(); prevented = false; return ids; }

            if (el.Handlers.TryGetValue(name, out var id))
            {
                ids.Add(id);
                if (el.Attributes.ContainsKey($"__internal_preventDefault_on{name}")) prevented = true;
                if (el.Attributes.ContainsKey($"__internal_stopPropagation_on{name}")) break;
            }
            if (!bubbles) break;
        }
        return ids;
    }

    static List<ElementNode> Chain(ElementNode? el)
    {
        var chain = new List<ElementNode>();
        for (; el is not null; el = el.ParentElement) chain.Add(el);
        return chain;
    }

    static ElementNode? CommonAncestor(ElementNode a, ElementNode? b)
    {
        if (b is null) return null;
        var ancestors = new HashSet<ElementNode>(Chain(a));
        return Chain(b).FirstOrDefault(ancestors.Contains);
    }

    MouseEventArgs Mouse(string type, float x, float y, long buttons = 0, long detail = 0) => new()
    {
        Type = type, ClientX = x, ClientY = y, ScreenX = x, ScreenY = y, Buttons = buttons, Detail = detail,
        CtrlKey = Ctrl, ShiftKey = Shift, AltKey = Alt, MetaKey = Meta
    };

    KeyboardEventArgs Keyboard(string type, string key, string code, float location, bool repeat) => new()
    {
        Type = type, Key = key, Code = code, Location = location, Repeat = repeat,
        CtrlKey = Ctrl, ShiftKey = Shift, AltKey = Alt, MetaKey = Meta
    };

    // Handlers for one event run in order, each awaited, so bubbling keeps the DOM's ordering.
    void Fire(List<PendingEvent> events)
    {
        if (events.Count == 0) return;
        _ = RunAsync(events);
    }

    async Task RunAsync(List<PendingEvent> events)
    {
        try
        {
            foreach (var e in events)
                foreach (var id in e.Handlers)
                    await Renderer.DispatchAsync(id, e.Args);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Event handler failed");
        }
    }

    public void Dispose()
    {
        Renderer.Dispose();
        _services.Dispose();
    }
}
