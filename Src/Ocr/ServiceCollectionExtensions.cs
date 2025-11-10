// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using Microsoft.Extensions.DependencyInjection;
using Ocr.InferenceEngine;
using Ocr.InferenceEngine.Engines;

namespace Ocr;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddOcrPipeline(
        this IServiceCollection services,
        OcrPipelineOptions options)
    {
        services.AddInferenceEngine(options.DetectionEngine);
        services.AddInferenceEngine(options.RecognitionEngine);

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

        services.AddSingleton(sp =>
        {
            var detector = sp.GetRequiredService<TextDetector>();
            var recognizer = sp.GetRequiredService<TextRecognizer>();
            return new OcrPipeline(detector, recognizer, options.MaxParallelism, 1);
        });

        return services;
    }

    public static OcrPipeline CreateOcrPipeline(OcrPipelineOptions options)
    {
        var services = new ServiceCollection();
        services.AddOcrPipeline(options);
        var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<OcrPipeline>();
    }
}
