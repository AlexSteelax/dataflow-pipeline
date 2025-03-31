namespace Steelax.DataflowPipeline.Common.Delegates;

public delegate TIndex SplitIndexHandle<in T, out TIndex>(T value, int index);