using Microsoft.AspNetCore.Components;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.Windowing;
using SkiaSharp;

namespace Sway.Core.Platform;

/// <summary>Entry point: <c>App.Create().AddStylesheet("app.css").Run&lt;MainWindow&gt;("Title", 1024, 768)</c>.</summary>
public sealed class App
{
    readonly List<string> _stylesheets = new();

    public static App Create() => new();

    /// <summary>Adds a stylesheet from an absolute or working-directory path (used by the headless runner's --css flag).</summary>
    public App AddStylesheet(string path)
    {
        _stylesheets.Add(path);
        return this;
    }

    /// <summary>
    /// Renders the component to a PNG without opening a window. Each step (a simulated pointer or key
    /// action) runs against a freshly laid-out frame, so hit testing sees what the previous step produced.
    /// </summary>
    public void Screenshot<TRoot>(string pngPath, int width, int height, IEnumerable<Action<UiHost>>? steps = null)
        where TRoot : IComponent
    {
        using var host = new UiHost(_stylesheets);
        host.UseManualClock(); // animations only move when a step advances the clock
        host.MountAsync<TRoot>().GetAwaiter().GetResult();
        host.SavePng(pngPath, width, height);

        foreach (var step in steps ?? Enumerable.Empty<Action<UiHost>>())
        {
            step(host);
            Thread.Sleep(50); // handlers run asynchronously
            host.SavePng(pngPath, width, height);
        }
    }

    public void Run<TRoot>(string title, int width, int height) where TRoot : IComponent
    {
        using var host = new UiHost(_stylesheets);

        var options = WindowOptions.Default with
        {
            Title = title,
            Size = new Vector2D<int>(width, height),
            API = new GraphicsAPI(ContextAPI.OpenGL, ContextProfile.Core, ContextFlags.ForwardCompatible, new APIVersion(3, 3)),
            PreferredStencilBufferBits = 8,
            PreferredBitDepth = new Vector4D<int>(8, 8, 8, 8),
            ShouldSwapAutomatically = false, // buffers are swapped only when a frame was actually drawn
        };

        var window = Window.Create(options);
        IInputContext? input = null;
        GRContext? grContext = null;
        SKSurface? surface = null;
        Vector2D<int> surfaceSize = default;
        var mice = new List<IMouse>();
        var heldKeys = new HashSet<Key>();
        string appliedCursor = "auto";

        window.Load += () =>
        {
            var glInterface = GRGlInterface.Create(name =>
                window.GLContext!.TryGetProcAddress(name, out var address) ? address : IntPtr.Zero);
            grContext = GRContext.CreateGl(glInterface);

            input = window.CreateInput();
            foreach (var mouse in input.Mice)
            {
                mouse.MouseMove += (m, p) => host.PointerMove(p.X, p.Y);
                mouse.MouseDown += (m, b) => { if (b == MouseButton.Left) host.PointerDown(m.Position.X, m.Position.Y); };
                mouse.MouseUp += (m, b) => { if (b == MouseButton.Left) host.PointerUp(m.Position.X, m.Position.Y); };
                mouse.Scroll += (m, wheel) => host.Wheel(m.Position.X, m.Position.Y, wheel.X, wheel.Y);
                mice.Add(mouse);
            }

            foreach (var keyboard in input.Keyboards)
            {
                host.GetClipboard = () => keyboard.ClipboardText;
                host.SetClipboard = text => keyboard.ClipboardText = text;

                // Characters arrive as UTF-16 units; hold a high surrogate until its pair shows up.
                char pendingHigh = '\0';
                keyboard.KeyChar += (k, c) =>
                {
                    if (char.IsHighSurrogate(c)) { pendingHigh = c; return; }
                    if (char.IsLowSurrogate(c) && pendingHigh != '\0') { host.TextInput(new string(new[] { pendingHigh, c })); pendingHigh = '\0'; return; }
                    pendingHigh = '\0';
                    if (!char.IsControl(c)) host.TextInput(c.ToString());
                };

                keyboard.KeyDown += (k, key, _) =>
                {
                    SyncModifiers(k);
                    var (name, code) = KeyMap.Translate(key, host.Shift);
                    // Holding a key makes GLFW report repeats as further KeyDown events.
                    bool repeat = !heldKeys.Add(key);
                    host.KeyDown(name, code, repeat);
                };
                keyboard.KeyUp += (k, key, _) =>
                {
                    SyncModifiers(k);
                    heldKeys.Remove(key);
                    var (name, code) = KeyMap.Translate(key, host.Shift);
                    host.KeyUp(name, code);
                };
            }

            void SyncModifiers(IKeyboard k)
            {
                host.Shift = k.IsKeyPressed(Key.ShiftLeft) || k.IsKeyPressed(Key.ShiftRight);
                host.Ctrl = k.IsKeyPressed(Key.ControlLeft) || k.IsKeyPressed(Key.ControlRight);
                host.Alt = k.IsKeyPressed(Key.AltLeft) || k.IsKeyPressed(Key.AltRight);
                host.Meta = k.IsKeyPressed(Key.SuperLeft) || k.IsKeyPressed(Key.SuperRight);
            }

            _ = host.MountAsync<TRoot>().ContinueWith(
                t => Console.Error.WriteLine(t.Exception),
                TaskContinuationOptions.OnlyOnFaulted);
        };

        window.Render += _ =>
        {
            var framebuffer = window.FramebufferSize;
            if (framebuffer.X <= 0 || framebuffer.Y <= 0 || grContext is null) return;

            // Nothing changed: skip the frame and give the CPU back instead of spinning.
            if (surface is not null && framebuffer == surfaceSize && !host.NeedsFrame(window.Size.X, window.Size.Y))
            {
                Thread.Sleep(2);
                return;
            }

            if (surface is null || framebuffer != surfaceSize)
            {
                surface?.Dispose();
                var target = new GRBackendRenderTarget(framebuffer.X, framebuffer.Y, 0, 8, new GRGlFramebufferInfo(0, 0x8058));
                surface = SKSurface.Create(grContext, target, GRSurfaceOrigin.BottomLeft, SKColorType.Rgba8888);
                surfaceSize = framebuffer;
            }

            var canvas = surface!.Canvas;
            // Layout works in logical pixels; scale up for high-DPI displays.
            float scale = framebuffer.X / (float)window.Size.X;
            canvas.Save();
            canvas.Scale(scale);
            host.Frame(canvas, window.Size.X, window.Size.Y);
            canvas.Restore();
            canvas.Flush();
            grContext.Flush();

            window.SwapBuffers();

            string cursor = host.Cursor;
            if (cursor != appliedCursor)
            {
                appliedCursor = cursor;
                foreach (var mouse in mice) mouse.Cursor.StandardCursor = KeyMap.Cursor(cursor);
            }
        };

        // Uncovering or restoring the window must redraw even though no document state changed.
        window.StateChanged += _ => host.RequestFrame();
        window.FocusChanged += _ => host.RequestFrame();
        window.FramebufferResize += _ => host.RequestFrame();

        window.Closing += () =>
        {
            surface?.Dispose();
            grContext?.Dispose();
            input?.Dispose();
        };

        window.Run();
    }
}
