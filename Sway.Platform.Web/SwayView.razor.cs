using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using SkiaSharp.Views.Blazor;

namespace Sway.Widgets;

/// <summary>
/// Blazor WebAssembly host. Give it a parent with a size (<c>.sway-host</c> fills it) and pass the widget tree to run:
/// <c>&lt;SwayView Root="new MyApp()" /&gt;</c>. Everything runs on the single browser thread, so JS input callbacks,
/// timers and painting never race.
/// </summary>
public partial class SwayView : ComponentBase, IAsyncDisposable
{
    [Inject] IJSRuntime Js { get; set; } = null!;

    /// <summary>The widget tree to run.</summary>
    [Parameter, EditorRequired] public Widget Root { get; set; } = null!;

    /// <summary>Show the frame-rate overlay (also toggled with F3).</summary>
    [Parameter] public bool ShowFps { get; set; }

    ElementReference _host;
    SKGLView? _view;
    WidgetsBinding? _binding;
    DotNetObjectReference<SwayView>? _self;
    IJSObjectReference? _module;
    float _width, _height;
    string _clipboard = "";
    bool _disposed;

    protected override void OnInitialized()
    {
        SystemTheme.Source = new WebThemeSource();
        _binding = new WidgetsBinding { ShowFps = ShowFps, PlatformBrightness = SystemTheme.Brightness() };
        _binding.GetClipboard = () => _clipboard;
        _binding.SetClipboard = text => { _clipboard = text; _ = _module?.InvokeVoidAsync("writeClipboard", text); };
        _binding.AttachRoot(Root);
        _binding.OnFrameRequested = () => _view?.Invalidate();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender) return;
        _self = DotNetObjectReference.Create(this);
        _module = await Js.InvokeAsync<IJSObjectReference>("import", "./_content/Sway.Platform.Web/sway.js");
        FilePicker.Source = new WebFilePicker(_module);
        await _module.InvokeVoidAsync("attach", _host, _self);
        _binding!.RequestFrame();
    }

    void OnPaint(SKPaintGLSurfaceEventArgs e)
    {
        var binding = _binding;
        if (binding is null) return;

        var info = e.Info;
        // CSS pixels are the logical unit; the canvas has devicePixelRatio times as many pixels.
        float scale = _width > 0 ? info.Width / _width : 1f;
        var canvas = e.Surface.Canvas;
        canvas.Save();
        canvas.Scale(scale);
        binding.DrawFrame(canvas, info.Width / scale, info.Height / scale);
        canvas.Restore();
        canvas.Flush();

        if (binding.NeedsFrame(info.Width / scale, info.Height / scale)) _view?.Invalidate();
    }

    // ---- JS callbacks ----

    [JSInvokable]
    public void OnResize(float width, float height, bool dark)
    {
        _width = width;
        _height = height;
        if (_binding is null) return;
        _binding.PlatformBrightness = dark ? Brightness.Dark : Brightness.Light;
        _binding.RequestFrame();
    }

    [JSInvokable]
    public void OnPointerMove(float x, float y) => _binding?.Gestures.PointerMove(x, y);

    [JSInvokable]
    public void OnPointerDown(float x, float y)
    {
        if (_binding is null) return;
        _binding.Gestures.PointerMove(x, y);
        _binding.Gestures.PointerDown(x, y);
    }

    [JSInvokable]
    public void OnPointerUp(float x, float y) => _binding?.Gestures.PointerUp(x, y);

    [JSInvokable]
    public void OnWheel(float x, float y, float dx, float dy) => _binding?.Gestures.PointerScroll(x, y, dx, dy);

    [JSInvokable]
    public void OnKey(string key, string code, bool down, bool repeat, bool shift, bool ctrl, bool alt, bool meta)
    {
        var binding = _binding;
        if (binding is null) return;
        binding.Shift = shift; binding.Ctrl = ctrl; binding.Alt = alt; binding.Meta = meta;
        if (down)
        {
            if (key == "F3" && !repeat) binding.ShowFps = !binding.ShowFps;
            binding.KeyDown(key, code, repeat);
        }
        else
        {
            binding.KeyUp(key, code);
        }
        binding.Shift = binding.Ctrl = binding.Alt = binding.Meta = false;
    }

    [JSInvokable]
    public void OnText(string text) => _binding?.TextInput(text);

    [JSInvokable]
    public void OnClipboard(string text) => _clipboard = text;

    [JSInvokable]
    public void OnFocus() => _binding?.RequestFrame();

    /// <summary>The JS side calls this once per animation frame so timers and animations keep running.</summary>
    [JSInvokable]
    public void Tick()
    {
        if (_binding is not null && _width > 0 && _binding.NeedsFrame(_width, _height)) _view?.Invalidate();
    }

    /// <summary>The mouse cursor the JS side should show, as a CSS cursor name.</summary>
    [JSInvokable]
    public string GetCursor() => _binding?.Gestures.Cursor switch
    {
        MouseCursor.Click => "pointer",
        MouseCursor.Text => "text",
        MouseCursor.ResizeHorizontal => "ew-resize",
        MouseCursor.ResizeVertical => "ns-resize",
        MouseCursor.Grab => "grab",
        MouseCursor.Grabbing => "grabbing",
        MouseCursor.Move => "move",
        _ => "default",
    };

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        if (_module is not null)
        {
            try
            {
                await _module.InvokeVoidAsync("detach", _host);
                await _module.DisposeAsync();
            }
            catch (JSDisconnectedException) { }
        }
        _self?.Dispose();
    }
}
