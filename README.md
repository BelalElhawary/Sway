# Sway

Flutter-style widgets for .NET, rendered natively with SkiaSharp and Silk.NET. You build UI from immutable widgets,
keep mutable state in `State` objects and call `SetState` to rebuild. Layout uses box constraints, theming is Material 3
by default, and there is no browser, WebView, HTML, CSS or XAML involved.

```csharp
using Sway.Widgets;

App.Run(new MaterialApp(home: new CounterPage()), "Counter", 800, 600);

class CounterPage : StatefulWidget
{
    public override State CreateState() => new CounterState();
}

class CounterState : State<CounterPage>
{
    int _count;

    public override Widget Build(BuildContext context) => new Scaffold(
        appBar: new AppBar(title: new Text("Counter")),
        body: new Center(new FilledButton(
            new Text($"Clicked {_count} times"),
            () => SetState(() => _count++))));
}
```

## Features

- **Flutter's model**: `Widget` -> `Element` -> `RenderObject`, `StatelessWidget`, `StatefulWidget`, `BuildContext`,
  `BoxConstraints` layout with incremental relayout, and Flutter's vocabulary (`Row`, `Column`, `Stack`,
  `MainAxisAlignment`, `EdgeInsets`, ...).
- **Material 3**: `MaterialApp`, `Scaffold`, `ColorScheme` from a seed colour, light/dark, `TextTheme`, and the
  common components (buttons, switches, sliders, dialogs, menus, snack bars, ...).
- **Layout**: flex, `Stack`, `Wrap`, `Grid`, lazy `ListView.Builder` and `GridView.Builder`, scrolling with physics,
  `LayoutBuilder`.
- **Animation**: `AnimationController`, curves, tweens and implicit `Animated*` widgets.
- **Text**: HarfBuzz shaping, bidirectional text and right-to-left layout, rich text and a full text editor
  (selection, clipboard, undo).
- **Input**: gestures, hover, focus traversal and keyboard handling.
- **Painting effects**: shadows, gradients, transforms, clipping and backdrop filters.
- **Media**: optional video and audio playback through LibVLC (`Sway.Media`).
- **Headless rendering**: render any widget tree to a PNG on the CPU, with scripted input. This is used by the tests
  and the demo's screenshot mode.

## Projects

| Project | Purpose |
| --- | --- |
| `Sway.Widgets` | The platform-neutral library: Foundation, Rendering and Widgets. Depends only on SkiaSharp and HarfBuzz. |
| `Sway.Platform.Android` | Android host: GL surface, touch, soft keyboard, system theme. Subclass `SwayActivity`. |
| `Sway.Platform.Desktop` | Windows/Linux/macOS host: window, input, OS theme (Silk.NET). Provides `App.Run`. |
| `Sway.Media` | Optional `MediaPlayerController` and `VideoPlayer` widget (LibVLCSharp). |
| `Sway.Example` | The shared example UI (Material 3, pages for components, layout, forms, motion, effects, RTL, stress and media). No platform code. |
| `Sway.Example.Desktop` | Runs the example on desktop; also the `--screenshot` and `--bench` tooling. |
| `Sway.Example.Android` | Runs the example on Android. |
| `Sway.Widgets.Tests` | xUnit tests that run the widget tree headlessly. |
| `website/` | Documentation site (Docusaurus). |

An app references `Sway.Widgets` and a platform project. Other platforms add their own
`Sway.Platform.*` project that feeds the same `WidgetsBinding`.

## Getting started

Requires the .NET 10 SDK. Windows is the tested platform. The live window needs OpenGL 3.3; headless rendering needs
no GPU.

```bash
dotnet build Sway.slnx
dotnet test                                    # headless widget tests
dotnet run --project Sway.Example.Desktop         # the demo app
```

### Headless screenshots

```bash
dotnet run --project Sway.Example.Desktop -- --screenshot out.png --page forms --dark
dotnet run --project Sway.Example.Desktop -- --screenshot out.png --page forms --click 300,230 --type "hello" --key Tab
dotnet run --project Sway.Example.Desktop -- --bench --page stress
```

Pages: `components`, `layout`, `forms`, `motion`, `effects`, `rtl`, `stress`, `media`. Options: `--dark`, `--rtl`,
`--size WxH`. Scripted steps: `--move`, `--click`, `--wheel`, `--type`, `--key`, `--advance`.

## Documentation

The docs live in [website/docs](website/docs): [getting started](website/docs/getting-started.md),
[architecture](website/docs/architecture.md), [widgets](website/docs/widgets.md),
[input](website/docs/input.md), [animation](website/docs/animation.md) and [theming](website/docs/theming.md).
To browse them as a site, run `npm install && npm start` inside `website/`.

Known gaps and approximations are tracked in [LIMITS.md](LIMITS.md).

## Migrating from the Blazor/CSS version

Earlier versions of Sway were a Blazor component model with a DOM and a CSS engine (`Sway.Core`, `Sway.Demo`). That
engine has been removed. The widget system in `Sway.Widgets` is now the only API: there are no `.razor` components,
no stylesheets and no DOM. Styling is done with typed widget properties and `Theme`, and layout uses Flutter's
widgets instead of CSS flex and grid terms.

## License

MIT, see [LICENSE.txt](LICENSE.txt).
