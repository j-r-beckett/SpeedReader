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
        services.TryAddSingleton<DbNetMarker>();
        services.TryAddSingleton<SvtrMarker>();

        switch (inferenceOptions)
        {
            case OnnxInferenceKernelOptions x:
                services.AddKeyedSingleton(key, x);
                services.AddKeyedSingleton<IInferenceKernel, OnnxInferenceKernel>(key);
                break;
            case NullInferenceKernelOptions x:
                services.AddKeyedSingleton(key, x);
                services.AddKeyedSingleton<IInferenceKernel, NullInferenceKernel>(key);
                break;
            case CachedInferenceKernelOptions x:
                services.AddKeyedSingleton(key, x);
                services.AddKeyedSingleton<IInferenceKernel, CachedOnnxInferenceKernel>(key);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(inferenceOptions));
        }

        switch (engineOptions)
        {
            case AdaptiveCpuEngineOptions x:
                services.TryAddKeyedSingleton(key, x);
                services.TryAddKeyedSingleton<IInferenceEngine, AdaptiveCpuEngine>(key);
                break;
            case SteadyCpuEngineOptions x:
                services.TryAddKeyedSingleton(key, x);
                services.TryAddKeyedSingleton<IInferenceEngine, SteadyCpuEngine>(key);
                break;
            case AdaptiveGpuEngineOptions x:
                services.TryAddKeyedSingleton(key, x);
                services.TryAddKeyedSingleton<IInferenceEngine, AdaptiveGpuEngine>(key);
                break;
            case SteadyGpuEngineOptions x:
                services.TryAddKeyedSingleton(key, x);
                services.TryAddKeyedSingleton<IInferenceEngine, SteadyGpuEngine>(key);
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

// Markers for keyed services, used to disambiguate constructors have differ only in keys
public class DbNetMarker;

public class SvtrMarker;
