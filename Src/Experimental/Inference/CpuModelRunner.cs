// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.Diagnostics;
using System.Threading.Tasks.Dataflow;
using Microsoft.ML.OnnxRuntime;

namespace Experimental.Inference;

public class CpuModelRunner : ModelRunner
{
    private readonly TransformBlock<(float[], int[]), (float[], int[])> _inferenceRunnerBlock;
    private readonly Task _inputsToInferenceBlockTask;
    private readonly Task _inferenceBlockToOutputsTask;

    public CpuModelRunner(InferenceSession inferenceSession, int maxParallelism) : base(inferenceSession)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxParallelism);

        _inferenceRunnerBlock = new TransformBlock<(float[] Data, int[] Shape), (float[], int[])>(item => RunInference(item.Data, item.Shape),
            new ExecutionDataflowBlockOptions
            {
                MaxDegreeOfParallelism = maxParallelism,
                BoundedCapacity = maxParallelism
            });
        _inputsToInferenceBlockTask = InputsToInferenceBlock();
        _inferenceBlockToOutputsTask = InferenceBlockToOutputs();
    }

    private async Task InputsToInferenceBlock()
    {
        await foreach (var (data, shape) in Inputs.Reader.ReadAllAsync())
        {
            Debug.Assert(shape.Length > 1);  // At least one dimension
            var batchedShape = new[] { 1 }.Concat(shape).ToArray();  // Add a batch size dimension. On CPU we don't batch, so this is just 1
            await _inferenceRunnerBlock.SendAsync((data, batchedShape));
        }

        _inferenceRunnerBlock.Complete();
    }

    private async Task InferenceBlockToOutputs()
    {
        var actionBlock = new ActionBlock<(float[] Data, int[] Shape)>(async item =>
        {
            Debug.Assert(item.Shape[0] == 1);  // We don't batch on CPU, so this should be 1
            var unbatchedShape = item.Shape[1..];  // Strip batch dimension
            await Outputs.Writer.WriteAsync((item.Data, unbatchedShape));
        }, new ExecutionDataflowBlockOptions { BoundedCapacity = 1 });

        _inferenceRunnerBlock.LinkTo(actionBlock, new DataflowLinkOptions { PropagateCompletion = true });

        await actionBlock.Completion;
    }

    protected override async ValueTask SubclassDisposeAsync()
    {
        await _inputsToInferenceBlockTask;
        await _inferenceBlockToOutputsTask;
    }
}
