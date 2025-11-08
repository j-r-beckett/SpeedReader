// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using Ocr.InferenceEngine.Engines;

namespace Ocr.InferenceEngine;

// InferenceOptions
public record DetectionInferenceOptions : InferenceOptions
{
    public DetectionInferenceOptions(Quantization quantization, int initialParallelism, int numIntraOpThreads,
        int numInterOpThreads = 1, bool enableProfiling = false)
        : base(Model.DbNet, quantization, initialParallelism, numIntraOpThreads, numInterOpThreads, enableProfiling) {}
}

public record RecognitionInferenceOptions : InferenceOptions
{
    public RecognitionInferenceOptions(Quantization quantization, int initialParallelism, int numIntraOpThreads,
        int numInterOpThreads = 1, bool enableProfiling = false)
        : base(Model.Svtr, quantization, initialParallelism, numIntraOpThreads, numInterOpThreads, enableProfiling) {}
}

// SteadyCpuEngineOptions
public record DetectionSteadyCpuEngineOptions : SteadyCpuEngineOptions
{
    public DetectionSteadyCpuEngineOptions(int parallelism) : base(parallelism) {}
}

public record RecognitionSteadyCpuEngineOptions : SteadyCpuEngineOptions
{
    public RecognitionSteadyCpuEngineOptions(int parallelism) : base(parallelism) {}
}

// AdaptiveCpuEngineOptions
public record DetectionAdaptiveCpuEngineOptions : AdaptiveCpuEngineOptions
{
    public DetectionAdaptiveCpuEngineOptions(int initialParallelism) : base(initialParallelism) {}
}

public record RecognitionAdaptiveCpuEngineOptions : AdaptiveCpuEngineOptions
{
    public RecognitionAdaptiveCpuEngineOptions(int initialParallelism) : base(initialParallelism) {}
}

// Onnx inference kernels
public class DetectionOnnxInferenceKernel : OnnxInferenceKernel
{
    public DetectionOnnxInferenceKernel(DetectionInferenceOptions inferenceOptions, ModelLoader modelLoader)
        : base(inferenceOptions, modelLoader) {}
}

public class RecognitionOnnxInferenceKernel : OnnxInferenceKernel
{
    public RecognitionOnnxInferenceKernel(RecognitionInferenceOptions inferenceOptions, ModelLoader modelLoader)
        : base(inferenceOptions, modelLoader) {}
}

// Inference engines
public class DetectionInferenceEngine : IInferenceEngine
{
    private readonly IInferenceEngine _backingEngine;

    public DetectionInferenceEngine(DetectionSteadyCpuEngineOptions options, DetectionOnnxInferenceKernel inferenceKernel)
        => _backingEngine = new SteadyCpuEngine(options, inferenceKernel);

    public DetectionInferenceEngine(DetectionAdaptiveCpuEngineOptions options, DetectionOnnxInferenceKernel inferenceKernel) =>
        _backingEngine = new AdaptiveCpuEngine(options, inferenceKernel);

    public Task<(float[] OutputData, int[] OutputShape)> Run(float[] inputData, int[] inputShape) =>
        _backingEngine.Run(inputData, inputShape);
}

public class RecognitionInferenceEngine : IInferenceEngine
{
    private readonly IInferenceEngine _backingEngine;

    public RecognitionInferenceEngine(RecognitionSteadyCpuEngineOptions options, RecognitionOnnxInferenceKernel inferenceKernel)
        => _backingEngine = new SteadyCpuEngine(options, inferenceKernel);

    public RecognitionInferenceEngine(AdaptiveCpuEngineOptions options, RecognitionOnnxInferenceKernel inferenceKernel) =>
        _backingEngine = new AdaptiveCpuEngine(options, inferenceKernel);

    public Task<(float[] OutputData, int[] OutputShape)> Run(float[] inputData, int[] inputShape) =>
        _backingEngine.Run(inputData, inputShape);
}
