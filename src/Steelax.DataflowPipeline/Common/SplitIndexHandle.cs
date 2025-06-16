namespace Steelax.DataflowPipeline.Common;

public delegate TIndex SplitIndexHandle<in T, out TIndex>(T value, int index);