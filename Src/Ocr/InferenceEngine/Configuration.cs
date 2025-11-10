// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

namespace Ocr.InferenceEngine;

#region Inference Kernel Configuration

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

public record KernelConfig
{
    public required Model Model { get; init; }
    public required Quantization Quantization { get; init; }
    public int NumIntraOpThreads { get; init; } = 1;
    public int NumInterOpThreads { get; init; } = 1;
    public bool EnableProfiling { get; init; } = false;
}

#endregion

#region CPU Engine Configuration

public record CpuTuningParameters
{
    public double ThroughputThreshold { get; init; } = 0.05;
    public int MeasurementWindowMultiplier { get; init; } = 8;
    public int MinParallelism { get; init; } = 1;
}

public record CpuEngineConfig
{
    public required KernelConfig Kernel { get; init; }
    public int Parallelism { get; init; } = 4;
    public CpuTuningParameters? AdaptiveTuning { get; init; }
}

#endregion

#region GPU Engine Configuration

public record GpuEngineConfig
{
    public required KernelConfig Kernel { get; init; }
    public int MaxBatchSize { get; init; } = 8;
}

#endregion

#region OcrPipeline Configuration

public record OcrPipelineOptions
{
    public int MaxParallelism { get; init; } = 4;

    public int TileWidth { get; init; } = 640;
    public int TileHeight { get; init; } = 640;

    public int RecognitionInputWidth { get; init; } = 160;
    public int RecognitionInputHeight { get; init; } = 48;

    public required CpuEngineConfig DetectionEngine { get; init; }
    public required CpuEngineConfig RecognitionEngine { get; init; }
}

#endregion

#region Kernel Options (Internal)

public abstract record InferenceKernelOptions
{
    public InferenceKernelOptions(Model model, Quantization quantization)
    {
        Model = model;
        Quantization = quantization;
    }

    public Model Model { get; }
    public Quantization Quantization { get; }
}

public record OnnxInferenceKernelOptions : InferenceKernelOptions
{
    public OnnxInferenceKernelOptions(Model model, Quantization quantization, int initialParallelism, int numIntraOpThreads,
        int numInterOpThreads = 1, bool enableProfiling = false)
        : base(model, quantization)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(initialParallelism, 1, nameof(initialParallelism));
        ArgumentOutOfRangeException.ThrowIfLessThan(numIntraOpThreads, 1, nameof(numIntraOpThreads));
        ArgumentOutOfRangeException.ThrowIfLessThan(numInterOpThreads, 1, nameof(numInterOpThreads));

        NumIntraOpThreads = numIntraOpThreads;
        NumInterOpThreads = numInterOpThreads;
        EnableProfiling = enableProfiling;
    }

    public int NumIntraOpThreads { get; }
    public int NumInterOpThreads { get; }
    public bool EnableProfiling { get; }
}

#endregion
