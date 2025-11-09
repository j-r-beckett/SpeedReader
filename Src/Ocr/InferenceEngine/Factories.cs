// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Ocr.InferenceEngine.Engines;
using Ocr.InferenceEngine.Kernels;

namespace Ocr.InferenceEngine;

public static class Factories
{
    public static IServiceCollection AddInferenceEngine(
        this IServiceCollection services,
        EngineOptions engineOptions,
        InferenceKernelOptions inferenceOptions)
    {
        var key = inferenceOptions.Model;

        services.TryAddSingleton<ModelLoader>();

        switch (inferenceOptions)
        {
            case OnnxInferenceKernelOptions x:
                services.AddKeyedSingleton(key, x);
                services.AddKeyedSingleton<IInferenceKernel>(key, OnnxInferenceKernel.Factory);
                break;
            case NullInferenceKernelOptions x:
                services.AddKeyedSingleton(key, x);
                services.AddKeyedSingleton<IInferenceKernel>(key, NullInferenceKernel.Factory);
                break;
            case CachedInferenceKernelOptions x:
                services.AddKeyedSingleton(key, x);
                services.AddKeyedSingleton<IInferenceKernel>(key, CachedOnnxInferenceKernel.Factory);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(inferenceOptions));
        }

        switch (engineOptions)
        {
            case AdaptiveCpuEngineOptions x:
                services.TryAddKeyedSingleton(key, x);
                services.TryAddKeyedSingleton<IInferenceEngine>(key, AdaptiveCpuEngine.Factory);
                break;
            case SteadyCpuEngineOptions x:
                services.TryAddKeyedSingleton(key, x);
                services.TryAddKeyedSingleton<IInferenceEngine>(key, SteadyCpuEngine.Factory);
                break;
            case AdaptiveGpuEngineOptions x:
                services.TryAddKeyedSingleton(key, x);
                services.TryAddKeyedSingleton<IInferenceEngine>(key, AdaptiveGpuEngine.Factory);
                break;
            case SteadyGpuEngineOptions x:
                services.TryAddKeyedSingleton(key, x);
                services.TryAddKeyedSingleton<IInferenceEngine>(key, SteadyGpuEngine.Factory);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(engineOptions));
        }

        return services;
    }

    public static IInferenceEngine CreateInferenceEngine(
        EngineOptions engineOptions,
        InferenceKernelOptions inferenceOptions)
    {
        var services = new ServiceCollection();
        services.AddInferenceEngine(engineOptions, inferenceOptions);
        var provider = services.BuildServiceProvider();
        var key = inferenceOptions.Model;
        return provider.GetRequiredKeyedService<IInferenceEngine>(key);
    }
}
