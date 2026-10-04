# Sway known limits

Everything below is a gap or approximation as of the end of step 6 (effects, motion and performance). It is kept in
one place so nothing is discovered by surprise. Update it when a limit is fixed or a new one is found.

Tags used:

- **Planned**: intended work, with the step where it is expected.
- **Approximation**: implemented, but deliberately simpler than the spec.
- **Unsupported**: not implemented and not currently scheduled.

---

## Sway.Widgets (Flutter-style rewrite, in progress)

The sections below this one describe the legacy Blazor/CSS engine (`Sway.Core`), which will be removed in phase 6.
This section tracks the new `Sway.Widgets` project and is updated per phase.

### Text and input (phase 3)

| Limit | Notes |
| --- | --- |
| Caret and selection in mixed-direction text | **Approximation.** Positions come from measuring string prefixes, so they are only exact for single-direction lines. Pure RTL lines are mirrored correctly; a line mixing Arabic and Latin can place the caret slightly off at run boundaries. |
| No emoji or per-character font fallback | **Unsupported.** A glyph missing from the chosen font draws as a box. |
| `ListView.Builder` needs equal-height items | **Planned.** Pass `itemExtent`, or the first item's size is used for all. Variable heights need a sliver system (`CustomScrollView`, `SliverList`). |
| No `LayoutBuilder` | **Planned.** Build-during-layout exists only inside the lazy list. |
| Gestures: no long-press, double-tap, scale or multi-pointer | **Planned.** Tap and drag (vertical, horizontal, pan) compete in a gesture arena. |
| Scrollbars are indicators only | **Planned.** They fade after scrolling and cannot be dragged or clicked. Wheel scrolling is a fixed 100 px per notch with no smooth scrolling. |
| Controls use a fixed palette | **Planned (phase 5).** `Theme`/`ThemeData` will replace the hard-coded colours. |
| Windowed host lightly exercised | Mouse, keyboard, clipboard and cursor paths through Silk.NET were ported from the old host and verified headlessly only. |
| No IME or dead-key composition | **Unsupported.** Text arrives as plain characters. |
| Tab traversal follows tree order | There is no `FocusTraversalGroup` or custom ordering, and focus does not scroll into view. |

### Animation (phase 4)

| Limit | Notes |
| --- | --- |
| No physics simulations | **Planned.** `SpringSimulation`, `FrictionSimulation` and `AnimationController.Fling` are absent. Scroll fling uses its own exponential decay, and `AnimationController` only runs duration-and-curve animations. |
| Animations keep ticking while offscreen | **Unsupported.** There is no `TickerMode` or muting, so a hidden repeating animation still requests a frame every tick and keeps the window awake. |
| No route, `Hero` or shared-element transitions | **Unsupported.** There is no `Navigator` yet; use `AnimatedSwitcher` for page changes. |
| Implicit alignment is physical | `AnimatedContainer`/`AnimatedAlign` take `Alignment`, not `AlignmentDirectional`. Resolve start/end yourself in RTL layouts. |
| Missing-to-present properties jump | **Approximation.** An `AnimatedContainer` property that goes from `null` to a value (or back) changes immediately instead of animating in. Colours and shadows inside a decoration do fade in. |
| Gradients | Blend only between gradients of the same kind with equal stop counts and no explicit stops; otherwise they switch at the halfway point. |
| `TweenAnimationBuilder` evaluation | It reuses your tween instance to blend, temporarily setting its `Begin`/`End`. Do not share one tween object between two builders. |
| `AnimatedSize` drives layout from frame callbacks | A size animation relays out its subtree every frame. Avoid it around very large subtrees. |
| No `AnimatedList`, `AnimatedPhysicalModel`, `AnimatedFractionallySizedBox` or `Hero` | **Planned** as needed. |

---

## 1. Verification and platform

| Limit | Notes |
| --- | --- |
| No automated tests | **Unsupported.** Behaviour was checked by rendering headless PNGs (`--screenshot`) and reading them. Two self-checks exist: `--verify` compares the incremental layout and restyle against a from-scratch one after scripted interactions, and `--bench` reports per-phase frame cost. Neither is a test suite. Selector matching, the flex/grid algorithms, `TextEditState` and the animation maths are pure logic and good candidates for unit tests. |
| Only tested on Windows | The font fallback assumes Segoe UI, Times New Roman and Consolas. Other platforms need a font mapping table. |
| Live window only lightly exercised | The window was launched, captured from the screen once to confirm it draws, and its idle CPU use measured (about 1.6% of one core). Real mouse, keyboard, clipboard, cursor and multi-monitor DPI behaviour through Silk.NET have not been exercised by hand. |
| OpenGL 3.3 core required | Skia renders through a GL context from the window. There is no software fallback for the live window (the headless path is raster). |
| No OS dark-mode detection | `UiHost.ColorScheme` must be set by the host. `prefers-color-scheme` is matched against it. |
| Single window | One window per process. |

## 2. Rendering and performance

Measured on a 12,000-element page (1,500 rows in a scroller), Release build. Before step 6 a hover cost 253 ms,
a scroll step 157 ms, and a text change 209 ms. Now they cost about 2 to 4 ms (CPU raster) and the idle window
renders nothing.

| Limit | Notes |
| --- | --- |
| Cold start is slow on big trees | About 450 to 530 ms for the first frame of 12,000 elements (renderer, style, layout). Not optimised. |
| A layout-affecting change relays out its dirty chain | Clean subtrees are reused (translated, not recomputed), but everything from the changed element up to the root is laid out again, and flex or grid containers re-measure their items. There is no relayout boundary, so a change near the root of a huge tree costs more than a change in a leaf. |
| Subtrees with absolute or fixed descendants are never reused | Any element that contains an out-of-flow box is re-laid out whenever its parent is. The flag is sticky until the node is recreated. |
| Visual bounds are recomputed for the dirty chain only | Correct, but the pass is whole-tree in a worst case such as a viewport resize. |
| The display list is rebuilt for every rendered frame | It is culled to what is on screen (28,500 ops became 316 on the stress page), so the rebuild is cheap, but it is not retained between frames. |
| No layer caching or damage rectangles | **Measured and deliberately not built.** On a GPU surface the heaviest demo (blurs, shadows, filters, gradients) costs 3.2 ms per frame and the stress page 2 to 3.5 ms, far below a 16 ms budget. The headless CPU raster path is much slower for blurs and filters (about 35 ms for the Effects page), so a CPU-only host would benefit. |
| Any input forces one frame | Even a pointer move over nothing repaints once. Hover and scroll are cheap, but input is not coalesced. |
| Event-driven rendering needs state changes to flag themselves | Renders happen when style or layout is dirty, an animation is running, input arrived, the caret blinks, or the window asks. A host that mutates the document without going through the node API would not wake it. |
| Per-pass intrinsic caches are discarded every layout | Widths and natural heights are recomputed for dirty subtrees each pass. |
| Coarse locking | One document lock guards tree mutation, style, layout, paint and input. |
| State-dependent restyling falls back to broad invalidation for `:not(:hover)` and `:is(:hover)` | Plain `:hover`, `:active`, `:focus` and `:focus-visible` are tracked per selector and restyle only the elements a rule can affect. |
| Text uses plain Skia metrics | **Planned.** No HarfBuzz shaping, so kerning, ligatures, complex scripts and bidi (Arabic, Hebrew) are approximate. Caret x positions are measured on string prefixes. |

## 3. CSS

### Parsing and cascade

- **Own parser, not ExCSS.** It preserves custom properties and `var()` as raw text. It does not validate declarations.
- **Unsupported at-rules are skipped whole:** `@import`, `@layer`, `@container`, `@property`, `@page`, `@namespace`. (`@media`, `@supports`, `@keyframes` and `@font-face` are supported.) Rules inside `@layer` or `@container` are dropped, not applied. `@supports` is treated as always true.
- **CSS nesting** is not supported.
- **Selectors:** no pseudo-elements (`::before`, `::after`, `::placeholder`, `::selection`), no `:has()`, `:focus-within`, `:nth-of-type`, `:first-of-type`, `:nth-last-child`, `:target`, `:visited`. A selector using one is dropped. If every selector in a rule is dropped, the rule is dropped.
- **Media queries:** `min/max-width`, `min/max-height`, `prefers-color-scheme`, `screen`, `all`, `and`, and comma lists only. No `not`, `only`, range syntax, `orientation`, `resolution`, `hover`.
- **Keywords:** `inherit` works for a subset of properties. `initial`, `unset`, `revert` are not supported.
- **Values:** `calc()` works for absolute lengths and numbers only (no `%`). `min()`, `max()`, `clamp()` and `env()` are not supported.
- **Colors:** hex, `rgb()/rgba()`, `hsl()/hsla()`, `currentColor`, `transparent` and the full CSS named-color list. No `color()`, `lab()`, `oklch()` or `color-mix()`.
- **Scoped CSS** (`.razor.css`) is not supported. Use global stylesheets or `style` attributes.
- **`!important`** works at declaration level.

### Box model and text

- **No margin collapsing**: adjacent vertical margins add.
- **Inline elements have no box of their own**: a `<span>` cannot have a background, border or padding that wraps its text. They are hit-testable through their text.
- **Not implemented:** `float`, `white-space` (so no `pre`/`nowrap`), `vertical-align` (inline items are centred in the line), `text-align: justify`, `text-overflow`, `overflow-wrap`, `letter-spacing`, `word-spacing`, `text-indent`, `line-clamp`, `columns`, `aspect-ratio`, `object-fit`.
- **`text-transform`** (`uppercase`, `lowercase`, `capitalize`) is supported. `capitalize` uppercases the first letter of each whitespace-separated word, a slightly coarser boundary than the Unicode word-break the spec calls for.
- **`border-style`**: `solid`, `dashed`, `dotted` and `double` are supported when a side's width, color and style all match its neighbours (the common case: a `border` shorthand, or a uniform `border-style`/`border-width`/`border-color`). `groove`, `ridge`, `inset` and `outset` still draw solid **(Approximation)**. A border whose sides differ in width, color or style falls back to the mitred solid-wedge renderer, so dashed or dotted mixed with per-side differences still draws solid.
- **`box-sizing`, min/max sizes, `%` and viewport units** work. `%` heights resolve only against a definite parent height. `%` padding and margin resolve against the containing block width.
- **Elements not laid out as designed:** `table` (tables, rows and cells have no table layout), `ul/ol/li` (no bullets or numbering), `img`, `svg`, `canvas`, `video`, `iframe`. Unknown tags default to inline.

### Visual effects

Supported: `box-shadow` (outer and inset, multiple layers, spread, blur), `text-shadow`, `linear-gradient`,
`radial-gradient` and their `repeating-` forms (hard stops, `to <corner>` and `at <position>` included),
2D `transform` with `transform-origin`, `filter` (blur, brightness, contrast, grayscale, sepia, saturate, hue-rotate,
invert, opacity, drop-shadow), per-corner `border-radius` (px and %), and mixed-colour borders with mitred corners.

- **Not implemented:** `clip-path`, `backdrop-filter`, `mix-blend-mode`, `mask`, `border-image`, `conic-gradient`, `filter: url()`, `background-image: url()` and any raster or SVG image, and `background-position`, `background-size`, `background-repeat`, `background-clip`, `background-attachment` (gradients always fill the border box).
- **Gradient details:** colour stop hints (midpoints) and interpolation colour spaces are not supported. A `transparent` stop borrows its neighbour's colour to avoid a grey fringe, which matches browsers' premultiplied result.
- **Radii:** the elliptical `a / b` syntax is not supported. A percentage radius uses the smaller side of the box, so `50%` gives a circle or a pill but not an ellipse on a non-square box **(Approximation)**.
- **Transforms are 2D only:** no `perspective`, `rotateX/Y/Z`, `translate3d` depth, or the standalone `translate`, `scale`, `rotate` properties. A transform paints and hit-tests correctly, but it does not change layout, scrollable overflow, `scrollIntoView`, or the position of a select dropdown **(Approximation)**.
- **Box shadows and culling:** content more than 160 px outside the visible area is skipped, so a shadow or outline reaching further than that into view could be clipped.
- The select dropdown's own shadow is hard-coded.

### Motion

Supported: `transition` (all longhands, `all`, shorthands such as `margin`, `cubic-bezier`, `steps`, delays, retargeting while in flight)
and `@keyframes` animation (`animation` shorthand and longhands, iteration count including `infinite`, direction, fill mode,
play state, multiple animations). Animatable: `opacity`, colours, lengths and sizes, margins, paddings, borders, radii, `transform`,
`box-shadow`, `text-shadow`, `filter`, flex properties, gaps, `font-size`, outline.

- **No animation events:** `transitionend`, `animationstart`, `animationend` and `animationiteration` are not dispatched to Blazor handlers.
- **Interpolation limits:** lengths of different units (px vs %) and `auto` switch at the halfway point rather than blending. Transform lists with different function sequences, and shadow or filter lists of different lengths, also switch at the halfway point (there is no matrix decomposition) **(Approximation)**.
- **Keyframes:** a keyframe's own `animation-timing-function` is ignored (the element's timing is used for every segment). Implicit `from` and `to` values are captured when the animation starts, so a later restyle does not update them.
- **Not animatable or not supported:** custom properties and `@property`, `z-index`, `display`, `visibility`, gradients, `grid-template-*`, `animation-composition`, `animation-timeline`, the Web Animations API, and `prefers-reduced-motion`.
- **Clock:** animations use a monotonic real-time clock. Tests can freeze it (`--advance`), but there is no host-visible pause.

### Fonts

`@font-face` with `local()` and `url()` sources is supported (file paths relative to the stylesheet, absolute paths, `file:///`),
choosing the nearest registered weight and style, with later rules overriding earlier ones.

- **TTF, OTF and TTC only.** WOFF and WOFF2 are skipped with a warning because Skia cannot decode them; a build step or a decoder is needed.
- `unicode-range`, `font-display`, `size-adjust`, `font-synthesis`, variable-font axes (`font-variation-settings`) and `http(s)` URLs are not supported. A weight range descriptor uses its first value. `local()` needs the exact installed family name.

### Flexbox

- `align-items: baseline` behaves as `flex-start` **(Approximation)**.
- `flex-wrap: wrap-reverse` behaves as `wrap` **(Approximation)**.
- `gap` accepts pixel-resolved lengths only (no `%`).
- The automatic minimum size is zero for scroll containers and content-based otherwise, which matches the spec for the common cases.

### Grid

- Not supported: named lines, `grid-template-areas`, `grid-area`, the `grid` shorthand, `grid-auto-flow: column` and `dense`, subgrid, masonry.
- `fit-content()` is approximated as `auto` with a max-content limit.
- `repeat(auto-fill | auto-fit, ...)` treats both identically (empty tracks are not collapsed for `auto-fit`).
- Auto-placement is sparse row-major only.

### Positioning and stacking

- `position: sticky` behaves as static.
- Stacking contexts exist for the root, `z-index` on positioned elements, `position: fixed`, `opacity < 1`, `transform` and `filter`. A positioned element with `z-index: auto` is painted at level 0 together with its positioned descendants **(Approximation)**. `isolation`, `mix-blend-mode` and `will-change` do not create contexts.
- `top: <percent>` on relative boxes resolves against the containing block width **(Approximation)**.
- The static position of an absolute box in inline content, flex or grid is the start of the content box **(Approximation)**.
- `margin: auto` centring of absolutely positioned boxes (with both insets set) is not implemented.
- A fixed box inside a scrolled context ignores that context's scrolling, which is correct. A transformed ancestor does not become the containing block of its fixed or absolute descendants, as it does in browsers **(Approximation)**.

### Scrolling

- **Scrollbars are overlay thumbs**: draggable, but no track clicks or arrow buttons **(Planned)**. `scrollbar-width` and `scrollbar-color` are ignored.
- Wheel scrolling uses a fixed 80 px per notch, with no smooth scrolling, momentum, `scroll-behavior`, `scroll-snap` or `overscroll-behavior`.
- `overflow: clip` is treated as `hidden`. `overflow: overlay` is treated as `auto`.
- No `onscroll` event is dispatched. No `scrollIntoView` API (Tab focus scrolls into view internally).
- Hit testing uses rectangles and ignores `border-radius`.

## 4. Input and events

### Pointer

- Only the **left button** is dispatched. Right and middle buttons, `contextmenu`, `auxclick`, `dblclick` (double-click is detected internally for text selection only) and touch are not dispatched.
- **Pointer Events** (`onpointerdown`, `onpointermove`, ...) are not dispatched. Only mouse events (`click`, `mousedown/up/move/enter/leave/over/out`, `wheel`).
- No pointer capture, drag and drop, or gesture handling.
- Cursor mapping covers `pointer`, `text`, `crosshair` and the horizontal/vertical resize cursors. Other keywords fall back to the arrow.

### Keyboard

- `keydown` and `keyup` are dispatched. `keypress`, `beforeinput`, `compositionstart/update/end`, and clipboard events (`copy`, `cut`, `paste`) are not.
- Printable `Key` values in `keydown`/`keyup` assume a **US layout**. Typed text itself uses the OS character events and is layout-correct.
- `KeyboardEventArgs.Location` is set (standard/left/right/numpad). Numpad keys still report `key` as plain digits (matching `code: "NumpadN"`), since Silk.NET does not expose NumLock state to tell a digit apart from a navigation keypad press.

### IME (**Unsupported on the current backend**)

Committed IME text works, since it arrives as characters. The pre-edit (composition) text, its underline, and candidate-window positioning do not, because GLFW 3.3 as exposed by Silk.NET 2.x has no pre-edit callback. This needs a newer GLFW binding or a platform-specific integration.

### Focus

- Sequential focus order is document order plus positive `tabindex`. No focus trapping, `autofocus`, or `:focus-within`.
- `FocusAsync()` and `ElementReference` capture are not supported (they need JS interop).

### Accessibility (**Unsupported**)

No UI Automation, ARIA mapping, screen-reader support, or high-contrast handling. This is a significant gap for a production toolkit.

### Text selection outside controls (**Unsupported**)

Static text (paragraphs, labels) cannot be selected or copied. Selection exists only inside `input` and `textarea`. `user-select` is ignored.

## 5. Form controls

- **Text input and textarea:** no context menu (right-click cut/copy/paste), no drag-and-drop of text, no spell check, no autofill. No right-to-left caret movement. Undo history is per field and lost when the element is removed. The textarea has no resize handle, and number inputs show no spinner buttons.
- **Placeholder** is a fixed gray and `::placeholder` cannot restyle it. Selection color is derived from `accent-color` and `::selection` cannot restyle it. There is no `caret-color`.
- **Input types not implemented:** `range`, `color`, `file`, `date`, `time`, `datetime-local`, `month`, `week`, `image`. They are edited as plain text or shown as an empty control.
- **Number input** accepts any of `0-9 + - . e E` while typing (including incomplete values like `1e`), with no locale handling.
- **Validation:** `required`, `pattern`, `min/max` (beyond stepping), `:valid`, `:invalid` and the constraint validation API are not implemented. `reset` buttons do nothing.
- **Select:** single selection only. No `multiple`, `size` list boxes, `optgroup` headings or `datalist`. The dropdown is capped at 10 visible rows and always uses a light theme.
- **Checkbox:** an `indeterminate` attribute paints the dash state and matches `:indeterminate`. Radio groups are matched by `name` within the same `form`.
- **`label`:** the activated control is the one named by `for` or the first labelable descendant.
- **Form submit:** the `submit` event is dispatched. There is no navigation or form-data collection.
- **Binding race:** two-way binding relies on handlers updating state synchronously. A handler that awaits before assigning can briefly overwrite fast typing, because the renderer does not use event field info to reconcile.

## 6. Blazor hosting

| Limit | Notes |
| --- | --- |
| No JS interop | `IJSRuntime` is not registered. Components that inject it fail. This rules out `Virtualize`, `InputFile`, `FocusAsync`, and most third-party component libraries. |
| No routing | `@page` is ignored, and `Router` and `NavigationManager` are not provided. The demo menu switches components with state. |
| `ElementReference` | `@ref` on elements is ignored (reference-capture frames are skipped). |
| Markup frames | Static HTML emitted by the Razor compiler is parsed by a minimal fragment parser: no implicit tag closing, no table fix-up, entities via `HtmlDecode` only. |
| `EditForm` and validation components | Untested. The submit event path works for plain `<form @onsubmit>`. |
| Hot reload | Untested. |
| Service provider | Only logging is registered. Add services by extending `UiHost`. |

---

## Where to look in the code

| Area | Files |
| --- | --- |
| CSS parsing and cascade | `Styling/CssParser.cs`, `Selectors.cs`, `StyleResolver.cs`, `CssValues.cs` |
| Effects values (shadows, gradients, transforms, filters) | `Styling/EffectValues.cs`, `EffectTypes.cs`, `Rendering/GradientShader.cs`, `Rendering/Affine.cs` |
| Transitions and animations | `Styling/Animator.cs`, `AnimatedProperties.cs`, `Easing.cs` |
| Block, inline, flex, grid layout | `Layout/LayoutEngine.cs`, `FlexLayout.cs`, `GridLayout.cs`, `PositionedLayout.cs` |
| Paint order, culling and hit testing | `Rendering/DisplayList.cs`, `Painter.cs` |
| Incremental invalidation and layout cache | `Dom/Nodes.cs` (`Document`, dirty flags), `Styling/StyleResolver.cs`, `Layout/LayoutEngine.cs` |
| Profiling and self-checks | `Platform/FrameStats.cs`, `UiHost.VerifyLayout` and `VerifyStyles`, `App.Benchmark` |
| Form controls | `Dom/TextEditState.cs`, `Dom/Controls.cs`, `Layout/TextControls.cs`, `Rendering/ControlPainter.cs` |
| Input routing | `Platform/UiHost.cs`, `Platform/KeyMap.cs`, `Platform/App.cs` |
