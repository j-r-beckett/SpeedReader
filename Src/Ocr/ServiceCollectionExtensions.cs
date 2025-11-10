// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using Microsoft.Extensions.DependencyInjection;
using Ocr.InferenceEngine;
using Ocr.InferenceEngine.Engines;
using Ocr.InferenceEngine.Kernels;

namespace Ocr;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers OcrPipeline and its dependencies with the DI container.
    /// Only supports SteadyCpuEngine for both detection and recognition.
    /// </summary>
    public static IServiceCollection AddOcrPipeline(
        this IServiceCollection services,
        OcrPipelineOptions options)
    {
        // Register inference engines
        services.AddInferenceEngine(
            new AdaptiveCpuEngineOptions(initialParallelism: options.DetectionParallelism),
            new OnnxInferenceKernelOptions(
                model: Model.DbNet,
                quantization: options.DbNetQuantization,
                initialParallelism: options.DetectionParallelism,
                numIntraOpThreads: options.NumIntraOpThreads));

        services.AddInferenceEngine(
            new AdaptiveCpuEngineOptions(initialParallelism: options.RecognitionParallelism),
            new OnnxInferenceKernelOptions(
                model: Model.Svtr,
                quantization: options.SvtrQuantization,
                initialParallelism: options.RecognitionParallelism,
                numIntraOpThreads: options.NumIntraOpThreads));

        // Register TextDetector and TextRecognizer
        services.AddSingleton(sp =>
        {
            var dbnetEngine = sp.GetRequiredKeyedService<IInferenceEngine>(Model.DbNet);
            return new TextDetector(dbnetEngine, options.TileWidth, options.TileHeight);
        });

        services.AddSingleton(sp =>
        {
            var svtrEngine = sp.GetRequiredKeyedService<IInferenceEngine>(Model.Svtr);
            return new TextRecognizer(svtrEngine, options.RecognitionInputWidth, options.RecognitionInputHeight);
        });

        // Register OcrPipeline
        services.AddSingleton(sp =>
        {
            var detector = sp.GetRequiredService<TextDetector>();
            var recognizer = sp.GetRequiredService<TextRecognizer>();
            return new OcrPipeline(detector, recognizer, options.MaxParallelism, options.MaxBatchSize);
        });

        return services;
    }

    /// <summary>
    /// Creates an OcrPipeline instance using a new DI container.
    /// </summary>
    public static OcrPipeline CreateOcrPipeline(OcrPipelineOptions options)
    {
        var services = new ServiceCollection();
        services.AddOcrPipeline(options);
        var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<OcrPipeline>();
    }
}
