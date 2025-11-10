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

        var kernelOptions = new OnnxInferenceKernelOptions(
            model: config.Kernel.Model,
            quantization: config.Kernel.Quantization,
            initialParallelism: config.Parallelism,
            numIntraOpThreads: config.Kernel.NumIntraOpThreads,
            numInterOpThreads: config.Kernel.NumInterOpThreads,
            enableProfiling: config.Kernel.EnableProfiling);

        services.AddKeyedSingleton(key, kernelOptions);
        services.AddKeyedSingleton<IInferenceKernel>(key, OnnxInferenceKernel.Factory);

        services.AddKeyedSingleton(key, config);
        services.AddKeyedSingleton<IInferenceEngine>(key, CpuEngine.Factory);

        return services;
    }

    public static IServiceCollection AddInferenceEngine(
        this IServiceCollection services,
        GpuEngineConfig config)
    {
        var key = config.Kernel.Model;

        services.TryAddSingleton<ModelLoader>();

        var kernelOptions = new OnnxInferenceKernelOptions(
            model: config.Kernel.Model,
            quantization: config.Kernel.Quantization,
            initialParallelism: 1,
            numIntraOpThreads: config.Kernel.NumIntraOpThreads,
            numInterOpThreads: config.Kernel.NumInterOpThreads,
            enableProfiling: config.Kernel.EnableProfiling);

        services.AddKeyedSingleton(key, kernelOptions);
        services.AddKeyedSingleton<IInferenceKernel>(key, OnnxInferenceKernel.Factory);

        services.AddKeyedSingleton(key, config);
        services.AddKeyedSingleton<IInferenceEngine>(key, GpuEngine.Factory);

        return services;
    }

    public static IInferenceEngine CreateInferenceEngine(CpuEngineConfig config)
    {
        var services = new ServiceCollection();
        services.AddInferenceEngine(config);
        var provider = services.BuildServiceProvider();
        var key = config.Kernel.Model;
        return provider.GetRequiredKeyedService<IInferenceEngine>(key);
    }

    public static IInferenceEngine CreateInferenceEngine(GpuEngineConfig config)
    {
        var services = new ServiceCollection();
        services.AddInferenceEngine(config);
        var provider = services.BuildServiceProvider();
        var key = config.Kernel.Model;
        return provider.GetRequiredKeyedService<IInferenceEngine>(key);
    }
}
