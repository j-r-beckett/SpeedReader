// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0


using Experimental.Inference;
using Microsoft.ML.OnnxRuntime;

namespace Benchmarks;

public class CachingModelRunner : CpuModelRunner
{
    private (float[], int[])? _cachedResult = null;
    private Lock _lock = new();

    public CachingModelRunner(InferenceSession session) : base(session, 1)
    {
    }

    protected override (float[], int[]) RunInferenceInternal(float[] batch, int[] shape)
    {
        if (_cachedResult is null)
        {
            lock (_lock)
            {
                if (_cachedResult is null)
                {
                    _cachedResult = base.RunInferenceInternal(batch, shape);
                    Console.WriteLine("Generating result");
                }
            }
        }

        return _cachedResult.Value;
    }
}
