using Sway.Widgets;

namespace Sway.Extras.Material3;

/// <summary>Supplies a <see cref="ThemeData"/> to the subtree. Without one, widgets use the default Material 3 light theme.</summary>
public sealed class Theme(ThemeData data, Widget child, Key? key = null) : InheritedWidget(child, key)
{
    public ThemeData Data { get; } = data;
    public override bool UpdateShouldNotify(InheritedWidget old) => !ReferenceEquals(((Theme)old).Data, Data) && !((Theme)old).Data.Equals(Data);

    static readonly ThemeData Fallback = ThemeData.Light();

    /// <summary>The nearest theme; rebuilds the caller when the theme changes.</summary>
    public static ThemeData Of(BuildContext context) => context.DependOn<Theme>()?.Data ?? Fallback;
}
