using Sway.Widgets;

namespace Sway.Extras.Material3;

/// <summary>Base type for app-defined theme data. Derive a record, add it with <see cref="ThemeData.WithExtensions"/>, read it with <c>Theme.Of(context).Extension&lt;T&gt;()</c>.</summary>
public abstract record ThemeExtension;
