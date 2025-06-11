namespace Steelax.DataflowPipeline.DefaultBlocks.Algorithmic;

public readonly record struct TrackedValue<TValue>(TValue Value, DateTimeOffset Timestamp)
{
    public TrackedValue(TValue value) : this(value, DateTimeOffset.MinValue) { }
}