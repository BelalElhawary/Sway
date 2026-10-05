---
sidebar_position: 4
---

# Widgets

Widgets are constructed with named arguments and collection expressions. Everything lives in the `Sway.Widgets`
namespace.

## Layout

Constraints go down and sizes come back up, as in Flutter: a parent gives each child `BoxConstraints` (min and max
width and height) and the child picks a size inside them.

| Widget | Purpose |
| --- | --- |
| `Container` | Padding, margin, size, constraints, alignment, decoration and transform in one widget. |
| `Row`, `Column`, `Flex` | Linear layout with `MainAxisAlignment`, `CrossAxisAlignment` (including `Baseline`), `MainAxisSize`, `spacing` and `VerticalDirection`. |
| `Expanded`, `Flexible`, `Spacer` | Share leftover space on the main axis by flex factor. |
| `Stack`, `Positioned`, `PositionedDirectional` | Overlap children and place them by edge. |
| `Wrap` | Flow children into runs with `spacing`, `runSpacing` and alignment. |
| `Grid`, `GridItem` | CSS-grid-style tracks. See below. |
| `Padding`, `Align`, `Center` | Inset or position a child. |
| `SizedBox`, `ConstrainedBox`, `LimitedBox`, `FractionallySizedBox`, `AspectRatio` | Impose size rules. |
| `IntrinsicWidth`, `IntrinsicHeight` | Size to the child's natural size. |
| `ListView`, `ListView.Builder`, `SingleChildScrollView` | Scrolling. Builder lists only create visible rows. |

### Grid

```csharp
new Grid(
    columns: [GridTrack.Fr(1), GridTrack.Fr(2), GridTrack.Px(120)],
    columnGap: 8, rowGap: 8,
    children:
    [
        new GridItem(header, column: 0, row: 0, columnSpan: 3),
        new GridItem(sidebar, column: 0, row: 1, rowSpan: 2),
        a, b, c, d,                                   // placed row by row in the free cells
        new GridItem(badge, alignment: Alignment.Center),
    ])

Grid.Count(3, children, gap: 8)           // three equal columns
Grid.AutoFill(110, children, gap: 8)      // as many columns as fit, each at least 110 px
```

Tracks are `GridTrack.Px(n)`, `GridTrack.Fr(n)` (a share of the free space) or `GridTrack.Auto` (sized by content).
Items stretch to their cell unless you give `GridItem` an alignment.

## Painting and effects

| Widget | Purpose |
| --- | --- |
| `DecoratedBox`, `ColoredBox` | Fill with a colour or `BoxDecoration`: colour, `Border`, `BorderRadius`, `BoxShadow` list, `LinearGradient`, `RadialGradient`, `SweepGradient`, `BoxShape.Circle`. |
| `Opacity` | Fade a subtree. |
| `ClipRRect`, `ClipRect`, `ClipOval` | Clip children. |
| `Transform` | Rotate, scale, translate or apply any `SKMatrix` about an origin. Paints and hit-tests correctly; layout keeps the original size. |
| `ImageFiltered` | Blur and `ColorFilters` (grayscale, sepia, saturate, brightness, contrast, hue-rotate, invert). |
| `CustomPaint` with `CustomPainter` | Draw directly with Skia. |
| `Icon`, `Icons` | Vector icons from SVG path data on a 24x24 grid. |

```csharp
new Container(
    padding: EdgeInsets.All(16),
    decoration: new BoxDecoration(
        Gradient: new LinearGradient([Colors.Indigo, Colors.Pink], Alignment.TopLeft, Alignment.BottomRight),
        BorderRadius: BorderRadius.Circular(16),
        BoxShadow: Elevation.Shadows(3, Colors.Black)),
    child: new Text("Hello"))
```

## Text

`Text` takes a `TextStyle` (colour, size, weight, italic, family, line-height multiplier, letter spacing, decoration,
shadows), `maxLines`, `overflow: TextOverflow.Ellipsis`, `textAlign` and `softWrap`. Rich text uses spans:

```csharp
new Text(new TextSpan("Rich ", new TextStyle(FontSize: 18), [
    new TextSpan("bold ", new TextStyle(FontWeight: FontWeight.Bold)),
    new TextSpan("italic", new TextStyle(Italic: true)),
]))
```

Unset style fields inherit from the enclosing `DefaultTextStyle`. Text is shaped with HarfBuzz and handles mixed
left-to-right and right-to-left runs.

## Right-to-left

`Directionality(TextDirection.Rtl, child)` mirrors a subtree: rows reverse, text aligns to the end edge, and
`EdgeInsetsDirectional`, `AlignmentDirectional` and `PositionedDirectional` resolve start and end against it.
`MaterialApp(textDirection: ...)` sets it for the whole app.

## Overlays

`Overlay` (installed by the app root) layers widgets above the page. `Dialogs.Show` and `Dialogs.ShowSnackBar` use it,
and so does the dropdown menu. Insert your own with `Overlay.Of(context).Insert(new OverlayEntry(...))`.

## Media and file pickers

`Sway.Media` is platform-neutral. Each host installs the engine it can run, once at startup, and the widgets work the same
everywhere:

| Platform | Install | Engine |
| --- | --- | --- |
| Windows, Linux, macOS, Android | `LibVlcMediaBackend.Install();` (`Sway.Media.LibVlc`) | LibVLC |
| Browser | `await BrowserMediaBackend.InstallAsync();` (`Sway.Media.Web`) | HTML media element |

```csharp
new VideoPlayer(source: "movie.mp4", autoPlay: true)          // picture + controls, owns its player
new AudioPlayer(source: "song.mp3", title: "Song", subtitle: "Artist")

var player = new MediaPlayerController();                      // or drive one yourself
new VideoPlayer(controller: player);
```

`VideoSurface` is the bare picture, and `MediaBuilder` rebuilds a widget as the player advances. To support another
platform, implement `IMediaBackend` and pass it to `MediaBackend.Install`.

`FilePicker` opens the platform's own dialogs. Each host installs its implementation of `IFilePickerSource` itself, so the call
is the same everywhere:

```csharp
FilePicker.PickFiles(files => { if (files.Count > 0) player.Open(files[0].Source); },
    new FilePickerOptions { Filters = [FileTypeFilter.Media] });

FilePicker.PickFolder(async folder => { var files = await folder!.GetFilesAsync(FileTypeFilter.Audio); /* ... */ });
```

The callbacks run on the UI thread. A picked file's `Source` is what you hand to a player: a path on desktop, a `content://` URI on
Android and a `blob:` URL in the browser. Use `PickedFile.OpenReadAsync()` to read the bytes on any platform. `WidgetsBinding.Post`
runs an action on the UI thread from any other thread.
