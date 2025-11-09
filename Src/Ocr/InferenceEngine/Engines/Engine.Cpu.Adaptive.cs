// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using Microsoft.Extensions.DependencyInjection;
using Ocr.InferenceEngine.Kernels;

namespace Ocr.InferenceEngine.Engines;

public record AdaptiveCpuEngineOptions : EngineOptions
{
    public AdaptiveCpuEngineOptions(int initialParallelism) => InitialParallelism = initialParallelism;

    public int InitialParallelism { get; }
}

public class AdaptiveCpuEngine : IInferenceEngine
{
    private readonly TaskPool<(float[], int[])> _taskPool;
    private readonly IInferenceKernel _inferenceKernel;

    public static AdaptiveCpuEngine Factory(IServiceProvider serviceProvider, object? key)
    {
        var options = serviceProvider.GetRequiredKeyedService<AdaptiveCpuEngineOptions>(key);
        var kernel = serviceProvider.GetRequiredKeyedService<IInferenceKernel>(key);
        return new AdaptiveCpuEngine(options, kernel);
    }

    private AdaptiveCpuEngine(AdaptiveCpuEngineOptions options, IInferenceKernel inferenceKernel)
    {
        _taskPool = new TaskPool<(float[], int[])>(options.InitialParallelism);
        _inferenceKernel = inferenceKernel;
    }

    public async Task<Task<(float[] OutputData, int[] OutputShape)>> Run(float[] inputData, int[] inputShape) =>
        throw new NotImplementedException();
}
