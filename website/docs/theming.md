---
sidebar_position: 5
---

# Theming

`Sway.Widgets` has no look of its own. A design system is a separate package that brings its own theme types and
components, the way Flutter splits `widgets` from `material` and `cupertino`. This page covers Material 3, in
`Sway.Extras.Material3` (add a project reference and `using Sway.Extras.Material3;`). IBM Carbon, in `Sway.Extras.Ibm`,
is on [its own page](./carbon) and does not use any of this.

In Material 3, widgets read `Theme.Of(context)` and fall back to the baseline light theme; `MaterialApp` installs a
theme and switches between light and dark.

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
