---
sidebar_position: 8
---

Everything below is a gap or approximation in the current widget system, kept in one place so nothing is discovered
by surprise. Update it when a limit is fixed or a new one is found. This page mirrors
[`LIMITS.md`](https://github.com/BelalElhawary/Sway/blob/master/LIMITS.md) in the repository root; update both together.

Tags used:

- **Planned**: intended work.
- **Approximation**: implemented, but deliberately simpler than the reference behaviour.
- **Unsupported**: not implemented and not currently scheduled.

---

## 1. Platform, verification and performance

| Limit | Notes |
| --- | --- |
| Limited automated tests | **Planned.** `Sway.Widgets.Tests`, `Sway.Extras.Material3.Tests` and `Sway.Extras.Ibm.Tests` (xunit, `dotnet test`) drive a headless app on a manual clock and cover curves, lerps, physics, text editing, flex layout, gestures, scrolling, lists and grids, animation, focus, the Material 3 components (dialogs, menus, sheets, the date picker, themes), the Carbon components and data table, and the backdrop filter. Rendering is still checked by headless PNGs (`--screenshot`) and the `--bench` report. `RenderGrid`, `RenderWrap`, text wrapping and bidi have no direct tests yet. |
| Only tested on Windows | The font fallback list assumes Roboto or Segoe UI, so other platforms need a font mapping. The macOS and Linux (GNOME) dark-mode probes, which run a helper process every 5 seconds, are written but untested. |
| Live window lightly exercised | The windowed host (Silk.NET input, clipboard, cursors, high-DPI scaling) was verified headlessly and by a few launches, not by sustained hands-on use. |
| GL context required | Skia renders through a GL context: OpenGL 3.3 core on desktop, a GL surface on Android, WebGL in the browser. There is no software fallback for the live window (the headless path is CPU raster). |
| Media playback is basic | **Approximation.** `Sway.Media` is platform-neutral (`MediaPlayerController`, `VideoSurface`, `MediaBuilder`; the Material 3 `VideoPlayer` and `AudioPlayer` are in `Sway.Extras.Material3.Media`) and plays through an `IMediaBackend` each host installs: `Sway.Media.LibVlc` (LibVLC) on Windows, Linux, macOS and Android, `Sway.Media.Web` (an HTML media element) in the browser. Video frames are copied to a CPU bitmap and drawn each frame; there is no GPU texture path, no subtitle or audio-track selection, no fullscreen of its own (`VideoPlayer.onFullscreen` only calls you) and no playlist. LibVLC bundles its binaries for Windows and Android (NuGet) and for macOS when built on a Mac; Linux needs the system VLC packages. macOS, Linux and the browser backend are untested. In the browser, formats are whatever the browser decodes, and a cross-origin URL needs CORS headers to show its picture (the sound plays regardless). |
| File pickers use the platform dialogs | **Approximation.** `FilePicker` shows the native open-file and open-folder dialogs: the Windows item dialog (tested), `zenity` or `kdialog` on Linux, `osascript` on macOS, the Storage Access Framework on Android and a file input in the browser. Android and the browser give `content://` and `blob:` sources instead of paths, and the browser lists a picked folder's files from the browser's own flat list. There is no save dialog. Linux, macOS, Android and web are untested. |
| Single window | One window per process. |
| Android and web hosts are minimal | `Sway.Platform.Android` and `Sway.Platform.Web` feed the same `WidgetsBinding` as the desktop host but are newer and less exercised than it. Input on both is a single pointer (see Text and input). The web host needs the `wasm-tools` workload and has no soft keyboard on touch devices. |
| The whole tree repaints every frame | Layout is incremental (only the dirty chain is re-laid-out and clean subtrees are cached), but painting walks the whole tree each frame. There are no repaint boundaries, layer caching or damage rectangles. Frames are only produced when something changed, a timer is due, or an animation is running. |
| Cold start | The first frame of a 1500-row demo page takes about 150 ms. It has not been optimised. |
| Frame cost on big non-lazy trees | `Wrap`, `Grid` and `Column` lay out and paint all children. Use `ListView.Builder` for large collections. |
| Deprecated Skia APIs | `SKPath` construction calls (`MoveTo`, `AddRect`, ...) are marked obsolete in SkiaSharp 4 in favour of `SKPathBuilder`; they still work and produce build warnings. |

## 2. Layout

| Limit | Notes |
| --- | --- |
| `ListView.Builder` estimates variable heights | **Approximation.** Pass `itemExtent` for equal-height items (exact and fastest). Otherwise items are measured as they scroll into view and unmeasured ones are estimated from the average, so the scrollbar thumb and `MaxScrollExtent` shift as measurements arrive; visible content is re-anchored so it does not jump. Jumping to the very end can take a few frames to settle. There is no sliver system (`CustomScrollView`, `SliverList`), so a list cannot mix with other scrolling children. |
| `Grid` is not lazy | **Approximation.** `Grid` lays out every child; use `GridView.Builder` (equal cells, fixed column count or minimum column width, no spans) for large collections. `Grid` tracks support px, fr and auto only: no `minmax`, `fit-content`, named lines or areas, and no dense auto-flow. Spanning items grow auto tracks evenly. |
| `Wrap` has no intrinsic height | It cannot be used inside `IntrinsicHeight` with wrapping content. |
| `LayoutBuilder` has no intrinsic size | It builds its child during layout, so it cannot be measured ahead of layout and reports 0 to `IntrinsicWidth`/`IntrinsicHeight`. `CustomMultiChildLayout` delegates cannot supply intrinsic sizes either. |
| Overflow indicators are debug-only | **Approximation.** A `Row` or `Column` whose children exceed its size reports `RenderFlex.OverflowExtent` and, in debug builds, paints a yellow and black band on the overflowing edge (`RenderFlex.PaintOverflowIndicators`). There is no text label, and `Stack`, `Wrap` and `Grid` do not report overflow. |
| Transforms do not affect layout | `Transform` paints and hit-tests correctly but its box keeps the untransformed size. |
| `AnimatedSize` drives layout from frame callbacks | A size animation relays out its subtree every frame. |

## 3. Text and input

| Limit | Notes |
| --- | --- |
| Caret and selection in mixed-direction text | **Approximation.** Positions come from measuring string prefixes, so they are only exact for single-direction lines. Pure RTL lines are mirrored correctly; a line mixing Arabic and Latin can place the caret slightly off at run boundaries. |
| Bidi runs use a simplified algorithm | Direction runs are found with a simple strong-direction scan, not the full Unicode Bidirectional Algorithm (no embeddings, isolates or mirrored brackets). |
| No emoji or per-character font fallback | **Unsupported.** A glyph missing from the chosen font draws as a box. |
| No IME or dead-key composition | **Unsupported.** Text arrives as plain characters. |
| Selectable text is a read-only field | `SelectableText` wraps a read-only editor, so it selects with mouse and keyboard but has no rich spans and no selection across separate widgets. |
| No accessibility tree | **Unsupported.** |
| Gestures: no scale or multi-pointer | **Planned.** Tap, double-tap, long-press and drag (vertical, horizontal, pan) compete in a gesture arena. A single tap waits 300 ms when a double-tap handler is present. Every host feeds one pointer: the left mouse button on desktop, web and headless, and a single touch on Android (touch is mapped onto the same pointer, so extra fingers, pen input and pointer kinds are ignored). Scale and multi-pointer gestures therefore have no input to work from. |
| No overscroll | **Approximation.** Scrollbars fade after scrolling, grow when hovered, and can be dragged or clicked to page. The wheel scrolls 100 px per notch with a short eased animation. There is no overscroll bounce or glow, and the scrollbar gutter (14 px) is not hit-testable for the content under it. |
| Tab traversal has no groups or policies | Tab follows tree order unless `FocusTraversalOrder` sets a position. `Focus(trapFocus: true)` confines Tab to a subtree, and keyboard focus scrolls enclosing scrollables to reveal the widget. There is no `FocusTraversalGroup` or directional (arrow key) traversal. |
| Floating label is single-line | Multi-line fields keep the label at the top-left; the notch is sized from the unwrapped label width. |

## 4. Animation

| Limit | Notes |
| --- | --- |
| Physics are 1-D only | **Approximation.** `SpringSimulation`, `FrictionSimulation`, `AnimationController.Fling` and `AnimationController.AnimateWith` exist. There is no `GravitySimulation`, `BouncingScrollSimulation` or clamped composite simulation. |
| Offscreen animations are only paused on request | **Approximation.** Wrap hidden content in `Offstage` or `TickerMode(false, ...)` and its animations pause and stop requesting frames. Nothing detects scrolled-out or covered content automatically. |
| No route, `Hero` or shared-element transitions | **Unsupported.** There is no `Navigator`; use `AnimatedSwitcher` for page changes. |
| Implicit scale/rotation origins are physical | `AnimatedAlign`, `AnimatedContainer` and `AnimatedPositionedDirectional` resolve start/end from the text direction, but the `alignment` origin of `AnimatedScale` and `AnimatedRotation` is still a physical `Alignment`. |
| Missing-to-present properties jump | **Approximation.** An `AnimatedContainer` property that goes from `null` to a value (or back) changes immediately instead of animating in. Colours and shadows inside a decoration do fade in. |
| Gradients | Linear, radial and sweep gradients blend with each other's kind, resampling ramps of different lengths onto shared stops. Gradients of different kinds switch at the halfway point. |
| `TweenAnimationBuilder` evaluation | It only uses your tween's blend function, so one tween object can back several builders. |
| No `AnimatedPhysicalModel` shadow shape animation | **Approximation.** `AnimatedList`, `AnimatedPhysicalModel`, `AnimatedFractionallySizedBox` and `SizeTransition` exist. Fractional elevations blend between the two neighbouring Material levels. |

## 5. Theming and Material 3 (`Sway.Extras.Material3`)

| Limit | Notes |
| --- | --- |
| `ColorScheme.FromSeed` approximates HCT | **Approximation.** Tonal palettes are generated in CIE L\*C\*h, not CAM16, so seeded colours are close to but not identical to Flutter's. The default light and dark schemes use the exact published M3 baseline values. |
| No ripple or ink splash | **Unsupported.** Interaction uses M3 state layers (hover, focus, press tints) only. |
| System light/dark is polled | **Approximation.** The desktop host re-reads the Windows `AppsUseLightTheme` setting about once a second and when the window regains focus; macOS and Linux probes are untested. Android re-reads the system theme when the activity resumes, and the web host follows the browser's `prefers-color-scheme`. Set `WidgetsBinding.PlatformBrightness` to override. |
| Roboto is not bundled | Text uses Roboto if installed and falls back to Segoe UI, so metrics differ slightly from the M3 spec. |
| Icons are path data, not an icon font | **Approximation.** The full Material set (about 2,100 icons) ships as path data in `Sway.Assets`: filled as `Icons.*`, plus `Icons.Outlined.*`, `Icons.Round.*`, `Icons.Sharp.*` and `Icons.TwoTone.*`. There is no icon font. Any SVG path on a 24x24 grid works through `IconData`. |
| Missing M3 components | `RangeSlider`, `Stepper`, `ExpansionPanel`, `DataTable`, a time picker and a date-range picker are not implemented. `Slider`, `TabBar`, `Tooltip`, `Badge`, `SegmentedButton`, `SearchBar` (no suggestions view), menus (`PopupMenuButton`, `Menus.Show`), navigation drawer, bottom sheet and date picker exist. |
| Dialogs and snack bars are overlay-based | There is no `Navigator`. Dialogs trap focus, close on Escape and restore focus, and snack bars queue one at a time, but dialogs do not take part in a back stack. |
| `BackdropFilter` blurs a rectangle | `BackdropFilter` blurs or recolours what is behind its bounds; it cannot clip to a rounded shape unless wrapped in a clip. |
| No component theme objects | **Approximation.** `ThemeExtension` supports app-defined theme data and `ThemeData.FromSystemAccent` seeds from the Windows accent colour (other platforms use the default). Component themes (`ButtonTheme`, `CardTheme`, ...) are not separate objects; restyle by wrapping or composing widgets. |

---

## Where to look in the code

| Area | Files in `Sway.Widgets` |
| --- | --- |
| Widget, Element, State, inherited and parent-data widgets | `Foundation/Widget.cs` |
| Constraints, geometry, decorations | `Foundation/Geometry.cs`, `Foundation/Painting.cs` |
| Render objects and layout | `Rendering/RenderObject.cs`, `RenderBox.cs`, `RenderShifted.cs`, `RenderFlex.cs`, `RenderLayouts.cs` |
| Text shaping, wrapping and editing | `Rendering/TextShaper.cs`, `Bidi.cs`, `FontCache.cs`, `RenderParagraph.cs`, `RenderEditable.cs`, `Foundation/TextEditState.cs` |
| Frames, clock, timers, pointer routing | `Widgets/Binding.cs`, `Widgets/Gestures.cs`, `Sway.Platform.Desktop/App.cs` |
| Focus and keyboard | `Widgets/Focus.cs`, `Sway.Platform.Desktop/KeyMap.cs` |
| Scrolling and lazy lists | `Widgets/Scrolling.cs` |
| Animation | `Foundation/Animation.cs`, `Curves.cs`, `Lerp.cs`, `Widgets/Animated.cs` |
| Theme, colour scheme, M3 components | `Foundation/ColorScheme.cs`, `Widgets/Theme.cs`, `Widgets/Controls.cs`, `Widgets/TextField.cs` |
| Layout widgets | `Widgets/Basic.cs`, `Widgets/Layouts.cs`, `Widgets/Overlay.cs` |
