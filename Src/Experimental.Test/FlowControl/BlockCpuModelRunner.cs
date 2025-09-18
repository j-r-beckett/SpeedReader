// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using Experimental.Inference;
using Microsoft.ML.OnnxRuntime;

namespace Experimental.Test.FlowControl;

public class BlockingCpuModelRunner : CpuModelRunner
{
    private readonly Lock _cachedResultLock = new();
    private (float[] Batch, int[] Shape)? _cachedResult;

    private readonly Func<int, Task> _block;

    private int _counter = -1;

    public BlockingCpuModelRunner(InferenceSession session, int maxParallelism, Func<int, Task> block)
        : base(session, maxParallelism) => _block = block;

    protected override (float[], int[]) RunInference(float[] batch, int[] shape)
    {
        var counter = Interlocked.Increment(ref _counter);

        if (_cachedResult == null)
        {
            lock (_cachedResultLock)
            {
                _cachedResult ??= base.RunInference(batch, shape);
            }
        }

        var blockTask = _block(counter);

        while (!blockTask.IsCompleted)
        {
            Thread.Sleep(25);
        }

        return _cachedResult.Value;
    }
}
