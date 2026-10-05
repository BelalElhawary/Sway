---
sidebar_position: 5
---

# Theming

Material 3 is the default theme. Without any setup, widgets read `Theme.Of(context)` and fall back to the baseline
light theme; `MaterialApp` installs a theme and switches between light and dark.

```csharp
App.Run(new MaterialApp(
    home: new MyHome(),
    theme: ThemeData.FromSeed(Colors.FromRgb(0x006A6A)),
    darkTheme: ThemeData.FromSeed(Colors.FromRgb(0x006A6A), Brightness.Dark),
    themeMode: ThemeMode.System), "App", 900, 700);
```

`ThemeMode.System` follows the OS preference, read from the Windows setting at start-up; `WidgetsBinding.PlatformBrightness`
overrides it.

## Colour scheme

`ColorScheme` holds the Material 3 roles: `Primary`, `OnPrimary`, `PrimaryContainer`, `Secondary`, `Tertiary`, `Error`
(each with on-colour and container variants), `Surface`, `OnSurface`, `SurfaceVariant`, `Outline`, the
`SurfaceContainer*` ramp, `InverseSurface` and more.

- `ColorScheme.Light` and `ColorScheme.Dark` are the exact baseline (purple) schemes.
- `ColorScheme.FromSeed(color, brightness)` generates a full scheme from one colour. It approximates Material's tonal
  palettes, so results are close to Flutter's but not identical.
- Schemes are records, so tweak one role with `with`:
  `ColorScheme.Light with { Primary = Colors.Teal }`.

Read colours in widgets, not constants, so dark mode and seeding just work:

```csharp
var s = Theme.Of(context).ColorScheme;
new Container(color: s.SurfaceContainerHigh, child: new Text("Hi", style: new TextStyle(Color: s.OnSurface)))
```

## Typography

`TextTheme` is the Material 3 type scale: `DisplayLarge/Medium/Small`, `HeadlineLarge/Medium/Small`,
`TitleLarge/Medium/Small`, `BodyLarge/Medium/Small` and `LabelLarge/Medium/Small`. Each is a `TextStyle`.

```csharp
new Text("Heading", style: Theme.Of(context).TextTheme.HeadlineMedium)
```

Roboto is used when installed, with Segoe UI as the fallback. To change a style, merge over it:
`theme.TextTheme.BodyLarge.Merge(new TextStyle(Color: s.Primary))`. Remember that the argument to `Merge` wins.

## Shape and elevation

`Shapes` holds the corner scale (`ExtraSmall` 4, `Small` 8, `Medium` 12, `Large` 16, `ExtraLarge` 28, `Full`).
`Elevation.Shadows(level, color)` returns the two-layer Material shadow for levels 0 to 5; `Material` and `Card` use it.

## Shape and density

`ThemeData.Shape` is a `ShapeTheme`: the corner radii (`ExtraSmall` to `ExtraLarge`, plus `Button` and `Round`), the
`ControlHeight` of buttons, the `FieldHeight` of text fields, the `ButtonPadding`, `Flat`, which removes every drop
shadow, and the checkbox and switch settings below. The defaults are Material 3. Widgets read these instead of constants, so a theme can change the whole look:

```csharp
var compact = ThemeData.Light() with { Shape = new ShapeTheme { ControlHeight = 32, Button = 4 } };
```

## IBM Carbon

The optional `Sway.Extras.Ibm` package adds [Carbon](https://carbondesignsystem.com), IBM's design system, as a theme:
the four Carbon colour themes (`White`, `Gray10`, `Gray90`, `Gray100`), the IBM Plex type scale and Carbon's square,
flat, 40px shape. The IBM Plex fonts are embedded (SIL Open Font License), so the text looks the same everywhere.

```csharp
using Sway.Extras.Ibm;

App.Run(new MaterialApp(
    home: new MyHome(),
    theme: IbmTheme.White(),
    darkTheme: IbmTheme.Gray100()), "App", 900, 700);
```

`IbmTheme.For(brightness, gray: true)` picks the tinted variant.

Carbon needs a few things Material does not, and each is a general theme setting you can use yourself:

- `ColorScheme.Accent` is the colour for primary-coloured text, outlines and icons (text and outlined buttons).
  It defaults to `Primary`. Carbon's dark themes set it to a lighter blue, because the button blue is too dim as text.
- `ShapeTheme.CheckboxRadius`, `ControlHalo` and `CompactSwitch` give square checkboxes, a focus border instead of
  the round hover halo, and a flat 48x24 switch.

### Data tables

`Sway.Extras.Ibm` also has the widgets Carbon is known for in data-heavy apps: `DataTable<T>`, `Pagination` and `Tag`.

```csharp
new DataTable<Server>(
    columns:
    [
        DataColumn<Server>.By("Name", r => r.Name, width: 160),
        new DataColumn<Server>("Status", r => r.Status, Width: 140, Cell: r => new Tag(r.Status, TagColor.Green)),
        DataColumn<Server>.By("CPU", r => r.Cpu, v => $"{v}%", align: TextAlign.End),
    ],
    rows: servers, title: "Servers", size: TableSize.Medium,
    selectable: true, searchable: true, pageSize: 10, maxBodyHeight: 480,
    batchActions: [new BatchAction<Server>("Delete", rows => Remove(rows), Icons.Delete)],
    onRowTap: row => Open(row));
```

- Search, sorting (click a header: ascending, descending, off), selection and paging are handled inside the table. You
  supply the rows and hear about changes through `onSelectionChanged` and `onRowTap`.
- `DataColumn<T>.By` sorts by a key (numbers, dates) instead of by the cell text; use the plain constructor for text
  columns, and its `Cell` to draw something other than text.
- `TableSize` is Carbon's row height: `ExtraSmall` 24, `Small` 32, `Medium` 40, `Large` 48, `ExtraLarge` 64.
- Rows are virtualised and the header stays put. Without `maxBodyHeight` the table is as tall as the rows on the page.
- Selecting a row swaps the toolbar for a blue batch bar with your `batchActions` and a Cancel button.
- Pass `rowDetail` to make rows expandable: a chevron appears in front of each row and the panel you build shows
  under the open row, `detailHeight` tall (96 by default).
- Columns that cannot all fit make the table scroll sideways, header and rows together, while the toolbar and pager stay
  put. A flexible column never gets narrower than its `MinWidth` (120 by default). A vertical mouse wheel over the table
  still scrolls the page.
- The header checkbox shows a dash when only some rows are selected.

`Pagination` also works alone (`totalItems`, zero-based `page`, `pageSize`, and callbacks) and drops its wordier parts
on narrow screens. `Tag` has the Carbon colours (`TagColor`), an outline style, an optional close button and a `compact`
18px size for dense rows.

Two small additions to the core widgets came with this: `Checkbox(indeterminate: true)` for the dash, and
`SingleChildScrollView(wheelScrollsOtherAxis: false)`, which stops a horizontal scroller from taking a vertical mouse
wheel meant for the page.

## Components

| Group | Widgets |
| --- | --- |
| Buttons | `FilledButton`, `FilledTonalButton`, `ElevatedButton`, `OutlinedButton`, `TextButton` (all with an optional icon), `IconButton` (standard, filled, tonal, outlined), `FloatingActionButton` |
| Selection | `Checkbox`, `Radio<T>`, `Switch` |
| Input | `TextField` (outlined or filled, floating label, helper and error text, prefix and suffix), `DropdownButton<T>` |
| Surfaces | `Card` (elevated, filled, outlined), `Material`, `Divider`, `ListTile`, `Chip` |
| Structure | `Scaffold`, `AppBar`, `NavigationRail`, `NavigationBar` |
| Feedback | `LinearProgressIndicator`, `CircularProgressIndicator` (determinate or indeterminate), `AlertDialog` with `Dialogs.Show`, `Dialogs.ShowSnackBar` |

Controls show Material state layers for hover, focus and press and a focus ring when focused from the keyboard. They
do not draw ripples. Pass a null callback to disable a control.

Build your own interactive controls with `Interactive`, which tracks hover, press and keyboard focus and handles
Space and Enter:

```csharp
new Interactive((ctx, state) => new Container(
    color: state.Hover ? s.PrimaryContainer : s.SurfaceContainer,
    child: new Text("Custom")), onTap: () => { });
```
