namespace Sway.Widgets;

/// <summary>Key by reference identity of an object.</summary>
public sealed class ObjectKey(object value) : Key
{
    public object Value { get; } = value;
    public override bool Equals(object? obj) => obj is ObjectKey k && ReferenceEquals(Value, k.Value);
    public override int GetHashCode() => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(Value);
}
