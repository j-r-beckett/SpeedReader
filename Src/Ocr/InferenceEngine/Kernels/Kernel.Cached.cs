// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using Microsoft.Extensions.DependencyInjection;

namespace Ocr.InferenceEngine.Kernels;

public class CachedOnnxInferenceKernel : OnnxInferenceKernel
{
    public static new CachedOnnxInferenceKernel Factory(IServiceProvider serviceProvider, object? key)
    {
        var options = serviceProvider.GetRequiredKeyedService<CachedInferenceKernelOptions>(key);
        var modelLoader = serviceProvider.GetRequiredService<ModelLoader>();
        return new CachedOnnxInferenceKernel(options, modelLoader);
    }

    private CachedOnnxInferenceKernel(CachedInferenceKernelOptions inferenceOptions, ModelLoader modelLoader)
        : base(GetOnnxOptions(inferenceOptions), modelLoader) { }

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
