---
sidebar_position: 3
---

# Architecture

Sway turns a Razor component tree into pixels in the same stages a browser would, each implemented as its own
area under `Sway.Core`:

```
Blazor component tree
        │  Microsoft.AspNetCore.Components (RenderTreeBuilder)
        ▼
  Dom/            node tree (elements, text, attributes) kept in sync with Blazor's render output
        ▼
  Styling/        CSS parsing, cascade and selector matching → ComputedStyle per node
        ▼
  Layout/         block / inline / flex / grid → position and size for every box
        ▼
  Rendering/       a culled display list, painted with SkiaSharp
        ▼
  Platform/        the OpenGL window (Silk.NET) or headless PNG output, input routing
```

## Dom

`Dom/Nodes.cs` defines the node tree (`Document`, `ElementNode`, `TextNode`) that Sway keeps in sync with Blazor's
render output, along with the dirty flags that drive incremental restyle/layout. `Dom/Controls.cs` classifies the
built-in form controls (`input`, `textarea`, `select`, checkboxes, radios) — these are "replaced" elements that
draw themselves instead of laying out children. `Dom/HtmlFragment.cs` parses the static HTML fragments the Razor
compiler emits for markup, using a minimal parser (no implicit tag closing or table fix-up). `Dom/TextEditState.cs`
holds the caret/selection state for text editing.

## Styling

`Styling/CssParser.cs` is a parser written for Sway rather than a wrapper around an existing engine, so it can
preserve custom properties and `var()` as raw text. `Selectors.cs` matches selectors against the node tree, and
`StyleResolver.cs` runs the cascade — specificity, `@media`/`@supports`, inheritance — to produce a
`ComputedStyle` (`ComputedStyle.cs`) for each node. `CssValues.cs` and `GridValues.cs` parse property values.
`Animator.cs`, `AnimatedProperties.cs` and `Easing.cs` drive `transition` and `@keyframes` animation, interpolating
the computed style over time. `EffectTypes.cs`/`EffectValues.cs` model shadows, gradients, filters and transforms.

## Layout

`Layout/LayoutEngine.cs` walks the styled tree and dispatches to `FlexLayout.cs`, `GridLayout.cs` or block/inline
layout for each box, producing a position and size for every node. `PositionedLayout.cs` resolves absolute/fixed
positioning and stacking. `TextControls.cs` lays out editable text controls, and `FontCache.cs` resolves
`@font-face` and system font fallback. A layout change only re-lays-out its dirty chain up to the root; clean
subtrees are translated rather than recomputed (see [Known limits](./known-limits) for the exceptions).

## Rendering

`Rendering/DisplayList.cs` builds a paint-order, culled list of draw operations from the laid-out tree each frame.
`Painter.cs` and `ControlPainter.cs` execute that list against a SkiaSharp canvas — the latter specifically for
form controls, which paint themselves rather than going through normal box painting. `GradientShader.cs` and
`Affine.cs` implement gradients and 2D transform matrices. `SkiaRenderer.cs` owns the `SKSurface`/canvas setup for
both the live GPU path and the headless CPU-raster path, and `SelectPopup.cs` renders the `<select>` dropdown as an
overlay.

## Platform

`Platform/App.cs` is the entry point (`App.Create().AddStylesheet(...).Run<TRoot>(...)`): it owns the Silk.NET
window, OpenGL/Skia surface, and input event wiring, and also exposes `Screenshot<TRoot>` for headless rendering.
`Platform/UiHost.cs` is the core per-document host — it mounts the Blazor component, owns the render loop
(`NeedsFrame`/`Frame`), and routes pointer, keyboard and clipboard events into the Dom/Styling/Layout pipeline. It
also implements the `VerifyLayout`/`VerifyStyles` self-checks and `FrameStats` profiling used by the `--verify` and
`--bench` CLI flags (see [Getting started](./getting-started)). `KeyMap.cs` translates Silk.NET key codes to
Blazor's key names and maps cursor styles to platform cursors.

## Where to look in the code

| Area | Files |
| --- | --- |
| CSS parsing and cascade | `Styling/CssParser.cs`, `Selectors.cs`, `StyleResolver.cs`, `CssValues.cs` |
| Effects values (shadows, gradients, transforms, filters) | `Styling/EffectValues.cs`, `EffectTypes.cs`, `Rendering/GradientShader.cs`, `Rendering/Affine.cs` |
| Transitions and animations | `Styling/Animator.cs`, `AnimatedProperties.cs`, `Easing.cs` |
| Block, inline, flex, grid layout | `Layout/LayoutEngine.cs`, `FlexLayout.cs`, `GridLayout.cs`, `PositionedLayout.cs` |
| Paint order, culling and hit testing | `Rendering/DisplayList.cs`, `Painter.cs` |
| Incremental invalidation and layout cache | `Dom/Nodes.cs` (`Document`, dirty flags), `Styling/StyleResolver.cs`, `Layout/LayoutEngine.cs` |
| Profiling and self-checks | `Platform/FrameStats.cs`, `UiHost.VerifyLayout`/`VerifyStyles`, `App.Benchmark` |
| Form controls | `Dom/TextEditState.cs`, `Dom/Controls.cs`, `Layout/TextControls.cs`, `Rendering/ControlPainter.cs` |
| Input routing | `Platform/UiHost.cs`, `Platform/KeyMap.cs`, `Platform/App.cs` |
