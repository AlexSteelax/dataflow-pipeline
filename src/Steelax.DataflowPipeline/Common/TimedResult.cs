namespace Steelax.DataflowPipeline.Common;

public readonly struct TimedResult<T> : IEquatable<TimedResult<T>>
{
    private readonly bool _timeout;
    private readonly T? _value;

    public TimedResult()
    {
        _timeout = true;
        _value = default;
    }

    public TimedResult(T value)
    {
        _timeout = false;
        _value = value;
    }

    public bool Empty => _timeout;
    public T Value => _timeout ? throw new InvalidOperationException() : _value!;

    public static bool operator ==(TimedResult<T> left, TimedResult<T> right) => left.Equals(right);
    public static bool operator !=(TimedResult<T> left, TimedResult<T> right) => !left.Equals(right);

    public bool Equals(TimedResult<T> other) => !_timeout && !other._timeout && _value!.Equals(other._value);
    public override bool Equals(object? obj) => obj is TimedResult<T> other && Equals(other);
    
    public override int GetHashCode() => _timeout ? 0 : _value!.GetHashCode();
}