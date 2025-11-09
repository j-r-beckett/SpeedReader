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
    /// Registers SpeedReader and its dependencies with the DI container.
    /// Only supports SteadyCpuEngine for both detection and recognition.
    /// </summary>
    public static IServiceCollection AddSpeedReader(
        this IServiceCollection services,
        SpeedReaderOptions options)
    {
        // Register inference engines
        services.AddInferenceEngine(
            new SteadyCpuEngineOptions(parallelism: options.DetectionParallelism),
            new OnnxInferenceKernelOptions(
                model: Model.DbNet,
                quantization: options.DbNetQuantization,
                initialParallelism: options.DetectionParallelism,
                numIntraOpThreads: options.NumIntraOpThreads));

        services.AddInferenceEngine(
            new SteadyCpuEngineOptions(parallelism: options.RecognitionParallelism),
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

        // Register SpeedReader
        services.AddSingleton(sp =>
        {
            var detector = sp.GetRequiredService<TextDetector>();
            var recognizer = sp.GetRequiredService<TextRecognizer>();
            return new SpeedReader(detector, recognizer, options.MaxParallelism, options.MaxBatchSize);
        });

        return services;
    }

    /// <summary>
    /// Creates a SpeedReader instance using a new DI container.
    /// </summary>
    public static SpeedReader CreateSpeedReader(SpeedReaderOptions options)
    {
        var services = new ServiceCollection();
        services.AddSpeedReader(options);
        var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<SpeedReader>();
    }
}

/// <summary>
/// Configuration options for SpeedReader and its dependencies.
/// </summary>
public record SpeedReaderOptions
{
    // SpeedReader options
    public int MaxParallelism { get; init; } = 4;
    public int MaxBatchSize { get; init; } = 1;

    // TextDetector options
    public int TileWidth { get; init; } = 640;
    public int TileHeight { get; init; } = 640;

    // TextRecognizer options
    public int RecognitionInputWidth { get; init; } = 160;
    public int RecognitionInputHeight { get; init; } = 48;

    // Engine parallelism
    public int DetectionParallelism { get; init; } = 4;
    public int RecognitionParallelism { get; init; } = 4;

    // Model quantization
    public Quantization DbNetQuantization { get; init; } = Quantization.Int8;
    public Quantization SvtrQuantization { get; init; } = Quantization.Fp32;

    // ONNX runtime options
    public int NumIntraOpThreads { get; init; } = 1;
}
