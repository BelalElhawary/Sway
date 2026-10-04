---
sidebar_position: 4
---

# CSS support

Sway has its own CSS parser and cascade rather than wrapping an existing engine. This page summarizes what's
implemented; see [Known limits](./known-limits) for the full, precise list this is condensed from.

## Parsing and cascade

- Custom properties and `var()` are preserved as raw text.
- Supported at-rules: `@media`, `@supports` (always treated as true), `@keyframes`, `@font-face`.
- Unsupported and skipped whole: `@import`, `@layer`, `@container`, `@property`, `@page`, `@namespace`. CSS nesting
  is not supported.
- Selectors: no pseudo-elements (`::before`, `::after`, `::placeholder`, `::selection`), no `:has()`,
  `:focus-within`, `:nth-of-type`, `:first-of-type`, `:nth-last-child`, `:target`, `:visited`. A rule is dropped if
  every selector in it uses something unsupported.
- Media queries: `min/max-width`, `min/max-height`, `prefers-color-scheme`, `screen`, `all`, `and`, comma lists.
  No `not`, `only`, range syntax, `orientation`, `resolution`, `hover`.
- `calc()` works for absolute lengths and numbers only (no `%`). `min()`, `max()`, `clamp()`, `env()` are not
  supported. `inherit` works for a subset of properties; `initial`/`unset`/`revert` do not. `!important` works.
- Colors: hex, `rgb()/rgba()`, `hsl()/hsla()`, `currentColor`, `transparent`, ~50 named colors. No `color()`,
  `lab()`, `oklch()`, `color-mix()`.

## Box model and text

- No margin collapsing (adjacent vertical margins add instead).
- Inline elements (e.g. `<span>`) have no box of their own — no background/border/padding wrapping their text.
- `<br>` doesn't break lines (parsed, no effect). `<hr>` has no UA style.
- Not implemented: `float`, `white-space` (no `pre`/`nowrap`), `vertical-align`, `text-align: justify`,
  `text-overflow`, `overflow-wrap`, `letter-spacing`, `word-spacing`, `text-transform`, `text-indent`,
  `line-clamp`, `columns`, `aspect-ratio`, `object-fit`.
- `border-style` other than `none` always draws solid.
- `box-sizing`, min/max sizes, `%` and viewport units work. `%` heights resolve only against a definite parent
  height; `%` padding/margin resolve against the containing block width.
- Not laid out as designed: `table` (no table layout), `ul`/`ol`/`li` (no bullets/numbering), `img`, `svg`,
  `canvas`, `video`, `iframe`. Unknown tags default to inline.

## Visual effects

**Supported:** `box-shadow` (outer/inset, multi-layer, spread, blur), `text-shadow`, `linear-gradient`,
`radial-gradient` (and `repeating-` forms), 2D `transform` with `transform-origin`, `filter` (blur, brightness,
contrast, grayscale, sepia, saturate, hue-rotate, invert, opacity, drop-shadow), per-corner `border-radius`
(px and %), mixed-color borders with mitred corners.

- Not implemented: `clip-path`, `backdrop-filter`, `mix-blend-mode`, `mask`, `border-image`, `conic-gradient`,
  `filter: url()`, `background-image: url()`/raster or SVG images, `background-position`/`size`/`repeat`/`clip`/
  `attachment` (gradients always fill the border box).
- Transforms are 2D only — no `perspective`, `rotateX/Y/Z`, 3D `translate`, or the standalone `translate`/`scale`/
  `rotate` properties. A transform paints and hit-tests correctly but doesn't affect layout or scroll.
- Content more than 160px outside the visible area is culled, including shadows/outlines.

## Motion

**Supported:** `transition` (all longhands, `all`, shorthands, `cubic-bezier`, `steps`, delays, retargeting) and
`@keyframes` animation (shorthand/longhands, iteration count incl. `infinite`, direction, fill mode, play state,
multiple animations). Animatable: `opacity`, colors, lengths/sizes, margins, paddings, borders, radii, `transform`,
`box-shadow`, `text-shadow`, `filter`, flex properties, gaps, `font-size`, outline.

- No animation events (`transitionend`, `animationstart/end/iteration`) dispatched to Blazor handlers.
- Interpolation between different units (px vs %), `auto`, differing transform/shadow/filter list shapes switches
  at the halfway point rather than blending.
- Not animatable: custom properties, `@property`, `z-index`, `display`, `visibility`, gradients,
  `grid-template-*`, `animation-composition`/`timeline`, the Web Animations API, `prefers-reduced-motion`.

## Fonts

`@font-face` with `local()` and `url()` works (relative/absolute/`file:///` paths), choosing the nearest registered
weight/style. **TTF, OTF and TTC only** — WOFF/WOFF2 are skipped (Skia can't decode them). `unicode-range`,
`font-display`, `size-adjust`, variable-font axes, and `http(s)` URLs are not supported.

## Flexbox

- `align-items: baseline` behaves as `flex-start`. `flex-wrap: wrap-reverse` behaves as `wrap`.
- `gap` accepts pixel-resolved lengths only (no `%`).
- `place-items`/`place-content`/`place-self` shorthands are not parsed.

## Grid

- Not supported: named lines, `grid-template-areas`, `grid-area`, the `grid` shorthand,
  `grid-auto-flow: column`/`dense`, subgrid, masonry.
- `fit-content()` is approximated as `auto` with a max-content limit. `repeat(auto-fill | auto-fit, ...)` are
  treated identically. Auto-placement is sparse row-major only.

## Positioning, stacking and scrolling

- `position: sticky` behaves as static.
- Stacking contexts: root, `z-index` on positioned elements, `position: fixed`, `opacity < 1`, `transform`,
  `filter`. `isolation`, `mix-blend-mode`, `will-change` do not create contexts.
- `margin: auto` centring of absolutely positioned boxes is not implemented.
- Scrollbars are overlay thumbs only (not draggable, no track/arrow clicks). Wheel scrolling is a fixed 80px per
  notch — no smooth scrolling, momentum, `scroll-behavior`, or `scroll-snap`. No `onscroll` event.

## RTL and bidirectional text

Sway supports right-to-left (RTL) layout and Arabic / Hebrew text shaping via **HarfBuzz** (through `SkiaSharp.HarfBuzz`).

**Supported:**

- `direction: rtl | ltr` CSS property and `dir="rtl|ltr"` HTML attribute — inherited by all descendants.
- `text-align: start | end` resolve to `left` / `right` according to the computed `direction`, so they work
  correctly in both LTR and RTL contexts. `left`, `right`, and `center` still work as absolute values.
- **Logical CSS properties:** `margin-inline-start`, `margin-inline-end`, `padding-inline-start`,
  `padding-inline-end` map to the physical left/right sides according to `direction`.
- **HarfBuzz text shaping** — Arabic ligatures, cursive joining, and correct glyph selection are applied
  automatically whenever RTL characters are detected in a text run.
- **RTL inline layout** — inline text runs are laid out right-to-left; items in an RTL line are placed starting
  from the right edge.
- **RTL flexbox** — `display: flex; direction: rtl` reverses the main axis so the first flex item appears at the
  right edge. The `flex-direction: row-reverse` interaction is correctly accounted for.
- **RTL grid** — column positions are mirrored; `justify-items`/`justify-self` alignment is direction-aware.
- **RTL form controls** — text inside `<input>` and `<textarea>` with `direction: rtl` draws from the right;
  caret position and text selection are mirrored. Placeholder text is right-aligned.
- **RTL `<select>`** — the selected label is right-aligned and the chevron moves to the left edge; the popup
  anchors to the right edge of the control.
- **RTL keyboard navigation** — `ArrowLeft` moves the caret forward (toward the end of the string) and
  `ArrowRight` moves it backward when the focused field has `direction: rtl`.

**Not yet supported:**

- `unicode-bidi` property (`embed`, `bidi-override`, `isolate`, etc.).
- Full Unicode BiDi reordering of mixed-direction inline runs (e.g., embedded LTR words inside an RTL paragraph
  are shaped correctly but may not be repositioned in visual order by the BiDi algorithm).
- Vertical text (`writing-mode`).

For the exact wording, caveats and the reasoning behind each approximation, see [Known limits](./known-limits).
