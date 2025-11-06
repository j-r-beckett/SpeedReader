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

    public CpuModelRunner(Model model, ModelPrecision modelPrecision, int initialParallelism)
        : base(InferenceSessionProvider.GetCpuInferenceSession(model, modelPrecision, 1))
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(initialParallelism);

        var telemetryTags = new Dictionary<string, string>
        {
            ["model"] = model.ToString(),
            ["precision"] = modelPrecision.ToString()
        };

        Executor = new Executor<(float[] Data, int[] Shape), (float[], int[])>(input =>
        {
            var (data, shape) = RunInferenceInternal(input.Data, input.Shape);
            Debug.Assert(shape[0] == 1); // Batch size is always 1 on CPU
            var unbatchedShape = shape[1..]; // Strip batch size dimension that we added earlier
            return (data, unbatchedShape);
        }, initialParallelism, telemetryTags);

        Controller = new Controller(Executor, 3, telemetryTags);
        // TODO: fix this later
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
        Controller.Tune(CancellationToken.None);
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
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
