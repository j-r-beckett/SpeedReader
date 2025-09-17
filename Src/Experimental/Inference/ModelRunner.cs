// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.Diagnostics;
using System.Threading.Channels;
using Microsoft.ML.OnnxRuntime;

namespace Experimental.Inference;

public abstract class ModelRunner : IAsyncDisposable
{
    // Subclasses should read inputs from Inputs -> do inference -> write inference results to Outputs
    protected readonly Channel<(float[] Data, int[] Shape)> Inputs;

    protected readonly Channel<(float[] Data, int[] Shape)> Outputs;

    private readonly Channel<TaskCompletionSource<(float[], int[])>> _completions;
    private readonly AsyncLock _writeLock = new();  // Synchronizes writes to Inputs and _completions

    // Reads from Outputs and _completions, completes tasks returned to callers with results
    private readonly Task _outputCompletionTask;

    private readonly InferenceSession _inferenceSession;

    protected ModelRunner(InferenceSession inferenceSession)
    {
        Inputs = Channel.CreateUnbounded<(float[], int[])>();
        Outputs = Channel.CreateUnbounded<(float[], int[])>();
        _completions = Channel.CreateUnbounded<TaskCompletionSource<(float[], int[])>>();
        _outputCompletionTask = CompleteOutputs();
        _inferenceSession = inferenceSession;
    }

    public async Task<(float[], int[])> Run(float[] input, int[] shape)
    {
        Debug.Assert(shape.Length > 1);  // At least one dimension
        var tcs = new TaskCompletionSource<(float[], int[])>();
        await _writeLock.AcquireAsync(CancellationToken.None);
        try
        {
            await _completions.Writer.WriteAsync(tcs);  // unbounded, will not block
            await Inputs.Writer.WriteAsync((input, shape));  // bounded, may block
        }
        finally
        {
            _writeLock.Release();
        }

        return await tcs.Task;
    }

    private async Task CompleteOutputs()
    {
        await foreach (var (data, shape) in Outputs.Reader.ReadAllAsync())
        {
            if (!_completions.Reader.TryRead(out var tcs))
            {
                throw new Exception("This shouldn't happen");
            }

            tcs.SetResult((data, shape));
        }
    }

    protected (float[], int[]) RunInference(float[] batch, int[] shape)
    {
        Debug.Assert(shape.Length > 2);  // 1 batch size dimension, at least one other dimension
        var longShape = shape.Select(d => (long)d).ToArray();  // convert int[] -> long[]
        var inputTensor = OrtValue.CreateTensorValueFromMemory(batch, longShape);  // Instantiate ONNX tensor
        var inputs = new Dictionary<string, OrtValue> { { "input", inputTensor } };
        using var runOptions = new RunOptions();
        var outputs = _inferenceSession.Run(runOptions, inputs, _inferenceSession.OutputNames);  // Run inference
        var outputTensor = outputs[0];  // One input, so one output
        var outputShape = outputTensor.GetTensorTypeAndShape().Shape.Select(l => (int)l).ToArray();  // Convert output shape from long[] -> int[]
        var outputSize = outputShape.Aggregate(1, (prod, next) => prod * next);  // Multiply all shape dimensions together to get total element count
        var output = new float[outputSize];  // Instantiate an output buffer
        outputTensor.GetTensorDataAsSpan<float>().CopyTo(output);  // Copy to output buffer
        return (output, outputShape);  // Return output buffer, shape
    }

    public async ValueTask DisposeAsync()
    {
        Inputs.Writer.Complete();
        await Inputs.Reader.Completion;

        Outputs.Writer.Complete();
        await Outputs.Reader.Completion;

        await _outputCompletionTask;

        await SubclassDisposeAsync();

        GC.SuppressFinalize(this);
    }

    protected abstract ValueTask SubclassDisposeAsync();
}
