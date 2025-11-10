// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

namespace Ocr.InferenceEngine;

using Kernels;

#region Base Configuration Types

public abstract record EngineOptions;

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

#endregion

#region Engine Configuration Types

public record AdaptiveCpuEngineOptions : EngineOptions
{
    public AdaptiveCpuEngineOptions(int initialParallelism) => InitialParallelism = initialParallelism;

    public int InitialParallelism { get; }
}

public record SteadyCpuEngineOptions : EngineOptions
{
    public SteadyCpuEngineOptions(int parallelism) => Parallelism = parallelism;

    public int Parallelism { get; }
}

public record AdaptiveGpuEngineOptions : EngineOptions
{
    public AdaptiveGpuEngineOptions() => throw new NotImplementedException();
}

public record SteadyGpuEngineOptions : EngineOptions
{
    public SteadyGpuEngineOptions() => throw new NotImplementedException();
}

#endregion

#region Kernel Configuration Types

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

public record NullInferenceKernelOptions : InferenceKernelOptions
{
    public NullInferenceKernelOptions(Model model, Quantization quantization, int[]? expectedInputShape, int[] outputShape)
        : base(model, quantization)
    {
        ExpectedInputShape = expectedInputShape;
        OutputShape = outputShape;
    }

    public int[]? ExpectedInputShape { get; init; }
    public int[] OutputShape { get; init; }
}

public record CachedInferenceKernelOptions : InferenceKernelOptions
{
    public CachedInferenceKernelOptions(Model model, Quantization quantization, int intraOpThreads)
        : base(model, quantization)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(intraOpThreads, 1, nameof(intraOpThreads));
        IntraOpThreads = intraOpThreads;
    }

    public int IntraOpThreads { get; }
}

#endregion

#region OcrPipeline Configuration

/// <summary>
/// Configuration options for OcrPipeline and its dependencies.
/// </summary>
public record OcrPipelineOptions
{
    // OcrPipeline options
    public int MaxParallelism { get; init; } = 4;
    public int MaxBatchSize { get; init; } = 1;

    // TextDetector options
    public int TileWidth { get; init; } = 640;
    public int TileHeight { get; init; } = 640;

    // TextRecognizer options
    public int RecognitionInputWidth { get; init; } = 160;
    public int RecognitionInputHeight { get; init; } = 48;

    // Engine parallelism
    public int DetectionParallelism { get; init; } = 4;
    public int RecognitionParallelism { get; init; } = 4;

    // Model quantization
    public Quantization DbNetQuantization { get; init; } = Quantization.Int8;
    public Quantization SvtrQuantization { get; init; } = Quantization.Fp32;

    // ONNX runtime options
    public int NumIntraOpThreads { get; init; } = 1;
}

#endregion
