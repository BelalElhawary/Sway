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
| No automated tests | **Unsupported.** Behaviour is checked by rendering headless PNGs (`--screenshot`) and reading them, plus the `--bench` frame-cost report. Constraint layout (`RenderFlex`, `RenderGrid`, `RenderWrap`), `TextEditState`, `Lerps`, `Curves` and the gesture arena are pure logic and good candidates for unit tests. |
| Only tested on Windows | The font fallback list assumes Roboto or Segoe UI. Other platforms need a font mapping. The OS dark-mode probe is Windows-only. |
| Live window lightly exercised | The windowed host (Silk.NET input, clipboard, cursors, high-DPI scaling) was verified headlessly and by a few launches, not by sustained hands-on use. |
| OpenGL 3.3 core required | Skia renders through a GL context. There is no software fallback for the live window (the headless path is CPU raster). |
| Single window | One window per process. |
| The whole tree repaints every frame | Layout is incremental (only the dirty chain is re-laid-out and clean subtrees are cached), but painting walks the whole tree each frame. There are no repaint boundaries, layer caching or damage rectangles. Frames are only produced when something changed, a timer is due, or an animation is running. |
| Cold start | The first frame of a 1500-row demo page takes about 150 ms. It has not been optimised. |
| Frame cost on big non-lazy trees | `Wrap`, `Grid` and `Column` lay out and paint all children. Use `ListView.Builder` for large collections. |
| Deprecated Skia APIs | `SKPath` construction calls (`MoveTo`, `AddRect`, ...) are marked obsolete in SkiaSharp 4 in favour of `SKPathBuilder`; they still work and produce build warnings. |

## 2. Layout

| Limit | Notes |
| --- | --- |
| `ListView.Builder` needs equal-height items | **Planned.** Pass `itemExtent`, or the first item's size is used for all. Variable heights need a sliver system (`CustomScrollView`, `SliverList`). |
| `Grid` is not lazy | **Planned.** It lays out every child. Tracks support px, fr and auto only: no `minmax`, `fit-content`, named lines or areas, and no dense auto-flow. Spanning items grow auto tracks evenly. |
| `Wrap` has no intrinsic height | It cannot be used inside `IntrinsicHeight` with wrapping content. |
| No `LayoutBuilder`, `OverflowBox` or `CustomMultiChildLayout` | **Planned.** Build-during-layout exists only inside the lazy list. |
| No overflow indicators | A `Row` or `Column` whose children are larger than the available space simply overflows (clipped only by an ancestor clip). |
| Transforms do not affect layout | `Transform` paints and hit-tests correctly but its box keeps the untransformed size. |
| `AnimatedSize` drives layout from frame callbacks | A size animation relays out its subtree every frame. |

## 3. Text and input

| Limit | Notes |
| --- | --- |
| Caret and selection in mixed-direction text | **Approximation.** Positions come from measuring string prefixes, so they are only exact for single-direction lines. Pure RTL lines are mirrored correctly; a line mixing Arabic and Latin can place the caret slightly off at run boundaries. |
| Bidi runs use a simplified algorithm | Direction runs are found with a simple strong-direction scan, not the full Unicode Bidirectional Algorithm (no embeddings, isolates or mirrored brackets). |
| No emoji or per-character font fallback | **Unsupported.** A glyph missing from the chosen font draws as a box. |
| No IME or dead-key composition | **Unsupported.** Text arrives as plain characters. |
| Text is not selectable outside fields | There is no `SelectableText`. |
| No accessibility tree | **Unsupported.** |
| Gestures: no long-press, double-tap, scale or multi-pointer | **Planned.** Tap and drag (vertical, horizontal, pan) compete in a gesture arena. Only the left mouse button is dispatched; there is no touch or pen input. |
| Scrollbars are indicators only | **Planned.** They fade after scrolling and cannot be dragged or clicked. Wheel scrolling is a fixed 100 px per notch with no smooth scrolling or overscroll. |
| Tab traversal follows tree order | There is no `FocusTraversalGroup` or custom ordering, and focus does not scroll into view. |
| Floating label is single-line | Multi-line fields keep the label at the top-left; the notch is sized from the unwrapped label width. |

## 4. Animation

| Limit | Notes |
| --- | --- |
| No physics simulations | **Planned.** `SpringSimulation`, `FrictionSimulation` and `AnimationController.Fling` are absent. Scroll fling uses its own exponential decay. |
| Animations keep ticking while offscreen | **Unsupported.** There is no `TickerMode` or muting, so a hidden repeating animation still requests a frame every tick and keeps the window awake. |
| No route, `Hero` or shared-element transitions | **Unsupported.** There is no `Navigator`; use `AnimatedSwitcher` for page changes. |
| Implicit alignment is physical | `AnimatedContainer` and `AnimatedAlign` take `Alignment`, not `AlignmentDirectional`. Resolve start/end yourself in RTL layouts. |
| Missing-to-present properties jump | **Approximation.** An `AnimatedContainer` property that goes from `null` to a value (or back) changes immediately instead of animating in. Colours and shadows inside a decoration do fade in. |
| Gradients | Blend only between gradients of the same kind with equal stop counts and no explicit stops; otherwise they switch at the halfway point. |
| `TweenAnimationBuilder` evaluation | It reuses your tween instance to blend, temporarily setting its `Begin` and `End`. Do not share one tween object between two builders. |
| No `AnimatedList`, `AnimatedPhysicalModel`, `AnimatedFractionallySizedBox` | **Planned** as needed. |

## 5. Theming and Material 3

| Limit | Notes |
| --- | --- |
| `ColorScheme.FromSeed` approximates HCT | **Approximation.** Tonal palettes are generated in CIE L\*C\*h, not CAM16, so seeded colours are close to but not identical to Flutter's. The default light and dark schemes use the exact published M3 baseline values. |
| No ripple or ink splash | **Unsupported.** Interaction uses M3 state layers (hover, focus, press tints) only. |
| System light/dark is read once | **Planned.** The host reads the Windows `AppsUseLightTheme` setting at start-up and does not follow later changes. Other platforms report light. Set `WidgetsBinding.PlatformBrightness` to override. |
| Roboto is not bundled | Text uses Roboto if installed and falls back to Segoe UI, so metrics differ slightly from the M3 spec. |
| Icons are a small built-in set | **Planned.** About twenty Material icons as path data (`Icons`); there is no icon font. Any SVG path on a 24x24 grid works through `IconData`. |
| Missing M3 components | `Slider`, `TabBar`, `Drawer`, `BottomSheet`, `Tooltip`, `DatePicker`, `SearchBar`, `SegmentedButton`, `Badge`, `NavigationDrawer`, and menus (`MenuAnchor`, `PopupMenuButton`) are not implemented. |
| Dialogs and snack bars are overlay-based | There is no `Navigator`, so dialogs do not trap focus, Escape does not close them, and snack bars are not queued. |
| `BackdropFilter` | **Unsupported.** `ImageFiltered` blurs the child itself, not what is behind it. |
| No dynamic colour or `ThemeExtension`s | **Unsupported.** Component themes (`ButtonTheme`, `CardTheme`, ...) are not separate objects; restyle by wrapping or composing widgets. |

---

## Where to look in the code

| Area | Files in `Sway.Widgets` |
| --- | --- |
| Widget, Element, State, inherited and parent-data widgets | `Foundation/Widget.cs` |
| Constraints, geometry, decorations | `Foundation/Geometry.cs`, `Foundation/Painting.cs` |
| Render objects and layout | `Rendering/RenderObject.cs`, `RenderBox.cs`, `RenderShifted.cs`, `RenderFlex.cs`, `RenderLayouts.cs` |
| Text shaping, wrapping and editing | `Rendering/TextShaper.cs`, `Bidi.cs`, `FontCache.cs`, `RenderParagraph.cs`, `RenderEditable.cs`, `Foundation/TextEditState.cs` |
| Frames, clock, timers, pointer routing | `Widgets/Binding.cs`, `Widgets/Gestures.cs`, `Platform/App.cs` |
| Focus and keyboard | `Widgets/Focus.cs`, `Platform/KeyMap.cs` |
| Scrolling and lazy lists | `Widgets/Scrolling.cs` |
| Animation | `Foundation/Animation.cs`, `Curves.cs`, `Lerp.cs`, `Widgets/Animated.cs` |
| Theme, colour scheme, M3 components | `Foundation/ColorScheme.cs`, `Widgets/Theme.cs`, `Widgets/Controls.cs`, `Widgets/TextField.cs` |
| Layout widgets | `Widgets/Basic.cs`, `Widgets/Layouts.cs`, `Widgets/Overlay.cs` |
