namespace Sway.Widgets;

public sealed class ValueKey<T>(T value) : Key where T : notnull
{
    public T Value { get; } = value;
    public override bool Equals(object? obj) => obj is ValueKey<T> k && EqualityComparer<T>.Default.Equals(Value, k.Value);
    public override int GetHashCode() => HashCode.Combine(typeof(T), Value);
}
