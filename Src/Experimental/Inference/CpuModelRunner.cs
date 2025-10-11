// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.Diagnostics;
using Microsoft.ML.OnnxRuntime;

namespace Experimental.Inference;

public class CpuModelRunner : ModelRunner
{
    private readonly ParallelismManager<(float[], int[]), (float[], int[])> _parallelismManager;

    public CpuModelRunner(InferenceSession inferenceSession, int maxParallelism) : base(inferenceSession)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxParallelism);

        _parallelismManager = new ParallelismManager<(float[] Data, int[] Shape), (float[], int[])>(input =>
        {
            var (data, shape) = RunInferenceInternal(input.Data, input.Shape);
            Debug.Assert(shape[0] == 1); // Batch size is always 1 on CPU
            var unbatchedShape = shape[1..]; // Strip batch size dimension that we added earlier
            return (data, unbatchedShape);
        }, maxParallelism);
    }

    public override Task<Task<(float[] Data, int[] Shape)>> Run(float[] batch, int[] shape)
    {
        Debug.Assert(shape.Length > 1);  // At least one dimension
        var batchedShape = new[] { 1 }.Concat(shape).ToArray();  // Add a batch size dimension. On CPU we don't batch, so this is just 1
        return _parallelismManager.Call((batch, batchedShape));
    }

    protected override ValueTask SubclassDisposeAsync() => ValueTask.CompletedTask;
}
