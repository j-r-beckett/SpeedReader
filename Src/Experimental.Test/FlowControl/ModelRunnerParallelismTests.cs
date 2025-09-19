// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using Experimental.Inference;
using Microsoft.ML.OnnxRuntime;

namespace Experimental.Test.FlowControl;

public class ModelRunnerParallelismTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task CpuModelRunner_RespectsMaxParallelism(int maxParallelism)
    {
        var tcs = new TaskCompletionSource();

        var runner = new BlockingCpuModelRunner(null!, maxParallelism, tcs.Task);

        // Start 2x maxParallelism tasks
        var results = Enumerable.Range(0, 2 * maxParallelism).Select(_ => runner.Run([0], [1, 1])).ToList();

        await Task.Delay(100);

        var observedParallelism = runner.Parallelism;

        tcs.SetResult();

        await Task.WhenAll(results);

        // Verify that exactly maxParallelism tasks were observed running in parallel
        Assert.Equal(maxParallelism, observedParallelism);
    }
}

public class BlockingCpuModelRunner : CpuModelRunner
{
    private readonly Task _block;

    public int Parallelism;

    public BlockingCpuModelRunner(InferenceSession session, int maxParallelism, Task block)
        : base(session, maxParallelism) => _block = block;

    protected override (float[], int[]) RunInferenceInternal(float[] batch, int[] shape)
    {
        Interlocked.Increment(ref Parallelism);

        _block.GetAwaiter().GetResult();

        Interlocked.Decrement(ref Parallelism);

        return ([0], [1, 1]);
    }
}
