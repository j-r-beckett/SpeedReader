// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

namespace Ocr.InferenceEngine;

#region InferenceKernel

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

public record OnnxInferenceKernelOptions
{
    public OnnxInferenceKernelOptions(Model model, Quantization quantization, int numIntraOpThreads,
        int numInterOpThreads = 1, bool enableProfiling = false)
    {
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


#endregion

#region CPU Engine

public record CpuTuningParameters
{
    public double ThroughputThreshold { get; init; } = 0.05;
    public int MeasurementWindowMultiplier { get; init; } = 8;
    public int MinParallelism { get; init; } = 1;
}

public record CpuEngineConfig
{
    public required OnnxInferenceKernelOptions Kernel { get; init; }
    public int Parallelism { get; init; } = 4;
    public CpuTuningParameters? AdaptiveTuning { get; init; }
}

#endregion

#region GPU Engine

public record GpuEngineConfig
{
    public required OnnxInferenceKernelOptions Kernel { get; init; }
    public int MaxBatchSize { get; init; } = 8;
}

#endregion
