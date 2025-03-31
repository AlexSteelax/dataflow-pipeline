using BenchmarkDotNet.Attributes;
using System.Threading.Channels;
using BenchmarkDotNet.Engines;
using Steelax.DataflowPipeline;

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
