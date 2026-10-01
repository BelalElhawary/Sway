---
sidebar_position: 5
---

# Input, events and form controls

## Pointer

Only the **left mouse button** is dispatched. Right/middle buttons, `contextmenu`, `auxclick`, `dblclick` (used
internally for text selection only) and touch are not dispatched. Pointer Events (`onpointerdown`, etc.) are not
dispatched — only mouse events (`click`, `mousedown/up/move/enter/leave/over/out`, `wheel`). No pointer capture,
drag and drop, or gesture handling. Cursor mapping covers `pointer`, `text`, `crosshair` and resize cursors; other
keywords fall back to the arrow.

## Keyboard

`keydown`/`keyup` are dispatched; `keypress`, `beforeinput`, composition events, and clipboard events (`copy`,
`cut`, `paste`) are not. Printable `Key` values assume a **US layout** (typed text itself is layout-correct, via
OS character events). `KeyboardEventArgs.Location` is not set; numpad keys report as digits.

## IME (unsupported on the current backend)

Committed IME text works (it arrives as characters). Pre-edit/composition text, its underline, and candidate-window
positioning do not — GLFW 3.3 as exposed by Silk.NET 2.x has no pre-edit callback.

## Focus and accessibility

Sequential focus order is document order plus positive `tabindex`. No focus trapping, `autofocus`, or
`:focus-within`. `FocusAsync()` and `ElementReference` capture don't work (they need JS interop). There is **no
accessibility support**: no UI Automation, ARIA mapping, screen-reader support, or high-contrast handling.

## Text selection

Static text (paragraphs, labels) cannot be selected or copied — selection only exists inside `input` and
`textarea`. `user-select` is ignored.

## Form controls

- **Text input / textarea:** no context menu, no drag-and-drop of text, no spell check, no autofill, no
  right-to-left caret movement. Undo history is per-field and lost when the element is removed. No textarea resize
  handle; no spinner buttons on number inputs.
- **Placeholder** is a fixed gray; `::placeholder` can't restyle it. No `caret-color`; `::selection` can't restyle
  selection color (it's derived from `accent-color`).
- **Not implemented input types:** `range`, `color`, `file`, `date`, `time`, `datetime-local`, `month`, `week`,
  `image` — these are edited as plain text or shown empty.
- **Number input** accepts `0-9 + - . e E` while typing, including incomplete values, with no locale handling.
- **Validation:** `required`, `pattern`, `min`/`max` (beyond stepping), `:valid`/`:invalid`, and the constraint
  validation API are not implemented. `reset` buttons do nothing.
- **Select:** single selection only — no `multiple`, `size` list boxes, `optgroup` headings, or `datalist`. The
  dropdown caps at 10 visible rows and always uses a light theme.
- **Checkbox:** no indeterminate state. Radio groups are matched by `name` within the same `form`.
- **`label`:** activates the control named by `for`, or the first labelable descendant.
- **Form submit:** the `submit` event is dispatched; there's no navigation or form-data collection.
- **Binding race:** two-way binding relies on handlers updating state synchronously — a handler that `await`s
  before assigning can briefly be overwritten by fast typing.

See [Dom/Controls.cs](https://github.com/BelalElhawary/Sway/blob/master/Sway.Core/Dom/Controls.cs) for how controls
are classified, and [Known limits](./known-limits) for the full reference this page is condensed from.
