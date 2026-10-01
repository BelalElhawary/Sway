---
sidebar_position: 6
---

# Blazor hosting

Sway hosts the standard Blazor component model, but it is not a Blazor Server or WebAssembly host, so some
framework features that depend on a browser or a JS runtime aren't available.

| Limit | Notes |
| --- | --- |
| No JS interop | `IJSRuntime` is not registered. Components that inject it fail. This rules out `Virtualize`, `InputFile`, `FocusAsync`, and most third-party component libraries. |
| No routing | `@page` is ignored; `Router` and `NavigationManager` are not provided. Switch views by swapping components based on state, as the sample app's menu does. |
| `ElementReference` | `@ref` on elements is ignored (reference-capture frames are skipped). |
| Markup frames | Static HTML emitted by the Razor compiler is parsed by a minimal fragment parser: no implicit tag closing, no table fix-up, entities via `HtmlDecode` only. |
| `EditForm` and validation components | Untested. The submit event path works for a plain `<form @onsubmit>`. |
| Hot reload | Untested. |
| Service provider | Only logging is registered. Add services by extending `UiHost`. |

Everything else — data binding, event handlers, cascading parameters, dependency injection you register yourself,
component lifecycle methods — works the same as it does in any other Blazor host, because it's the same
`Microsoft.AspNetCore.Components` render tree underneath.
