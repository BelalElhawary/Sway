# Sway

Flutter-style widgets for .NET, rendered natively with SkiaSharp and Silk.NET. Immutable widgets, `State` with
`SetState`, box-constraint layout, Material 3 theming (the default), animation, rich text with right-to-left
support, gestures, focus and a full text editor. There is no browser, WebView or XAML.

```csharp
App.Run(new MaterialApp(home: new CounterPage()), "Counter", 800, 600);
```

```bash
dotnet run --project Sway.Widgets.Demo                                   # the Material 3 demo app
dotnet run --project Sway.Widgets.Demo -- --screenshot out.png --page forms --dark   # headless PNG
```

- `Sway.Widgets/`: the library (Foundation, Rendering, Widgets, Platform).
- `Sway.Widgets.Demo/`: demo pages for components, layout, forms, motion, effects, RTL and stress.
- `website/`: documentation (Docusaurus). Known gaps are tracked in [LIMITS.md](LIMITS.md).

Requires the .NET 10 SDK; tested on Windows.
