// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using Microsoft.Extensions.DependencyInjection;
using Ocr.InferenceEngine.Kernels;

namespace Ocr.InferenceEngine.Engines;

public class SteadyGpuEngine : IInferenceEngine
{
    public static SteadyGpuEngine Factory(IServiceProvider serviceProvider, object? key)
    {
        var options = serviceProvider.GetRequiredKeyedService<SteadyGpuEngineOptions>(key);
        var kernel = serviceProvider.GetRequiredKeyedService<IInferenceKernel>(key);
        return new SteadyGpuEngine(options, kernel);
    }

    private SteadyGpuEngine(SteadyGpuEngineOptions options, IInferenceKernel inferenceKernel) =>
        throw new NotImplementedException();

    public async Task<Task<(float[] OutputData, int[] OutputShape)>> Run(float[] inputData, int[] inputShape) =>
        throw new NotImplementedException();

    public ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }
}
