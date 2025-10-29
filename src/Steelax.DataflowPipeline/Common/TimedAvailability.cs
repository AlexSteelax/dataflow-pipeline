using JetBrains.Annotations;

namespace Steelax.DataflowPipeline.Common;

/// <summary>
/// Represents the availability of a value at a specific point in time, with support for timeout scenarios.
/// This structure is useful for operations that wait for a value with a timeout, indicating whether
/// the value was available at the specified timestamp or if a timeout occurred.
/// </summary>
/// <typeparam name="T">The type of the value</typeparam>
/// <remarks>
/// The structure can be in one of two states:
/// - Available: Contains a value and the timestamp when it became available
/// - Timeout: Contains only a timestamp indicating when the timeout occurred
/// </remarks>
public readonly struct TimedAvailability<T> : IEquatable<TimedAvailability<T>>
{
    private readonly DateTimeOffset _timestamp;
    private readonly bool _isAvailable;
    private readonly T? _value;

    /// <summary>
    /// Initializes a new instance of the <see cref="TimedAvailability{T}"/> structure with default values.
    /// This constructor creates a timeout state with minimal timestamp and should typically not be used directly.
    /// Prefer using the <see cref="Available"/> or <see cref="Timeout"/> factory methods instead.
    /// </summary>
    public TimedAvailability() : this(DateTimeOffset.MinValue, false) { }

    private TimedAvailability(DateTimeOffset timestamp, bool isAvailable, T? value = default)
    {
        _timestamp = timestamp;
        _isAvailable = isAvailable;
        _value = value;
    }
    
    /// <summary>
    /// Creates a <see cref="TimedAvailability{T}"/> instance representing a successfully available value.
    /// </summary>
    /// <param name="value">The value that became available</param>
    /// <param name="timestamp">The timestamp when the value became available</param>
    /// <returns>A new <see cref="TimedAvailability{T}"/> instance in the available state</returns>
    [PublicAPI]
    public static TimedAvailability<T> Available(T value, DateTimeOffset timestamp)
        => new(timestamp, true, value);
    
    /// <summary>
    /// Creates a <see cref="TimedAvailability{T}"/> instance representing a timeout scenario.
    /// </summary>
    /// <param name="timestamp">The timestamp when the timeout occurred</param>
    /// <returns>A new <see cref="TimedAvailability{T}"/> instance in the timeout state</returns>
    [PublicAPI]
    public static TimedAvailability<T> Timeout(DateTimeOffset timestamp)
        => new(timestamp, false);
    
    /// <summary>
    /// Creates a new <see cref="TimedAvailability{T}"/> instance with the specified timestamp,
    /// preserving the availability state and value (if available) from the current instance.
    /// </summary>
    /// <param name="timestamp">The new timestamp to use</param>
    /// <returns>A new instance with the updated timestamp</returns>
    [PublicAPI]
    public TimedAvailability<T> WithTimestamp(DateTimeOffset timestamp)
        => new(timestamp, _isAvailable, _value);

    /// <summary>
    /// Gets a value indicating whether a value is available.
    /// </summary>
    /// <value><c>true</c> if a value is available; <c>false</c> if a timeout occurred</value>
    [PublicAPI]
    public bool IsAvailable => _isAvailable;
    
    /// <summary>
    /// Gets the timestamp associated with this availability result.
    /// For available values, this represents when the value became available.
    /// For timeouts, this represents when the timeout occurred.
    /// </summary>
    [PublicAPI]
    public DateTimeOffset Timestamp => _timestamp;
    
    /// <summary>
    /// Gets the value if available.
    /// </summary>
    /// <value>The value if <see cref="IsAvailable"/> is <c>true</c></value>
    /// <exception cref="InvalidOperationException">
    /// Thrown when <see cref="IsAvailable"/> is <c>false</c> (timeout occurred)
    /// </exception>
    [PublicAPI]
    public T Value => _isAvailable ? _value! : throw new InvalidOperationException("Value is not available.");
    
    /// <summary>
    /// Determines whether two <see cref="TimedAvailability{T}"/> instances are equal.
    /// </summary>
    /// <param name="left">The first instance to compare</param>
    /// <param name="right">The second instance to compare</param>
    /// <returns><c>true</c> if the instances are equal; otherwise, <c>false</c></returns>
    public static bool operator ==(TimedAvailability<T> left, TimedAvailability<T> right) => left.Equals(right);
    
    /// <summary>
    /// Determines whether two <see cref="TimedAvailability{T}"/> instances are not equal.
    /// </summary>
    /// <param name="left">The first instance to compare</param>
    /// <param name="right">The second instance to compare</param>
    /// <returns><c>true</c> if the instances are not equal; otherwise, <c>false</c></returns>
    public static bool operator !=(TimedAvailability<T> left, TimedAvailability<T> right) => !left.Equals(right);

    /// <summary>
    /// Indicates whether the current object is equal to another <see cref="TimedAvailability{T}"/> instance.
    /// </summary>
    /// <param name="other">An object to compare with this object</param>
    /// <returns><c>true</c> if the current object is equal to the <paramref name="other"/> parameter; otherwise, <c>false</c></returns>
    /// <remarks>
    /// Two instances are considered equal if they have the same timestamp, same availability state,
    /// and if available, the same value.
    /// </remarks>
    public bool Equals(TimedAvailability<T> other)
    {
        if (_timestamp != other._timestamp || _isAvailable != other._isAvailable)
            return false;

        return _isAvailable && (_value is not null && _value.Equals(other._value) || _value is null && other._value is null);
    }
    
    /// <summary>
    /// Determines whether the specified object is equal to the current object.
    /// </summary>
    /// <param name="obj">The object to compare with the current object</param>
    /// <returns><c>true</c> if the specified object is equal to the current object; otherwise, <c>false</c></returns>
    public override bool Equals(object? obj) => obj is TimedAvailability<T> other && Equals(other);

    /// <summary>
    /// Serves as the default hash function.
    /// </summary>
    /// <returns>A hash code for the current object</returns>
    public override int GetHashCode() => HashCode.Combine(_timestamp, _value);
}