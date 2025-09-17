// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.Threading.Channels;
using Microsoft.ML.OnnxRuntime;

namespace Experimental.Inference;

public abstract class ModelRunner : IAsyncDisposable
{
    // Subclasses should read inputs from Inputs -> do inference -> write inference results to Outputs
    protected Channel<float[]> Inputs
    {
        get;
    }
    protected Channel<float[]> Outputs
    {
        get;
    }

    private readonly Channel<TaskCompletionSource<float[]>> _completions;
    private readonly AsyncLock _writeLock = new();  // Synchronizes writes to Inputs and _completions

    // Reads from Outputs and _completions, completes tasks returned to callers with results
    private readonly Task _outputCompletionTask;

    private readonly InferenceSession _inferenceSession;

    protected ModelRunner(int maxBatchSize, InferenceSession inferenceSession)
    {
        Inputs = Channel.CreateBounded<float[]>(maxBatchSize * 2);
        Outputs = Channel.CreateBounded<float[]>(maxBatchSize * 2);
        _completions = Channel.CreateUnbounded<TaskCompletionSource<float[]>>();
        _outputCompletionTask = CompleteOutputs();
        _inferenceSession = inferenceSession;
    }

    // First await (outer task) is for the handoff, second await (inner task) is for the actual inference
    public async Task<Task<float[]>> Run(float[] input)
    {
        var tcs = new TaskCompletionSource<float[]>();
        await _writeLock.AcquireAsync(CancellationToken.None);
        try
        {
            await _completions.Writer.WriteAsync(tcs);  // unbounded, will not block
            await Inputs.Writer.WriteAsync(input);  // bounded, may block
        }
        finally
        {
            _writeLock.Release();
        }

        return tcs.Task;
    }

    private async Task CompleteOutputs()
    {
        await foreach (var result in Outputs.Reader.ReadAllAsync())
        {
            if (!_completions.Reader.TryRead(out var tcs))
            {
                throw new Exception("This shouldn't happen");
            }

            tcs.SetResult(result);
        }
    }

    // Run inference using _inferenceSession
    protected float[] RunInference(float[] input) => [];

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
