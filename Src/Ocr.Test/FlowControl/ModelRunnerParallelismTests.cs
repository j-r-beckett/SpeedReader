// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using Ocr.Inference;
using Microsoft.ML.OnnxRuntime;

namespace Ocr.Test.FlowControl;

public class ModelRunnerParallelismTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task CpuModelRunner_RespectsMaxParallelism(int maxParallelism)
    {
        var tcs = new TaskCompletionSource();

        var parallelism = 0;

        // Simulate inference by waiting for tcs task to complete. Track number of ongoing simulated inferences by
        // incrementing/decrementing parallelism counter
        var infer = () =>
        {
            Interlocked.Increment(ref parallelism);
            tcs.Task.GetAwaiter().GetResult();
            Interlocked.Decrement(ref parallelism);
            return MockCpuModelRunner.SimpleResult;
        };

        var runner = new MockCpuModelRunner(infer, maxParallelism);

        // Start 2x maxParallelism tasks
        var results = Enumerable.Range(0, 2 * maxParallelism).Select(_ => runner.Run([0], [1, 1])).ToList();

        await Task.Delay(100);

        // Snapshot parallelism before we allow execution to proceed
        var observedMaxParallelism = parallelism;

        tcs.SetResult();

        // Ensure no exceptions were thrown
        await Task.WhenAll(results);

        // Verify that exactly maxParallelism tasks were observed running in parallel
        Assert.Equal(maxParallelism, observedMaxParallelism);
    }
}
