# dataflow-pipeline
[![Steelax.DataflowPipeline](https://img.shields.io/nuget/v/Steelax.DataflowPipeline.svg)](https://www.nuget.org/packages/Steelax.DataflowPipeline) [![Steelax.DataflowPipeline](https://img.shields.io/nuget/dt/Steelax.DataflowPipeline.svg)](https://www.nuget.org/packages/Steelax.DataflowPipeline/)

DataflowPipeline is a standard library that allows you to create a Pipeline which you can feed data into and process it by adding Dataflow blocks to it.

## Supported Runtimes
- .NET 8.0+

## Runtime Installation

All stable packages are available on [NuGet](https://www.nuget.org/packages/Steelax.DataflowPipeline/).

## Basic usage

### Configure your pipeline and run
```
see tests for examples
```

### You can create your own dataflow block with simple interfaces
```
interface IDataflowAction<TInput>;
interface IDataflowBreader<TOutput>;
interface IDataflowPipe<TInput, TOutput>;
interface IDataflowPipe<T>;
```

### Note
It is based on IAsyncEnumerator and uses a pull model.