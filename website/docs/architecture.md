---
sidebar_position: 3
---

# Architecture

Sway turns a widget tree into pixels in stages that mirror Flutter's, each implemented under `Sway.Widgets`:

```
Widget tree            immutable configuration returned by Build()
        │  diff by type and key
        ▼
Element tree           long-lived instances: State, inherited lookups, dirty tracking
        ▼
RenderObject tree      constraints down, sizes up; paint; hit test
        ▼
Skia canvas            SKCanvas on an OpenGL surface (or a CPU bitmap when headless)
        ▼
Platform host           window, input routing, clipboard, cursors (Sway.Platform.Desktop today)
```

## Foundation

`Foundation/Widget.cs` defines `Widget`, `StatelessWidget`, `StatefulWidget`/`State`, `InheritedWidget`,
`ParentDataWidget` and the matching elements. `Element.UpdateChildren` reconciles child lists from both ends and then
by key, so a `State` follows its widget when siblings reorder. `BuildOwner` keeps the dirty list and rebuilds
shallowest elements first. `Geometry.cs` holds `Offset`, `Size`, `EdgeInsets`, `Alignment` and `BoxConstraints`;
`Painting.cs` holds decorations, borders, shadows and gradients.

## Rendering

`Rendering/RenderObject.cs` is the base of the render tree. `MarkNeedsLayout` flags the chain up to the root, and a
layout pass only re-lays-out flagged nodes: a child with unchanged constraints and no dirty flag returns immediately.
`RenderBox` adds the constraint protocol and hit testing; `RenderShifted.cs` (padding, align, clip, opacity,
transform, listener), `RenderFlex.cs` (Row/Column and Stack) and `RenderLayouts.cs` (Wrap, Grid, filters) implement
the layout widgets.

Text goes through `RenderParagraph`: runs are measured with HarfBuzz (`TextShaper`), broken into lines, split into
directional runs by `Bidi`, and drawn per run in visual order. `RenderEditable` draws the same way for text fields and
adds the caret, selection and inner scrolling on top of `TextEditState`.

## Binding and frames

`Widgets/Binding.cs` (`WidgetsBinding`) owns the build and pipeline owners and runs a frame:

1. Run due timers and frame callbacks (tickers, fling).
2. Rebuild dirty elements.
3. Flush layout.
4. Paint the whole tree into the canvas.
5. Re-run hover testing, since layout may have moved things under a still pointer.

The host only draws when `NeedsFrame` is true: something was marked dirty, a timer is due, or an animation requested
another frame. The clock can be frozen and advanced by hand (`UseManualClock`, `AdvanceClock`), which is what makes
animated screenshots deterministic.

## Input

`GestureBinding` hit tests the render tree, delivers pointer events to the objects under the pointer, tracks hover for
`MouseRegion`, and routes events by pointer id. `GestureArena` decides which recognizer wins when several compete
(a tap versus a drag inside a scroll view); the first claimant wins, otherwise the deepest on pointer-up.
`FocusManager` holds the primary focus, dispatches key events up the focus chain and implements Tab traversal.

## Platform

Rendering and platform code live in separate projects. `Sway.Widgets` is platform-neutral: widgets, layout, painting
and text on SkiaSharp and HarfBuzz, with no window, input or OS dependency. It talks to the host through a small
surface: `WidgetsBinding` takes pointer, key and text events and draws a frame onto any `SKCanvas`, the clipboard and
cursor are delegates, and `ISystemThemeSource` (installed through `SystemTheme.Source`) supplies the OS light/dark
preference and accent colour. `Headless.Screenshot` renders to a PNG with no host at all.

`Sway.Platform.Desktop` is the host for Windows, Linux and macOS. `App.Run` owns the Silk.NET window, the
OpenGL/Skia surface and the input wiring; `KeyMap` translates Silk.NET keys to DOM-style key names (`"Enter"`,
`"ArrowLeft"`), which is what `KeyEvent.Key` carries; `DesktopSystemTheme` picks the registry, `defaults` or
`gsettings` theme source for the current OS. `App.Screenshot` wraps `Headless.Screenshot` with the desktop theme.

A new platform (Android, say) is a new `Sway.Platform.*` project that creates a GPU surface, forwards touch, key and
text input to `WidgetsBinding`, calls `DrawFrame` each frame it needs one, and provides an `ISystemThemeSource`.
Nothing in `Sway.Widgets` changes.

## Where to look in the code

| Area | Files |
| --- | --- |
| Widget, Element, State | `Foundation/Widget.cs` |
| Constraints, geometry, decorations | `Foundation/Geometry.cs`, `Painting.cs` |
| Render objects and layout | `Rendering/RenderObject.cs`, `RenderBox.cs`, `RenderShifted.cs`, `RenderFlex.cs`, `RenderLayouts.cs` |
| Text | `Rendering/TextShaper.cs`, `Bidi.cs`, `FontCache.cs`, `RenderParagraph.cs`, `RenderEditable.cs` |
| Frames, clock and gestures | `Widgets/Binding.cs`, `Widgets/Gestures.cs` |
| Focus and keyboard | `Widgets/Focus.cs`, `Sway.Platform.Desktop/KeyMap.cs` |
| Scrolling | `Widgets/Scrolling.cs` |
| Animation | `Foundation/Animation.cs`, `Curves.cs`, `Lerp.cs`, `Widgets/Animated.cs` |
| Material 3 (`Sway.Extras.Material3`) | `ColorScheme.cs`, `Theme.cs`, `Controls.cs`, `Components.cs`, `Menus.cs`, `TextField.cs` |
| IBM Carbon (`Sway.Extras.Ibm`) | `CarbonColors.cs`, `CarbonTheme.cs`, `CarbonButton.cs`, `CarbonTextInput.cs`, `DataTable.cs` |
| Design-neutral building blocks | `Widgets/Interactive.cs`, `Icon.cs`, `EditableText.cs` |
