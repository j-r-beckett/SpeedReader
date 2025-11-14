// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Ocr.InferenceEngine.Engines;

namespace Ocr.InferenceEngine;

public static class Factories
{
    public static IServiceCollection AddInferenceEngine(
        this IServiceCollection services,
        CpuEngineConfig config)
    {
        var key = config.Kernel.Model;

        services.TryAddSingleton<ModelLoader>();

        services.AddKeyedSingleton(key, config.Kernel);
        services.AddKeyedSingleton<IInferenceKernel>(key, NativeOnnxInferenceKernel.Factory);

        services.AddKeyedSingleton(key, config);
        services.AddKeyedSingleton<IInferenceEngine>(key, CpuEngine.Factory);

        return services;
    }
}
