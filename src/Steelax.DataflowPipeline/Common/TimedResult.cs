namespace Steelax.DataflowPipeline.Common;

public readonly struct TimedResult<T> : IEquatable<TimedResult<T>>
{
    private readonly DateTimeOffset _timestamp;
    private readonly T? _value;
    private readonly bool _expired;

    public TimedResult()
    {
        _expired = true;
        _value = default;
        _timestamp = DateTimeOffset.UtcNow;
    }
    
    public TimedResult(DateTimeOffset timestamp)
    {
        _expired = true;
        _value = default;
        _timestamp = timestamp;
    }

    public TimedResult(T value)
    {
        _expired = false;
        _value = value;
        _timestamp = DateTimeOffset.UtcNow;
    }
    
    public TimedResult(T value, DateTimeOffset timestamp)
    {
        _expired = false;
        _value = value;
        _timestamp = timestamp;
    }

    /// <summary>
    /// Timed out flag
    /// </summary>
    public bool Expired => _expired;
    
    /// <summary>
    /// Result timestamp
    /// </summary>
    public DateTimeOffset Timestamp => _timestamp;
    
    /// <summary>
    /// Result value
    /// </summary>
    /// <exception cref="InvalidOperationException">If it has expired flag</exception>
    public T Value => _expired ? throw new InvalidOperationException("Contains no value") : _value!;
    
    public TimedResult<T> WithTimestamp(DateTimeOffset timestamp) => Expired
        ? new TimedResult<T>(timestamp)
        : new TimedResult<T>(Value, timestamp);
    
    public static bool operator ==(TimedResult<T> left, TimedResult<T> right) => left.Equals(right);
    public static bool operator !=(TimedResult<T> left, TimedResult<T> right) => !left.Equals(right);

    public bool Equals(TimedResult<T> other) => !_expired && !other._expired && _value!.Equals(other._value) && _timestamp == other._timestamp;
    public override bool Equals(object? obj) => obj is TimedResult<T> other && Equals(other);
    
    public override int GetHashCode() => _expired ? 0 : HashCode.Combine(_value!, _timestamp);
}