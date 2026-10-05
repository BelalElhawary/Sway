using SkiaSharp;

namespace Sway.Widgets;

/// <summary>Drives <see cref="AnimatedList"/>: insert and remove items with an animation.</summary>
public sealed class AnimatedListController
{
    internal AnimatedListState? State;

    public int Count => State?.LiveCount ?? 0;

    /// <summary>Animates in the item that the list's builder now returns for <paramref name="index"/>.</summary>
    public void InsertItem(int index, TimeSpan? duration = null) => Attached.Insert(index, duration);

    /// <summary>
    /// Animates out the item at <paramref name="index"/>. <paramref name="removedBuilder"/> must draw the item as it was
    /// (the list's builder no longer has it); its animation runs from 1 to 0.
    /// </summary>
    public void RemoveItem(int index, Func<BuildContext, Animation<float>, Widget> removedBuilder, TimeSpan? duration = null) =>
        Attached.Remove(index, removedBuilder, duration);

    AnimatedListState Attached => State ?? throw new InvalidOperationException("The controller is not attached to an AnimatedList.");
}
