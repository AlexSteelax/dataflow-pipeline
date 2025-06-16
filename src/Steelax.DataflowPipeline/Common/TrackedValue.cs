namespace Steelax.DataflowPipeline.Common;

public readonly record struct TrackedValue<TValue>(TValue Value, DateTimeOffset Timestamp)
{
    public TrackedValue(TValue value) : this(value, DateTimeOffset.MinValue) { }
}