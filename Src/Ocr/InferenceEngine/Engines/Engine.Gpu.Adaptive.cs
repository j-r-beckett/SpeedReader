// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using Microsoft.Extensions.DependencyInjection;
using Ocr.InferenceEngine.Kernels;

namespace Ocr.InferenceEngine.Engines;

public record AdaptiveGpuEngineOptions : EngineOptions
{
    public AdaptiveGpuEngineOptions() => throw new NotImplementedException();
}

public class AdaptiveGpuEngine : IInferenceEngine
{
    public static AdaptiveGpuEngine Factory(IServiceProvider serviceProvider, object? key)
    {
        var options = serviceProvider.GetRequiredKeyedService<AdaptiveGpuEngineOptions>(key);
        var kernel = serviceProvider.GetRequiredKeyedService<IInferenceKernel>(key);
        return new AdaptiveGpuEngine(options, kernel);
    }

    private AdaptiveGpuEngine(AdaptiveGpuEngineOptions options, IInferenceKernel inferenceKernel) =>
        throw new NotImplementedException();

    public async Task<Task<(float[] OutputData, int[] OutputShape)>> Run(float[] inputData, int[] inputShape) =>
        throw new NotImplementedException();

    public ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }
}

