namespace Steelax.DataflowPipeline.Async;

/// <summary>
/// Defines how the merge operation handles exceptions from individual sources.
/// </summary>
public enum FaultToleranceMode
{
    /// <summary>
    /// Any exception (including OperationCanceledException) immediately stops the entire operation.
    /// Use case: Critical systems where partial results are unacceptable.
    /// </summary>
    Strict,

    /// <summary>
    /// OperationCanceledException is absorbed (source is removed), other exceptions stop the operation.
    /// Use case: Systems requiring graceful degradation during temporary cancellations.
    /// </summary>
    TolerantToCancellation,

    /// <summary>
    /// All exceptions are absorbed, operation continues with remaining sources.
    /// Use case: Best-effort data collection, monitoring, analytics.
    /// </summary>
    Resilient
}