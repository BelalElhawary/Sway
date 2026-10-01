---
sidebar_position: 2
---

# Getting started

## Prerequisites

- .NET 10 SDK.
- Windows. Font fallback currently assumes Segoe UI, Times New Roman and Consolas are installed; other platforms
  need their own font mapping (see [Known limits](./known-limits)).
- A GPU/driver with OpenGL 3.3 core for the live window. The headless screenshot path renders on the CPU and needs
  no GPU.

## Solution layout

```
Sway.slnx
Sway.Core/     # the rendering engine: Dom, Layout, Styling, Rendering, Platform
Sway.Demo/     # a sample Blazor app that exercises Sway.Core
```

`Sway.Core` is the library — it has no dependency on the sample. `Sway.Demo` references `Sway.Core` and contains the
`.razor` pages used to exercise it, an `app.css` stylesheet, and the `Program.cs` entry point described below.

## Run the sample app

```bash
dotnet run --project Sway.Demo
```

This opens a real window and runs `MainWindow`, the demo's page-switching menu. `Program.cs` builds the app with:

```csharp
var app = App.Create().AddStylesheet("app.css");
app.Run<MainWindow>("My App", 1024, 768);
```

`App.Create()` returns a builder; `AddStylesheet` queues a CSS file (resolved relative to the working directory or
as an absolute path); `Run<TRoot>` opens the window and mounts `TRoot` as the root component. Any `IComponent`
(i.e. any `.razor` component) can be the root.

## Headless mode: screenshots, benchmarks, verification

The demo's `Program.cs` also wires up a small CLI, built on `App.Screenshot<TRoot>` and `UiHost`, that renders
without opening a window — useful for checking layout changes or scripting input without a display:

```bash
# Render a page to PNG without opening a window
dotnet run --project Sway.Demo -- --screenshot out.png --page layout

# Script pointer/keyboard input before capturing
dotnet run --project Sway.Demo -- --screenshot out.png --page forms --click 100,200 --key Tab --type "hello"

# Compare an incremental layout/restyle against a from-scratch one
dotnet run --project Sway.Demo -- --screenshot out.png --verify

# Measure per-phase frame cost (style, layout, paint) on the stress page
dotnet run --project Sway.Demo -- --bench 120 --page stress
dotnet run --project Sway.Demo -- --bench 120 --gpu   # render on a real GPU surface instead of CPU raster
```

Common `--screenshot` steps: `--move x,y`, `--click x,y`, `--wheel x,y,notches`, `--key [ctrl+][shift+]Name`,
`--type text`, `--advance ms` (advances the manual animation clock), `--css file` (adds another stylesheet), and
`--page name` to pick which demo page to mount. Steps run in order, and each one is captured as a separate frame
of the output PNG sequence.

This headless path is also how the project is checked without a browser-style test runner — see the note on testing
in [Known limits](./known-limits).

## Writing a component

Sway hosts ordinary Razor components. Markup, `@code`, two-way binding (`@bind`) and event handlers (`@onclick`,
`@oninput`, ...) all work the same way they do in Blazor:

```razor
<div class="card">
    <h2>Count: @count</h2>
    <p class="hint">Hover and press the buttons.</p>
    <button @onclick="() => count++">Increment</button>
    <button class="secondary" @onclick="() => count = 0" disabled="@(count == 0)">Reset</button>
</div>

@code {
    int count;
}
```

Style it with a plain CSS file added via `AddStylesheet` — see [CSS support](./css-support) for what's implemented.
Not every Blazor feature is available in this host (no JS interop, no routing); check
[Blazor hosting](./blazor-hosting) before relying on a specific API.
