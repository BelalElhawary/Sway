---
sidebar_position: 6
---

# Input and focus

## Pointer and gestures

Only the left mouse button is dispatched. `GestureDetector` recognizes taps and drags:

```csharp
new GestureDetector(
    onTap: () => { },
    onTapDown: d => { }, onTapUp: d => { }, onTapCancel: () => { },
    onPanUpdate: d => offset += d.Delta,
    onVerticalDragEnd: d => fling(d.Velocity.Dy),
    child: child)
```

When several recognizers compete, the gesture arena decides: the first to claim the pointer wins (a drag claims it
after moving a few pixels), otherwise the deepest recognizer wins on release. That is why a tappable row inside a
scroll view can both tap and scroll.

- `Listener` exposes raw pointer events (down, move, up, scroll).
- `MouseRegion` provides `onEnter`, `onExit`, `onHover` and a `MouseCursor`.
- `IgnorePointer` hides a subtree from hit testing.

## Scrolling

`SingleChildScrollView` and `ListView` scroll with the wheel, by dragging with fling, and show a fading scrollbar.
`ListView.Builder` builds only visible rows:

```csharp
ListView.Builder(10000, (context, i) => new ListTile(new Text($"Row {i}")), itemExtent: 56)
```

Items must share one main-axis size: pass `itemExtent`, or the first item's size is used. Drive a list from code
with a `ScrollController` (`JumpTo`, `AnimateTo`, `Offset`). Nested scrollables hand the wheel to the outer one at
the edge.

## Focus and keyboard

`Focus` makes a subtree focusable and exposes a `FocusNode`. Key events go to the primary focus and bubble up through
its ancestors until one returns true from `OnKey`; an unhandled Tab moves focus to the next node in tree order
(Shift+Tab goes back).

```csharp
new Focus(
    onKey: e => e.IsDown && e.Key == "Enter" ? Submit() : false,
    onFocusChange: has => SetState(() => _focused = has),
    child: child)
```

`KeyEvent.Key` uses DOM-style names (`"Enter"`, `"ArrowLeft"`, `"a"`), with `Ctrl`, `Shift`, `Alt` and `Meta`
flags; `Command` is true for Ctrl or Meta. Typed characters arrive separately through `FocusNode.OnTextInput`.

## Text fields

`EditableText` is the undecorated core in `Sway.Widgets`; `TextField` (Material 3) and `CarbonTextInput` (Carbon) are the decorated fields built on it. Both take a `TextEditingController`:

```csharp
var name = new TextEditingController("Ada");

new TextField(name,
    decoration: new InputDecoration(LabelText: "Name", HelperText: "Shown on your profile"),
    onChanged: text => { },
    onSubmitted: text => { })

name.Text = "Grace";          // programmatic change
name.SelectAll();
```

Supported editing: caret and selection with mouse (click, drag, double-click word, triple-click line), Shift-extend,
word movement with Ctrl, copy, cut, paste, undo and redo, multi-line wrapping with inner scrolling (`maxLines`,
`minLines`), obscured text, read-only and disabled states, and right-to-left lines. Set `Filled: true` in
`InputDecoration` for the filled variant, and `ErrorText` for the error state.
