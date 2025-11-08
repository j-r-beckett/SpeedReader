// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

namespace Ocr.InferenceEngine;

public enum Model
{
    DbNet,
    Svtr
}

public enum Quantization
{
    Int8,
    Bf16,
    Fp32
}

public record InferenceOptions
{
    public InferenceOptions(Model model, Quantization quantization, int initialParallelism, int numIntraOpThreads,
        int numInterOpThreads = 1, bool enableProfiling = false)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(initialParallelism, 1, nameof(initialParallelism));
        ArgumentOutOfRangeException.ThrowIfLessThan(numIntraOpThreads, 1, nameof(numIntraOpThreads));
        ArgumentOutOfRangeException.ThrowIfLessThan(numInterOpThreads, 1, nameof(numInterOpThreads));

        Model = model;
        Quantization = quantization;
        NumIntraOpThreads = numIntraOpThreads;
        NumInterOpThreads = numInterOpThreads;
        EnableProfiling = enableProfiling;
    }

    public Model Model { get; }
    public Quantization Quantization { get; }
    public int NumIntraOpThreads { get; }
    public int NumInterOpThreads { get; }
    public bool EnableProfiling { get; }
}

public record SteadyCpuEngineOptions
{
    public SteadyCpuEngineOptions(int parallelism)
    {
        Parallelism = parallelism;
    }

    public int Parallelism { get; }
}

public record AdaptiveCpuEngineOptions
{
    public AdaptiveCpuEngineOptions(int initialParallelism)
    {
        InitialParallelism = initialParallelism;
    }

    public int InitialParallelism { get; }
}

public record SteadyGpuEngineOptions
{
    public SteadyGpuEngineOptions() => throw new NotImplementedException();
}

public record AdaptiveGpuEngineOptions
{
    public AdaptiveGpuEngineOptions() => throw new NotImplementedException();
}

