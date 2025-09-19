// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.Diagnostics;
using System.Threading.Tasks.Dataflow;
using Microsoft.ML.OnnxRuntime;

namespace Experimental.Inference;

public class CpuModelRunner : ModelRunner
{
    private readonly ActionBlock<(TaskCompletionSource<(float[], int[])>, float[], int[])> _inferenceRunnerBlock;

    public CpuModelRunner(InferenceSession inferenceSession, int maxParallelism) : base(inferenceSession)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxParallelism);

        _inferenceRunnerBlock = new ActionBlock<(TaskCompletionSource<(float[], int[])> Tcs, float[] Data, int[] Shape)>(input =>
        {
            var (data, shape) = RunInferenceInternal(input.Data, input.Shape);
            Debug.Assert(shape[0] == 1);  // Batch size is always 1 on CPU
            var unbatchedShape = shape[1..];  // Strip batch size dimension that we added earlier
            input.Tcs.SetResult((data, unbatchedShape));
        }, new ExecutionDataflowBlockOptions
        {
            MaxDegreeOfParallelism = maxParallelism,
            BoundedCapacity = maxParallelism
        });
    }

    public override async Task<(float[] Data, int[] Shape)> Run(float[] batch, int[] shape)
    {
        Debug.Assert(shape.Length > 1);  // At least one dimension
        var tcs = new TaskCompletionSource<(float[], int[])>();
        var batchedShape = new[] { 1 }.Concat(shape).ToArray();  // Add a batch size dimension. On CPU we don't batch, so this is just 1
        await _inferenceRunnerBlock.SendAsync((tcs, batch, batchedShape));
        return await tcs.Task;
    }

    protected override async ValueTask SubclassDisposeAsync()
    {
        _inferenceRunnerBlock.Complete();
        await _inferenceRunnerBlock.Completion;
    }
}
