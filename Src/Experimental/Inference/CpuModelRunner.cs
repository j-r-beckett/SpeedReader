// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.Threading.Tasks.Dataflow;
using Microsoft.ML.OnnxRuntime;

namespace Experimental.Inference;

public class CpuModelRunner : ModelRunner
{
    private readonly TransformBlock<float[], float[]> _inferenceRunnerBlock;
    private readonly Task _inputsToInferenceBlockTask;
    private readonly Task _inferenceBlockToOutputsTask;

    public CpuModelRunner(InferenceSession inferenceSession, int maxParallelism) : base(inferenceSession)
    {
        _inferenceRunnerBlock = new TransformBlock<float[], float[]>(RunInference,
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
        await foreach (var input in Inputs.Reader.ReadAllAsync())
        {
            await _inferenceRunnerBlock.SendAsync(input);
        }

        _inferenceRunnerBlock.Complete();
    }

    private async Task InferenceBlockToOutputs()
    {
        var actionBlock = new ActionBlock<float[]>(async result => await Outputs.Writer.WriteAsync(result),
            new ExecutionDataflowBlockOptions { BoundedCapacity = 1 });

        _inferenceRunnerBlock.LinkTo(actionBlock, new DataflowLinkOptions { PropagateCompletion = true });

        await actionBlock.Completion;
    }

    protected override async ValueTask SubclassDisposeAsync()
    {
        await _inputsToInferenceBlockTask;
        await _inferenceBlockToOutputsTask;
    }
}
