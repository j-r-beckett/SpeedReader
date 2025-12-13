// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using Microsoft.Extensions.DependencyInjection;

namespace Ocr.InferenceEngine.Engines;

public class GpuEngine : IInferenceEngine
{
    public static GpuEngine Factory(IServiceProvider serviceProvider, object? key)
    {
        var config = serviceProvider.GetRequiredKeyedService<GpuEngineConfig>(key);
        return new GpuEngine(config);
    }

    private GpuEngine(GpuEngineConfig config) => throw new NotImplementedException();

    public int CurrentMaxCapacity() => throw new NotImplementedException();

    public async Task<Task<(float[] OutputData, int[] OutputShape)>> Run(float[] inputData, int[] inputShape) => throw new NotImplementedException();

    public ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }
}
