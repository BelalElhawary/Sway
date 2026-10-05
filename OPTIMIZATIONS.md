# Sway optimization notes

Performance work found by reading the render pipeline, the text path, the lazy list and the desktop host. Nothing
here has been profiled yet: the **Impact** column is an estimate from the code, so measure with `--bench`
(`Sway.Example.Headless`) before and after each change. Items are ordered by expected payoff.

`LIMITS.md` already lists the whole-tree repaint and the cold start as known; they appear here with a concrete plan.

Items marked ✅ are done and covered by tests (see **Completed** at the end).

Legend: **High** = visible in normal use, **Med** = shows on large content or during animation, **Low** = allocation
trimming.

---

## 1. Frame scheduling and painting

| # | Impact | Where | Problem | Fix |
| --- | --- | --- | --- | --- |
| 1.1 ✅ | High | [GestureBinding.cs:38,64,77](Sway.Widgets/Widgets/Gestures/GestureBinding.cs) | `PointerMove` calls `RequestFrame()` unconditionally, so every mouse move repaints the whole tree even when nothing under the pointer reacts. | Request a frame only when something changed: hover set or cursor changed, a dispatched handler marked layout/paint, or a drag is active. Return a "dirty" flag from `Dispatch` and `UpdateHover`. |
| 1.2 | High | [Binding.cs:214](Sway.Widgets/Widgets/Binding.cs) `DrawFrameCore` | The whole tree is painted and cleared every frame. `MarkNeedsPaint` only calls `RequestFrame`, so a blinking caret or a hover colour repaints everything. | Add repaint boundaries: a `RenderRepaintBoundary` that records its subtree into an `SKPicture` (or an offscreen `SKSurface` for opacity/backdrop layers) and replays it while clean. Make `MarkNeedsPaint` walk up to the nearest boundary and flag it. Wrap scroll viewports, the caret/ripple/spinner leaves, and `Overlay` entries first, since they are the ones that animate on their own. |
| 1.3 | Med | [Binding.cs:214](Sway.Widgets/Widgets/Binding.cs) | Even without layers, the canvas is cleared and redrawn in full. | Track a dirty rect from `MarkNeedsPaint` (union of the boundary bounds) and clip the frame to it. Needs a back buffer that survives the swap, so do it after 1.2. |
| 1.4 | Med | [PaintingContext.cs](Sway.Widgets/Rendering/RenderObject/PaintingContext.cs) `PushOpacity` | Allocates an `SKPaint` per call and opens a `SaveLayer` for every partially transparent subtree. Nested fades (`AnimatedSwitcher`, ripples, menus) stack layers. | Reuse one paint instance per context. For a leaf child that draws a single primitive, fold the alpha into that primitive's colour instead of using a layer (`RenderOpacity` can ask the child via a `CanFoldOpacity` hook). |
| 1.5 | Med | [RenderBackdropFilter.cs](Sway.Widgets/Rendering/RenderLayouts/RenderBackdropFilter.cs) | Creates a new `SKImageFilter` every paint and a full blur every frame. | Cache the filter and rebuild only when `_blur` or `_colorFilter` changes. Combined with 1.2, a static blurred panel stops costing a blur per frame. |
| 1.6 | Low | `PushClipRect`, `PushClipRRect`, `PushOpacity`, `PushTransform` and every caller (`RenderClip`, `RenderOpacity`, `RenderLazyViewport.Paint`) | Each call allocates a lambda that captures the child, so a clip or fade costs a delegate allocation per frame. | Give the render objects non-capturing overloads (`PushClipRect(offset, rect, this, static (self, ctx, o) => ...)`) or an explicit `Save`/`Restore` pair. |
| 1.7 | Low | [BoxDecoration.cs](Sway.Widgets/Foundation/Painting/BoxDecoration.cs) `Paint` | Per box per frame: an `SKPath` for the shape, an `SKPaint` per shadow/fill/border, a new blur `SKMaskFilter` per shadow, and a shader per gradient. | Use `DrawRect`/`DrawRoundRect`/`DrawCircle` for the common cases instead of building a path. Cache the mask filter per blur radius and the gradient shader per `(gradient, size, direction)` on the render object. |
| 1.8 | Low | [RenderParagraph.cs:229-275](Sway.Widgets/Rendering/RenderParagraph/RenderParagraph.cs), [RenderEditable.cs:308](Sway.Widgets/Rendering/RenderEditable.cs) | `Paint` creates an `SKPaint` per segment (and per shadow), runs `_lines.Any(...)` to decide clipping, and `Enumerable.Reverse` for RTL, all per frame. | Compute `_needsClip` once in `PerformLayout`. Keep one paint per style on the segment. Iterate RTL segments with a reverse `for` loop. |

---

## 2. Text

| # | Impact | Where | Problem | Fix |
| --- | --- | --- | --- | --- |
| 2.1 ✅ | High | [TextEditState.cs:197-217](Sway.Widgets/Foundation/TextEditState/TextEditState.cs) `PreviousBoundary`, `NextBoundary` | Both call `StringInfo.ParseCombiningCharacters(Value)`, which scans and allocates an `int[]` for the whole string. They are called inside loops (`RenderEditable.FitLine`, `IndexAt`, `MoveVisualHorizontal`, `NextBoundary` twice per iteration in `FitLine`), so wrapping a long field is O(n²) in time and allocation, and runs on every keystroke. | Cache the boundary array keyed by `Version` (rebuild lazily once per edit) and binary-search it. Or use `StringInfo.GetNextTextElementLength` on a span, which does not allocate. Add an ASCII fast path: with no surrogates or combining marks, every index is a boundary. |
| 2.2 ✅ | High | [RenderEditable.cs](Sway.Widgets/Rendering/RenderEditable.cs) `FitLine` (~line 111) | For each candidate boundary it does `text.Substring(start, end - start)` and re-measures the whole prefix, again O(n²) for a long line. | Measure incrementally: accumulate advances per grapheme (or per word), or binary-search the break index over a cached `float[]` of prefix advances. The `LineOffsets` result already holds those advances once a line exists. |
| 2.3 | High | [RenderEditable.cs](Sway.Widgets/Rendering/RenderEditable.cs) `BuildLines` / `PerformLayout` | Every edit re-lays-out and re-shapes the whole document, including `Bidi.Analyze`, `Substring` and `CaretOffsets` for each line. Typing in a long textarea costs the length of the document. | Cache lines per paragraph (split at `\n`), keyed by the paragraph string plus width, and re-lay-out only paragraphs whose text changed. `TextEditState.LinesVersion` / `LinesWidth` already exist for this and are unused here. |
| 2.4 | Med | [TextShaper.cs](Sway.Widgets/Rendering/TextShaper.cs) `MeasureShaped`, `DrawShapedText` | Only RTL strings are cached. LTR strings go to `font.MeasureText` and `canvas.DrawText(string)` every time. `DrawText(string)` converts the string to glyphs on each call. | Cache `SKTextBlob` for LTR runs the same way (`(face, size, text)` key), or a paragraph-level blob per `Segment`, built once in `PerformLayout`. Painting then becomes one `DrawText(blob)` per segment. |
| 2.5 | Med | [TextShaper.cs](Sway.Widgets/Rendering/TextShaper.cs) `GetShaped` | When the cache reaches 2048 entries it clears everything, disposing blobs that may still be referenced by the frame in flight, and then reshapes the whole screen at once (a frame hitch). | Use an LRU, or generation-based eviction (drop the half not touched in the last N frames). Defer blob disposal to after the frame. |
| 2.6 | Med | [TextShaper.cs](Sway.Widgets/Rendering/TextShaper.cs) `CaretOffsets` | Builds a `Dictionary`, sorts with LINQ `OrderBy`, and allocates a `SortedDictionary` per run, on every layout of RTL text. Non-RTL runs fall back to `font.MeasureText(text.AsSpan(0, i))` per character, which is O(n²) in glyph work. | Use `font.GetGlyphWidths` / `GetGlyphPositions` once per run for LTR. For RTL, use arrays indexed by cluster instead of dictionaries, and sort in place. Cache the result per `(face, size, text, rtl)` like `ShapeCache`. |
| 2.7 | Med | [RenderParagraph.cs:68-135](Sway.Widgets/Rendering/RenderParagraph/RenderParagraph.cs) `BuildLines` / `Place` | Merging a token into the previous segment re-measures the concatenated string (`Measure(last.Text + token)`) and re-sums all segment widths with LINQ, so a long single-style paragraph is quadratic. `Substring` allocates every token. | Add widths instead of re-measuring when the style matches (shaping across a word boundary is the only error, and for LTR it is zero). Keep a running line width. Tokenise over `ReadOnlySpan<char>` and only materialise a string when a segment is created. |
| 2.8 | Med | [RenderParagraph.cs:316-343](Sway.Widgets/Rendering/RenderParagraph/RenderParagraph.cs) intrinsics | `MaxIntrinsicWidth`, `MaxIntrinsicHeight` and `MinIntrinsicWidth` rebuild all lines on each call, and `IntrinsicWidth`/`IntrinsicHeight` parents call them repeatedly. | Memoise per `(text version, width)`; clear in `Update` when `layout` is true. |
| 2.9 | Low | [RenderEditable.cs:51](Sway.Widgets/Rendering/RenderEditable.cs) `Display` | For obscured fields `new string('•', n)` is allocated on every access (layout, paint, hit test). `Font` also calls `ToFont()` (a dictionary lookup under a lock) each time. | Cache the obscured string by length and the `SKFont` by style. |
| 2.10 | Low | [RenderEditable.cs:359](Sway.Widgets/Rendering/RenderEditable.cs) `Paint` | `text.Substring` per visible line per frame (the caret blink repaints it twice a second). | Draw from the cached line text, or store the substring on `Line` when it is built. |
| 2.11 | Low | [FontCache.cs](Sway.Widgets/Rendering/FontCache.cs) | `GetTypeface` reads `Typefaces` without the lock that `Get` holds around it; `Get` takes a global lock on every `TextStyle.ToFont()`. | Make `Typefaces` and `Fonts` `ConcurrentDictionary`, or cache the `SKFont` on the `TextStyle` instance (it is an immutable record). |

---

## 3. Layout, build and lists

| # | Impact | Where | Problem | Fix |
| --- | --- | --- | --- | --- |
| 3.1 | High | [RenderLazyViewport.cs:112,212,219,225-229](Sway.Widgets/Widgets/Scrolling/RenderLazyViewport.cs) | Variable-extent lists use `PrefixOffset` (a linear sum from index 0) up to three times per layout, plus an inner loop per visible child (`for i = first..pd.Index`), and the `first` search walks from item 0. With 100k items, every scroll tick is O(n), and `OnScroll` marks layout on every pixel. | Keep a Fenwick tree (or a chunked prefix-sum array) of extents so prefix offset and "item at offset" are O(log n), and place children with a running cursor instead of re-summing. Estimates change as measurements arrive, so store measured extents in the tree and add `(index - measuredBefore) * estimate` on top. |
| 3.2 | Med | [RenderLazyViewport.cs](Sway.Widgets/Widgets/Scrolling/RenderLazyViewport.cs) `OnScroll` | Each scroll pixel calls `MarkNeedsLayout`, which re-runs `BuildRange` and `Layout` on the visible children. For `itemExtent` lists, layout is only needed when the visible range changes. | In `OnScroll`, compare the new first/last index with the previous one. If equal, only repaint (children offsets are derived from `scroll` at paint time) and skip layout. |
| 3.3 | Med | [Binding.cs:119](Sway.Widgets/Widgets/Binding.cs) `RunScheduled`, line 172 `NeedsFrame` | Every frame: a LINQ `Where/OrderBy/ToList` plus `RemoveAll` over the timers, a `ToArray` of frame callbacks, and `_timers.Any(...)` in the host's idle check (called each render tick, i.e. while idle too). | Keep timers in a `PriorityQueue` (or a sorted list) and compare against the head. Swap two reusable callback lists instead of `ToArray`. Track the earliest due time so `NeedsFrame` is O(1). |
| 3.4 | Med | [BuildOwner.cs](Sway.Widgets/Foundation/Widget/BuildOwner.cs) `BuildScope` | A lambda sort and a `ToArray` per pass, even for a single dirty element. | Fast-path `_dirty.Count == 1`. Reuse a scratch list. |
| 3.5 | Med | [Element.cs](Sway.Widgets/Foundation/Widget/Element.cs) `UpdateChildren` | Always allocates the result array, a `ToList()` via `Select`, and (for non-matching middle sections) a `Dictionary`. Runs for every `Column`/`Row`/`Stack` rebuild. | Return the array wrapped in a list without the LINQ copy, skip the dictionary when the middle section is empty (already partly done), and short-circuit when every widget is reference-equal to the old one. |
| 3.6 | Med | [Element.cs](Sway.Widgets/Foundation/Widget/Element.cs) `FindRenderObject`, `Unmount`; [RenderObject.cs](Sway.Widgets/Rendering/RenderObject/RenderObject.cs) `Attach`/`Detach`/`VisitChildren` | Each tree visit takes an `Action<>` that captures state (`found ??= ...`), so one closure is allocated per node per call. `Detach`/`Attach` are recursive over the whole subtree. | Replace the delegate visitors with a struct enumerator or `IReadOnlyList` children for hot paths (`FindRenderObject` is hit by `GestureBinding.HitsNode` on every press). |
| 3.7 | Low | [RenderBoxContainer.cs](Sway.Widgets/Rendering/RenderObject/RenderBoxContainer.cs) `SetChildren` | After the cheap equality check it still builds two `HashSet`s and a `ToArray` on any change. | For small lists compare by linear scan; build the sets only above ~16 children. |
| 3.8 | Low | [RenderFlex.cs](Sway.Widgets/Rendering/RenderFlex/RenderFlex.cs), [RenderGrid.cs](Sway.Widgets/Rendering/RenderLayouts/RenderGrid.cs), [RenderWrap.cs](Sway.Widgets/Rendering/RenderLayouts/RenderWrap.cs) | Non-lazy containers lay out and paint every child, and `RenderFlex` runs its intrinsic queries when `IntrinsicHeight/Width` is used. | `PaintingContext.PaintChild` already culls off-screen children with `QuickReject`, but layout still visits all of them. Document `ListView.Builder` / `GridView.Builder` for large data (already in `LIMITS.md`) and consider caching intrinsic results per constraint pair, invalidated by `MarkNeedsLayout`. |

---

## 4. Input and hit testing

| # | Impact | Where | Problem | Fix |
| --- | --- | --- | --- | --- |
| 4.1 ✅ | High | [GestureBinding.cs](Sway.Widgets/Widgets/Gestures/GestureBinding.cs) `PointerMove`, `AfterFrame` | A move triggers a hit test in `UpdateHover` (plus the stored path for dispatch). `AfterFrame` then hit-tests **again after every frame**, even during animation with a still pointer, and allocates two lists (`ToList`, `SequenceEqual`) each time. | Skip `AfterFrame` hover refresh unless layout ran this frame (`PipelineOwner` can expose a `DidLayout` flag). In `UpdateHover`, walk the path once with a `for` loop into a reusable list, and compare with the previous list without LINQ. |
| 4.2 | Med | [GestureBinding.cs](Sway.Widgets/Widgets/Gestures/GestureBinding.cs) `Dispatch`, `PointerScroll` | `path.ToArray()` per event copies the hit path each time. | Iterate by index; copy only if a handler can mutate the tree (the arena/handlers can re-enter, so snapshot into a pooled array, not a fresh one). |
| 4.3 | Low | [RenderView.cs](Sway.Widgets/Rendering/RenderBox/RenderView.cs) `HitTestAt` | New `HitTestResult` (and path list) per call. | Pool one result per binding and `Clear()` it. |
| 4.4 | Low | [RenderEditable.cs](Sway.Widgets/Rendering/RenderEditable.cs) `IndexAt` | Scans every grapheme boundary on the line for each pointer move while dragging a selection (and inherits 2.1). | Binary-search `XOf` over the line's monotonic positions (per bidi run), then refine with neighbours. |

---

## 5. Hosts and startup

| # | Impact | Where | Problem | Fix |
| --- | --- | --- | --- | --- |
| 5.1 | Med | [App.cs](Sway.Platform.Desktop/App.cs) render callback | The idle path polls with `Thread.Sleep(2)`, which wakes the process ~500 times a second while nothing changes. | Block on the window event queue (`window.IsEventDriven = true` and wake it from `OnFrameRequested`), or sleep until the next timer's due time. |
| 5.2 | Med | [App.cs](Sway.Platform.Desktop/App.cs) | `SyncBrightness` spawns helper processes on macOS/Linux (5 s poll, per `LIMITS.md`). | Use the OS change notification (registry watcher on Windows, `gsettings monitor` / `NSDistributedNotificationCenter`) instead of polling. |
| 5.3 | Med | [FontCache.cs](Sway.Widgets/Rendering/FontCache.cs) static constructor | All seven bundled faces (including both Noto Sans Arabic) are decoded in the type initializer, on the first frame's critical path. | Load lazily per face on first match in `FindRegistered`, and load the Arabic faces only when RTL text first appears (`TextShaper.Resolve` already asks for a fallback by code point). Part of the 150 ms cold start in `LIMITS.md`. |
| 5.4 | Low | Cold start in general | First frame of a 1500-row page is ~150 ms (`LIMITS.md`). | Profile first. Likely contributors are the font load above, `SKShaper` creation, and building every element of a non-lazy page; check that the demo uses `ListView.Builder` before blaming the framework. |
| 5.5 | Low | Android / Web hosts | Media frames are copied to a CPU bitmap each frame (`LIMITS.md`). | Upload to a GPU texture with `SKImage.FromTexture` where the backend allows it. |

---

## 6. Notes on the uncommitted change

The pending edits to [RenderEditable.cs](Sway.Widgets/Rendering/RenderEditable.cs) and
[EditableTextState.cs](Sway.Widgets/Widgets/EditableText/EditableTextState.cs) (gesture-arena participation so a
selection drag does not scroll the parent, and collapsing the selection on blur) are cheap. Two follow-ups:

- `AfterCaretMove()` runs on every drag `Move`, which calls `MarkNeedsPaint` and `OnSelectionChanged` even when the
  caret index did not change. Skip both when `MoveTo` leaves `Caret`/`Anchor` unchanged. Combined with 1.1 this stops
  pixel-by-pixel drags from repainting when the selection stays the same.
- The blur handler `Edit.MoveTo(Edit.Caret, false)` is followed by `SetState()`, which rebuilds the whole
  `EditableText` subtree. If only the selection highlight needs to disappear, calling
  `RenderEditable.MarkNeedsPaint()` is enough.

---

## Suggested order

1. ✅ **1.1, 4.1** (stop repainting and hit testing when nothing changed): small diffs, biggest idle and hover win.
2. **2.1** ✅, **2.2** ✅ (grapheme boundary cache, incremental line fitting): removes the quadratic behaviour in text fields.
3. **3.1, 3.2** (lazy list prefix sums and scroll without layout): needed for lists beyond a few thousand rows.
4. **1.2** (repaint boundaries): the large architectural change; do it once the cheap items are measured.
5. Everything marked Low, as part of normal edits to those files.

Measure each step with `dotnet run --project Sway.Example.Headless -- --page stress --bench`, and extend the bench
with a hover-only frame count, a 5k-character textarea edit, and a 100k-item variable-height scroll so these paths
have numbers.

---

## Completed

| # | What changed | Tests (`Sway.Widgets.Tests/HoverFrameTests.cs`) |
| --- | --- | --- |
| 1.1 | `PointerMove` requests a frame only when `UpdateHover` reports a hover-set or cursor change. State-changing handlers request their own frames. | `MovingOverNothingRequestsNoFrame`, `MovingInsideTheSameRegionRequestsNoFrame` (fail before, pass after); `EnteringAndLeavingARegionStillFiresCallbacksAndRequestsAFrame`, `CursorChangeRequestsAFrameSoTheHostCanApplyIt`, `OnHoverStillFiresOnEveryMoveInsideTheRegion` (pass before and after: behaviour that must not regress). |
| 4.1 | `AfterFrame` re-hit-tests only when `PipelineOwner.DidLayout` is set; hover diffing uses loops instead of LINQ. | `AFrameWithoutLayoutDoesNotHitTestAgain` (fails before, passes after); `LayoutMovingAWidgetFromUnderAStillPointerStillRefreshesHover` (passes before and after). |
| 2.1 | `PreviousBoundary`/`NextBoundary` use a per-edit cached boundary array (binary search), with an identity fast path for plain text (no marks, surrogates or CR). | `GraphemeBoundaryTests`: every index of 9 samples (ASCII, CRLF, combining, ZWJ family, Arabic, CJK) matches a fresh `ParseCombiningCharacters` scan, and the cache follows `Insert`/`Undo`/`SetValueExternal`. These pin behaviour and pass before and after. |
| 2.2 | `FitLine` gallops (doubling probes) to the first overflowing prefix, then binary-searches the exact break, measuring only at grapheme boundaries; the whole-paragraph measure per line is gone. | `LineFittingTests`: line breaks for 7 samples (sentences, one long word, combining marks and emoji, paragraphs, runs of spaces, short, empty) equal the output captured from the old algorithm, and every wrapped line fits. Pass before and after. |

### Measured

`dotnet run -c Release --project Sway.Example.Headless -- --bench` ends with two scenarios on a static synthetic page
(60 hoverable rows; the demo pages all animate, so they never go idle and cannot show these savings). Three runs each,
CPU raster, 1100x760:

| Scenario | Before | After |
| --- | --- | --- |
| Hover host loop, 1200 moves (frames drawn) | 1200 | 2 |
| Hover host loop, cost per move | 1.14-1.16 ms | 0.009-0.011 ms (about 100x less) |
| Still-pointer clock-only frame, avg | 0.91-0.98 ms | 0.98-1.11 ms (no measurable change) |
| 2.1 Grapheme walk, 5k-char ASCII field (10010 steps) | 950-980 ms | 0.52-0.55 ms (about 1800x less) |
| 2.1 Grapheme walk, 3.2k-char field with combining marks and emoji | 280-310 ms | 0.68-0.71 ms (about 430x less) |
| 2.2 Textarea keystroke frame, 5k-char single paragraph, 400 px wide | 11.7-13.2 ms | 4.9-5.7 ms (about 2.4x less) |
| 2.2 Textarea first layout, same text | 11.6-16.7 ms | 5.9-6.7 ms |

4.1 shows no gain on this small page: its saved hit test is microseconds here. It matters on deep trees; add a
large-tree scenario before claiming a number for it.

The remaining ~5 ms per keystroke is mostly 2.3 (every edit re-shapes every line) and 2.4 (no LTR text cache).

Rule for the next items: add a bench scenario with before/after numbers, a test that fails on the old code and passes on the new, plus tests pinning the behaviour that must not change.
