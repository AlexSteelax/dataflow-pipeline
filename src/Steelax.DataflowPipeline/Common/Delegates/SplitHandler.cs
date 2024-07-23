namespace Steelax.DataflowPipeline.Common.Delegates;

public delegate int SplitHandler<in TValue>(TValue value, int index);