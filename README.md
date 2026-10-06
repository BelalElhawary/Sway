# Sway

Flutter-style widgets for .NET, rendered natively with SkiaSharp and Silk.NET. You build UI from immutable widgets,
keep mutable state in `State` objects and call `SetState` to rebuild. Layout uses box constraints, and there is no
browser, WebView, HTML, CSS or XAML involved. The core library has no look of its own: design systems are optional
packages (`Sway.Extras.Material3`, `Sway.Extras.Ibm`, `Sway.Extras.Shopify`).

```csharp
using Sway.Extras.Material3;
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
- **Design systems as packages**: `Sway.Extras.Material3` (`MaterialApp`, `Scaffold`, `ColorScheme` from a seed colour,
  light/dark, `TextTheme`, buttons, switches, sliders, dialogs, menus, snack bars, ...) and `Sway.Extras.Ibm` (IBM
  Carbon: its own theme tokens, IBM Plex, buttons, fields, dropdown, tags, `DataTable<T>`). The core knows about neither.
- **Layout**: flex, `Stack`, `Wrap`, `Grid`, lazy `ListView.Builder` and `GridView.Builder`, scrolling with physics,
  `LayoutBuilder`.
- **Animation**: `AnimationController`, curves, tweens and implicit `Animated*` widgets.
- **Text**: HarfBuzz shaping, bidirectional text and right-to-left layout, rich text and a full text editor
  (selection, clipboard, undo).
- **Localization**: `Locale`, `Localizations` and `LocalizationsDelegate<T>` in the core; `MaterialApp(locale: ...)` and
  `CarbonApp(locale: ...)` translate the built-in strings (English, Arabic, Spanish, French, German), format dates with the
  locale's culture and week start, and switch to right-to-left for RTL languages. Add your own resources with
  `localizationsDelegates` and read them with `Localizations.Of<T>(context)`.
- **Input**: gestures, hover, focus traversal and keyboard handling.
- **Painting effects**: shadows, gradients, transforms, clipping and backdrop filters.
- **Media**: a `MediaPlayerController` and `VideoSurface` in the core media package, Material 3 `VideoPlayer` and `AudioPlayer` widgets with controls in `Sway.Extras.Material3.Media`, on LibVLC (Windows, Linux, macOS, Android) or an HTML media element (browser).
- **File pickers**: native open-file and open-folder dialogs on every host (`FilePicker`).
- **Headless rendering**: render any widget tree to a PNG on the CPU, with scripted input. This is used by the tests
  and the demo's screenshot mode.

## Projects

| Project | Purpose |
| --- | --- |
| `Sway.Widgets` | The platform-neutral, design-neutral library: Foundation, Rendering and Widgets (layout, text, gestures, focus, scrolling, animation, `EditableText`, `Icon`, `Image`, `Interactive`). Depends only on SkiaSharp and HarfBuzz. |
| `Sway.Platform.Android` | Android host: GL surface, touch, soft keyboard, system theme. Subclass `SwayActivity`. |
| `Sway.Platform.Web` | Browser host: Blazor WebAssembly component (`SwayView`) on `SkiaSharp.Views.Blazor` (WebGL), pointer, keyboard, clipboard and theme. |
| `Sway.Platform.Headless` | Windowless host: renders to a PNG with a software surface (`Headless.Screenshot`). |
| `Sway.Platform.Desktop` | Windows/Linux/macOS host: window, input, OS theme (Silk.NET). Provides `App.Run`. |
| `Sway.Media` | Platform- and design-neutral media: `MediaPlayerController`, `VideoSurface`, `MediaBuilder`, and the `IMediaBackend` interface. |
| `Sway.Media.LibVlc` | The LibVLC backend for Windows, Linux, macOS and Android (`LibVlcMediaBackend.Install()`). |
| `Sway.Media.Web` | The browser backend (`await BrowserMediaBackend.InstallAsync()`). |
| `Sway.Extras.Material3` | Optional: Material 3. `MaterialApp`, `ThemeData`, `ColorScheme`, `TextTheme`, and the components (buttons, fields, switches, sliders, dialogs, menus, navigation, ...). |
| `Sway.Extras.Material3.Media` | Optional: the Material 3 `VideoPlayer`, `AudioPlayer` and their controls. Needs `Sway.Extras.Material3` and `Sway.Media`. |
| `Sway.Extras.Ibm` | Optional: IBM Carbon. `CarbonApp`, `CarbonThemeData` (White, Gray 10, Gray 90, Gray 100), embedded IBM Plex fonts, `CarbonButton`, `CarbonTextInput`, `CarbonDropdown`, `CarbonCheckbox`, `CarbonTag`, `DataTable<T>`, `Pagination`. Does not use Material 3. |
| `Sway.Extras.Shopify` | Optional: commerce components in the Shopify design language, built for phones first (44px touch targets, 2/3/4-column reflow, bottom sheet and bottom nav). `ShopifyApp`, light (cream) and dark (cinematic) themes, `ShopifyButton`, `ShopifyTextField`, `ShopifySearchField`, `ShopifyProductCard`, `ShopifyProductGrid`, `ShopifyPrice`, `ShopifyRating`, `ShopifyQuantityStepper`, `ShopifyOptionChips`, `ShopifySwatchPicker`, `ShopifyCartLine`, `ShopifyOrderSummary`, `ShopifyFreeShippingBar`, `ShopifyCheckoutSteps`, `ShopifyHeader`, `ShopifyBottomNavBar`, `ShopifyHero`, `ShopifyAccordion`, `ShopifySheet`. Bundles Inter and Inter Display (OFL) as the open stand-in for Neue Haas Grotesk; an installed Neue Haas is used if present. The aloe and pistachio greens exist only on the light theme. |
| `Sway.Example` | The shared example UI (a Material 3 app with pages for components, layout, forms, motion, effects, RTL, stress, media, icons and an IBM Carbon page and a Shopify-style storefront). No platform code. |
| `Sway.Example.Desktop` | Runs the example in a desktop window. |
| `Sway.Example.Headless` | Runs the example without a window: the `--screenshot` and `--bench` tooling. |
| `Sway.Example.Android` | Runs the example on Android. |
| `Sway.Example.Web` | Runs the example in the browser (Blazor WebAssembly). |
| `Sway.Widgets.Tests` | xUnit tests for the core library, run headlessly. |
| `Sway.Extras.Material3.Tests` | Tests for the Material 3 components. |
| `Sway.Extras.Ibm.Tests` | Tests for the Carbon components and data table. |
| `Sway.Extras.Shopify.Tests` | Tests for the commerce components, including phone-width overflow and touch-target checks. |
| `website/` | Documentation site (Docusaurus). |

An app references `Sway.Widgets`, a platform project and the design system it wants (`Sway.Extras.Material3`, `Sway.Extras.Ibm`, or its own). Other platforms add their own
`Sway.Platform.*` project that feeds the same `WidgetsBinding`.

## Getting started

Requires the .NET 10 SDK. Windows is the tested platform. The live window needs OpenGL 3.3; headless rendering needs
no GPU.

```bash
dotnet build Sway.slnx
dotnet test                                    # headless widget tests
dotnet run --project Sway.Example.Desktop         # the demo app
```

### Web

The browser host needs the `wasm-tools` workload (`dotnet workload install wasm-tools`), which relinks the .NET
runtime with SkiaSharp's native code.

```bash
dotnet run --project Sway.Example.Web
```

Host it in your own Blazor WebAssembly app with `<SwayView Root="new MyApp()" />`. Not yet supported on the web:
the soft keyboard on touch devices.

### Headless screenshots

```bash
dotnet run --project Sway.Example.Headless -- --screenshot out.png --page forms --dark
dotnet run --project Sway.Example.Headless -- --screenshot out.png --page forms --click 300,230 --type "hello" --key Tab
dotnet run --project Sway.Example.Headless -- --bench --page stress
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
