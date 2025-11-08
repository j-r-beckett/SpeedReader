// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using Microsoft.Extensions.DependencyInjection;

namespace Ocr.InferenceEngine.Kernels;

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

public class CachedOnnxInferenceKernel : OnnxInferenceKernel
{
    public CachedOnnxInferenceKernel([FromKeyedServices(Model.DbNet)] CachedInferenceKernelOptions inferenceOptions,
        ModelLoader modelLoader, DbNetMarker marker) : base(GetOnnxOptions(inferenceOptions), modelLoader, marker) { }

    public CachedOnnxInferenceKernel([FromKeyedServices(Model.Svtr)] CachedInferenceKernelOptions inferenceOptions,
        [FromKeyedServices(Model.Svtr)] OnnxInferenceKernel onnxInferenceKernel,
        ModelLoader modelLoader, SvtrMarker marker) : base(GetOnnxOptions(inferenceOptions), modelLoader, marker) { }

    private static OnnxInferenceKernelOptions GetOnnxOptions(CachedInferenceKernelOptions cachedOptions) =>
        new(
            cachedOptions.Model,
            cachedOptions.Quantization,
            initialParallelism: 1,
            numIntraOpThreads: cachedOptions.IntraOpThreads,
            numInterOpThreads: 1,
            enableProfiling: false);

    private readonly Lock _cacheLock = new();
    private (float[], int[])? _cachedOutput;

    public override (float[] OutputData, int[] OutputShape) Execute(float[] data, int[] shape)
    {
        if (_cachedOutput == null)
        {
            lock (_cacheLock)
            {
                if (_cachedOutput == null)
                {
                    _cachedOutput = base.Execute(data, shape);
                }
            }
        }

        return _cachedOutput.Value;
    }
}
