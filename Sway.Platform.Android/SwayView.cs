using Android.Content;
using Android.Opengl;
using Android.OS;
using Android.Views;
using Android.Views.InputMethods;
using Javax.Microedition.Khronos.Egl;
using Javax.Microedition.Khronos.Opengles;
using SkiaSharp;

namespace Sway.Widgets;

/// <summary>
/// Hosts a <see cref="WidgetsBinding"/> in a GL surface. All binding work runs on the GL thread; touch, key and IME
/// events are queued onto it, so the widget tree is only ever touched from one thread.
/// </summary>
public sealed class SwayView : GLSurfaceView, GLSurfaceView.IRenderer
{
    readonly WidgetsBinding _binding;
    readonly Handler _ui = new(Looper.MainLooper!);
    readonly Java.Lang.IRunnable _poll;
    GRContext? _grContext;
    SKSurface? _surface;
    int _width, _height;
    bool _keyboardShown;
    bool _polling;

    public SwayView(Context context, WidgetsBinding binding) : base(context)
    {
        _binding = binding;
        Focusable = true;
        FocusableInTouchMode = true;
        SetEGLContextClientVersion(3);
        SetEGLConfigChooser(8, 8, 8, 8, 0, 8);
        SetRenderer(this);
        RenderMode = Rendermode.WhenDirty;
        _poll = new Java.Lang.Runnable(PollFrame);
        binding.OnFrameRequested = RequestRender;
    }

    /// <summary>Physical pixels per logical pixel: widgets lay out in dp.</summary>
    float Density => Resources?.DisplayMetrics?.Density ?? 1f;

    // A timer due in the future (or a frame callback) cannot call RequestRender by itself, so check on a slow tick.
    void PollFrame()
    {
        if (!_polling) return;
        QueueEvent(() =>
        {
            if (_binding.NeedsFrame(_width / Density, _height / Density)) RequestRender();
        });
        _ui.PostDelayed(_poll, 16);
    }

    protected override void OnAttachedToWindow()
    {
        base.OnAttachedToWindow();
        _polling = true;
        _ui.PostDelayed(_poll, 16);
    }

    protected override void OnDetachedFromWindow()
    {
        _polling = false;
        _ui.RemoveCallbacks(_poll);
        base.OnDetachedFromWindow();
    }

    // ---- renderer (GL thread) ----

    public void OnSurfaceCreated(IGL10? gl, Javax.Microedition.Khronos.Egl.EGLConfig? config)
    {
        _surface?.Dispose();
        _surface = null;
        _grContext?.Dispose();
        _grContext = GRContext.CreateGl(GRGlInterface.Create());
    }

    public void OnSurfaceChanged(IGL10? gl, int width, int height)
    {
        _width = width;
        _height = height;
        _surface?.Dispose();
        _surface = null;
        _binding.RequestFrame();
    }

    public void OnDrawFrame(IGL10? gl)
    {
        if (_grContext is null || _width <= 0 || _height <= 0) return;

        if (_surface is null)
        {
            // GLSurfaceView renders into framebuffer 0; 0x8058 is GL_RGBA8.
            var target = new GRBackendRenderTarget(_width, _height, 0, 8, new GRGlFramebufferInfo(0, 0x8058));
            _surface = SKSurface.Create(_grContext, target, GRSurfaceOrigin.BottomLeft, SKColorType.Rgba8888);
        }

        float density = Density;
        var canvas = _surface!.Canvas;
        canvas.Save();
        canvas.Scale(density);
        _binding.DrawFrame(canvas, _width / density, _height / density);
        canvas.Restore();
        canvas.Flush();
        _grContext.Flush();

        UpdateKeyboard();
        if (_binding.NeedsFrame(_width / density, _height / density)) RequestRender();
    }

    // ---- touch ----

    public override bool OnTouchEvent(MotionEvent? e)
    {
        if (e is null) return false;
        float density = Density;
        float x = e.GetX() / density, y = e.GetY() / density;
        switch (e.ActionMasked)
        {
            case MotionEventActions.Down:
                QueueEvent(() => { _binding.Gestures.PointerMove(x, y); _binding.Gestures.PointerDown(x, y); });
                break;
            case MotionEventActions.Move:
                QueueEvent(() => _binding.Gestures.PointerMove(x, y));
                break;
            case MotionEventActions.Up:
            case MotionEventActions.Cancel:
                QueueEvent(() => _binding.Gestures.PointerUp(x, y));
                break;
        }
        return true;
    }

    // Mouse wheel and trackpad scrolling arrive as generic motion events.
    public override bool OnGenericMotionEvent(MotionEvent? e)
    {
        if (e is { ActionMasked: MotionEventActions.Scroll })
        {
            float density = Density;
            float x = e.GetX() / density, y = e.GetY() / density;
            float dx = -e.GetAxisValue(Android.Views.Axis.Hscroll) * 100, dy = -e.GetAxisValue(Android.Views.Axis.Vscroll) * 100;
            QueueEvent(() => _binding.Gestures.PointerScroll(x, y, dx, dy));
            return true;
        }
        return base.OnGenericMotionEvent(e);
    }

    // ---- hardware keys ----

    public override bool OnKeyDown(Keycode keyCode, Android.Views.KeyEvent? e) => RouteKey(keyCode, e, true) || base.OnKeyDown(keyCode, e);
    public override bool OnKeyUp(Keycode keyCode, Android.Views.KeyEvent? e) => RouteKey(keyCode, e, false) || base.OnKeyUp(keyCode, e);

    bool RouteKey(Keycode keyCode, Android.Views.KeyEvent? e, bool down)
    {
        if (e is null) return false;
        string? key = AndroidKeyMap.Translate(keyCode);
        string? text = null;
        if (key is null)
        {
            // Printable characters from a hardware keyboard are text, not key presses.
            int unicode = e.UnicodeChar & 0x7FFFFFFF; // the top bit flags a combining accent
            if (unicode <= 0 || e.IsCtrlPressed) return false;
            text = char.ConvertFromUtf32(unicode);
        }

        bool shift = e.IsShiftPressed, ctrl = e.IsCtrlPressed, alt = e.IsAltPressed, meta = e.IsMetaPressed, repeat = e.RepeatCount > 0;
        QueueEvent(() =>
        {
            _binding.Shift = shift; _binding.Ctrl = ctrl; _binding.Alt = alt; _binding.Meta = meta;
            if (key is not null)
            {
                if (down) _binding.KeyDown(key, key, repeat); else _binding.KeyUp(key, key);
            }
            else if (down && text is not null)
            {
                _binding.TextInput(text);
            }
            _binding.Shift = _binding.Ctrl = _binding.Alt = _binding.Meta = false;
        });
        return true;
    }

    // ---- soft keyboard ----

    // The keyboard follows focus: shown while a text-editing node holds it.
    void UpdateKeyboard()
    {
        bool want = _binding.Focus.Primary?.OnTextInput is not null;
        if (want == _keyboardShown) return;
        _keyboardShown = want;
        _ui.Post(() =>
        {
            var imm = (InputMethodManager?)Context?.GetSystemService(Context.InputMethodService);
            if (imm is null) return;
            if (want)
            {
                RequestFocus();
                imm.ShowSoftInput(this, ShowFlags.Implicit);
            }
            else
            {
                imm.HideSoftInputFromWindow(WindowToken, HideSoftInputFlags.None);
            }
        });
    }

    public override bool OnCheckIsTextEditor() => true;

    public override IInputConnection? OnCreateInputConnection(EditorInfo? outAttrs)
    {
        if (outAttrs is not null)
        {
            // Visible-password mode asks the keyboard to commit characters directly instead of composing them,
            // which is all this host understands for now.
            outAttrs.InputType = Android.Text.InputTypes.ClassText | Android.Text.InputTypes.TextVariationVisiblePassword | Android.Text.InputTypes.TextFlagNoSuggestions;
            outAttrs.ImeOptions = ImeFlags.NoExtractUi | ImeFlags.NoFullscreen | ImeFlags.NoEnterAction;
        }
        return new SwayInputConnection(this, _binding);
    }

    internal void RunOnGl(Action action) => QueueEvent(action);
}

/// <summary>Forwards committed text and editing keys from the soft keyboard into the binding.</summary>
sealed class SwayInputConnection : BaseInputConnection
{
    readonly SwayView _view;
    readonly WidgetsBinding _binding;

    public SwayInputConnection(SwayView view, WidgetsBinding binding) : base(view, false)
    {
        _view = view;
        _binding = binding;
    }

    void Tap(string key)
    {
        _binding.KeyDown(key, key);
        _binding.KeyUp(key, key);
    }

    public override bool CommitText(Java.Lang.ICharSequence? text, int newCursorPosition)
    {
        string? s = text?.ToString();
        if (string.IsNullOrEmpty(s)) return true;
        _view.RunOnGl(() =>
        {
            // Enter arrives as a newline in the text stream.
            var lines = s.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                if (i > 0) Tap("Enter");
                if (lines[i].Length > 0) _binding.TextInput(lines[i]);
            }
        });
        return true;
    }

    public override bool SetComposingText(Java.Lang.ICharSequence? text, int newCursorPosition) => CommitText(text, newCursorPosition);

    public override bool DeleteSurroundingText(int beforeLength, int afterLength)
    {
        _view.RunOnGl(() =>
        {
            for (int i = 0; i < beforeLength; i++) Tap("Backspace");
            for (int i = 0; i < afterLength; i++) Tap("Delete");
        });
        return true;
    }

    public override bool SendKeyEvent(Android.Views.KeyEvent? e)
    {
        if (e is null) return false;
        string? key = AndroidKeyMap.Translate(e.KeyCode);
        if (key is null) return base.SendKeyEvent(e);
        bool down = e.Action == KeyEventActions.Down;
        _view.RunOnGl(() => { if (down) _binding.KeyDown(key, key); else _binding.KeyUp(key, key); });
        return true;
    }

    public override bool PerformEditorAction(ImeAction actionCode)
    {
        _view.RunOnGl(() => Tap("Enter"));
        return true;
    }
}
