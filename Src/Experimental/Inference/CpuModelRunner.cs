// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.Diagnostics;
using Experimental.Controls;
using Microsoft.ML.OnnxRuntime;
using Resources;

namespace Experimental.Inference;

public class CpuModelRunner : ModelRunner
{
    public readonly Executor<(float[] Data, int[] Shape), (float[], int[])> Executor;
    public readonly Controller? Controller;

    public CpuModelRunner(Model model, ModelPrecision precision, InferenceSession session)
        : this(session, 1)  // TODO: horrible hack
    {
        Controller = new Controller(Executor, 3, new InferenceTelemetryRecorder(model, precision));
        Controller.Tune(CancellationToken.None);
    }

    public CpuModelRunner(InferenceSession inferenceSession, int initialParallelism) : base(inferenceSession)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(initialParallelism);

        Executor = new Executor<(float[] Data, int[] Shape), (float[], int[])>(input =>
        {
            var (data, shape) = RunInferenceInternal(input.Data, input.Shape);
            Debug.Assert(shape[0] == 1); // Batch size is always 1 on CPU
            var unbatchedShape = shape[1..]; // Strip batch size dimension that we added earlier
            return (data, unbatchedShape);
        }, initialParallelism);
    }

    public override Task<Task<(float[] Data, int[] Shape)>> Run(float[] batch, int[] shape)
    {
        Debug.Assert(shape.Length > 1);  // At least one dimension
        var batchedShape = new[] { 1 }.Concat(shape).ToArray();  // Add a batch size dimension. On CPU we don't batch, so this is just 1
        return Executor.ExecuteSingle((batch, batchedShape));
    }

    protected override ValueTask SubclassDisposeAsync() => ValueTask.CompletedTask;
}
