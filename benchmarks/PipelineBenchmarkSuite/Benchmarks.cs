using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;

namespace PipelineBenchmarkSuite
{
    [SimpleJob(RunStrategy.Monitoring, iterationCount: 1), MemoryDiagnoser]
    public class Benchmarks
    {
        [Benchmark]
        public void Scenario1()
        {
            // var source = Channel.CreateUnbounded<object>();
            //
            // var pipeline = source.Reader
            //     .UseAsDataflowSource()
            //     .Batch(10)
            //     .Split(
            //         s => s.AsUnbounded().End(),
            //         s => s.AsUnbounded().End());
            //
            //
            // pipeline.InvokeAsync();



            // Implement your benchmark here
        }
    }
}
