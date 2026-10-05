---
sidebar_position: 2
---

# Getting started

## Prerequisites

- .NET 10 SDK.
- Windows is the tested platform. Text uses Roboto if installed and falls back to Segoe UI; other platforms need their
  own font mapping (see [Known limits](./known-limits)).
- A GPU and driver with OpenGL 3.3 core for the live window. The headless screenshot path renders on the CPU and
  needs no GPU.

## Solution layout

```
Sway.slnx
Sway.Widgets/           # the platform-neutral library: Foundation, Rendering, Widgets
Sway.Platform.Desktop/  # Windows/Linux/macOS host: window, input, OS theme (Silk.NET)
Sway.Platform.Android/  # Android host: GL surface, touch, soft keyboard, system theme
Sway.Example/           # the shared example UI (platform-neutral)
Sway.Example.Desktop/   # desktop runner for the example
Sway.Platform.Headless/ # windowless host: renders to a PNG
Sway.Example.Headless/  # headless runner for the example (screenshots, bench)
Sway.Example.Android/   # Android runner for the example
```

`Sway.Widgets` is the library. It depends only on SkiaSharp and SkiaSharp.HarfBuzz, so it carries no windowing or OS
code. `Sway.Platform.Desktop` adds the window and input host (Silk.NET); an app references both and calls `App.Run`.
Add `using Sway.Widgets;`.

## Run the demo

```bash
dotnet run --project Sway.Example.Desktop
```

This opens a window with a navigation rail and pages for components, layout, forms, motion, effects, right-to-left
and a stress test. The app bar has switches for light/dark mode, the seed colour and text direction.

## Your first app

```csharp
using Sway.Widgets;

App.Run(new MaterialApp(home: new HelloPage()), "Hello", 640, 480);

class HelloPage : StatelessWidget
{
    public override Widget Build(BuildContext context) => new Scaffold(
        appBar: new AppBar(title: new Text("Hello")),
        body: new Center(new FilledButton(new Text("Press me"),
            () => Dialogs.ShowSnackBar(context, "Pressed"))));
}
```

`App.Run(widget, title, width, height)` opens the window and mounts the widget. `MaterialApp` installs the theme,
default text style and surface colour; without it widgets still work and use the default Material 3 light theme.

### Stateful widgets

```csharp
class Counter : StatefulWidget
{
    public override State CreateState() => new CounterState();
}

class CounterState : State<Counter>
{
    int _count;

    public override Widget Build(BuildContext context) =>
        new FilledButton(new Text($"Clicked {_count} times"), () => SetState(() => _count++));
}
```

Constructors use C# named arguments and collection expressions, which keeps trees close to Flutter's shape:

```csharp
new Column(
    crossAxisAlignment: CrossAxisAlignment.Start,
    spacing: 8,
    children:
    [
        new Text("Title", style: Theme.Of(context).TextTheme.TitleLarge),
        new Row(children: [new Icon(Icons.Info), new Text("Details")]),
    ])
```

## Headless mode: screenshots and benchmarks

The demo's `Program.cs` wires up a small CLI built on `App.Screenshot(widget, path, width, height, steps)`. It renders
without opening a window, which is useful for checking layout changes or scripting input without a display:

```bash
# Render a page to PNG without opening a window
dotnet run --project Sway.Example.Headless -- --screenshot out.png --page layout

# Dark mode, right-to-left, custom size
dotnet run --project Sway.Example.Headless -- --screenshot out.png --page rtl --dark --size 1100x900

# Script pointer and keyboard input before capturing
dotnet run --project Sway.Example.Headless -- --screenshot out.png --page forms --click 300,230 --type "hello" --key Tab

# Per-frame cost for scrolling and hovering a page (CPU raster)
dotnet run --project Sway.Example.Headless -- --bench --page stress
```

Pages: `components`, `layout`, `forms`, `motion`, `effects`, `rtl`, `stress`. Steps: `--move x,y`, `--click x,y`,
`--wheel x,y,delta`, `--key [ctrl+][shift+]Name`, `--type text`, `--advance ms` (advances the manual animation
clock). Steps run in order and each one re-renders the PNG, so the file ends up showing the final state.

You can do the same in your own code:

```csharp
App.Screenshot(new MyPage(), "out.png", 800, 600, new Action<WidgetsBinding>[]
{
    b => b.Gestures.PointerDown(100, 100),
    b => b.Gestures.PointerUp(100, 100),
    b => b.AdvanceClock(TimeSpan.FromMilliseconds(300)),
});
```
