using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.Windowing;
using SkiaSharp;

namespace Sway.Widgets;

/// <summary>Entry point: <c>App.Run(new MyApp(), "Title", 1024, 768)</c>.</summary>
public static class App
{
    /// <summary>Opens a window and runs <paramref name="root"/> until it is closed.</summary>
    public static void Run(Widget root, string title = "Sway", int width = 1024, int height = 768)
    {
        var binding = new WidgetsBinding();
        binding.AttachRoot(root);

        var options = WindowOptions.Default with
        {
            Title = title,
            Size = new Vector2D<int>(width, height),
            API = new GraphicsAPI(ContextAPI.OpenGL, ContextProfile.Core, ContextFlags.ForwardCompatible, new APIVersion(3, 3)),
            PreferredStencilBufferBits = 8,
            PreferredBitDepth = new Vector4D<int>(8, 8, 8, 8),
            ShouldSwapAutomatically = false, // swap only when a frame was drawn
        };

        var window = Window.Create(options);
        IInputContext? input = null;
        GRContext? grContext = null;
        SKSurface? surface = null;
        Vector2D<int> surfaceSize = default;
        var mice = new List<IMouse>();
        var appliedCursor = MouseCursor.Default;

        window.Load += () =>
        {
            var glInterface = GRGlInterface.Create(name =>
                window.GLContext!.TryGetProcAddress(name, out var address) ? address : IntPtr.Zero);
            grContext = GRContext.CreateGl(glInterface);

            input = window.CreateInput();
            foreach (var mouse in input.Mice)
            {
                mouse.MouseMove += (m, p) => binding.Gestures.PointerMove(p.X, p.Y);
                mouse.MouseDown += (m, b) => { if (b == MouseButton.Left) binding.Gestures.PointerDown(m.Position.X, m.Position.Y); };
                mouse.MouseUp += (m, b) => { if (b == MouseButton.Left) binding.Gestures.PointerUp(m.Position.X, m.Position.Y); };
                // One wheel notch is 100 logical pixels (3 lines on most platforms).
                mouse.Scroll += (m, wheel) => binding.Gestures.PointerScroll(m.Position.X, m.Position.Y, -wheel.X * 100, -wheel.Y * 100);
                mice.Add(mouse);
            }
        };

        window.Render += _ =>
        {
            var framebuffer = window.FramebufferSize;
            if (framebuffer.X <= 0 || framebuffer.Y <= 0 || grContext is null) return;

            if (surface is not null && framebuffer == surfaceSize && !binding.NeedsFrame(window.Size.X, window.Size.Y))
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
            float scale = framebuffer.X / (float)window.Size.X;
            canvas.Save();
            canvas.Scale(scale);
            binding.DrawFrame(canvas, window.Size.X, window.Size.Y);
            canvas.Restore();
            canvas.Flush();
            grContext.Flush();
            window.SwapBuffers();

            var cursor = binding.Gestures.Cursor;
            if (cursor != appliedCursor)
            {
                appliedCursor = cursor;
                foreach (var mouse in mice) mouse.Cursor.StandardCursor = ToStandardCursor(cursor);
            }
        };

        window.StateChanged += _ => binding.RequestFrame();
        window.FocusChanged += _ => binding.RequestFrame();
        window.FramebufferResize += _ => binding.RequestFrame();

        window.Closing += () =>
        {
            surface?.Dispose();
            grContext?.Dispose();
            input?.Dispose();
        };

        window.Run();
    }

    static StandardCursor ToStandardCursor(MouseCursor c) => c switch
    {
        MouseCursor.Click => StandardCursor.Hand,
        MouseCursor.Text => StandardCursor.IBeam,
        MouseCursor.ResizeHorizontal => StandardCursor.HResize,
        MouseCursor.ResizeVertical => StandardCursor.VResize,
        MouseCursor.Grab or MouseCursor.Grabbing or MouseCursor.Move => StandardCursor.Hand,
        _ => StandardCursor.Default,
    };

    /// <summary>
    /// Renders the widget to a PNG without opening a window. Each step simulates input against a freshly
    /// laid-out frame, then a new screenshot overwrites the file.
    /// </summary>
    public static void Screenshot(Widget root, string pngPath, int width, int height, IEnumerable<Action<WidgetsBinding>>? steps = null)
    {
        var binding = new WidgetsBinding();
        binding.UseManualClock();
        binding.AttachRoot(root);
        SavePng(binding, pngPath, width, height);

        foreach (var step in steps ?? Enumerable.Empty<Action<WidgetsBinding>>())
        {
            step(binding);
            SavePng(binding, pngPath, width, height);
        }
    }

    static void SavePng(WidgetsBinding binding, string path, int width, int height)
    {
        using var bitmap = binding.RenderToBitmap(width, height);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = File.Create(path);
        data.SaveTo(stream);
    }
}
