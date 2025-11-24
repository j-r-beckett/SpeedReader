// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using Microsoft.Extensions.DependencyInjection;
using Ocr.InferenceEngine;
using Ocr.InferenceEngine.Engines;
using Resources;

namespace Ocr;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddOcrPipeline(
        this IServiceCollection services,
        OcrPipelineOptions options)
    {
        services.AddInferenceEngine(options.DetectionEngine);
        services.AddInferenceEngine(options.RecognitionEngine);

        services.AddSingleton(options.DetectionOptions);
        services.AddSingleton(options.RecognitionOptions);

        services.AddSingleton<CharacterDictionary>();

        services.AddSingleton(sp => TextDetector.Factory(sp, Model.DbNet));
        services.AddSingleton(sp => TextRecognizer.Factory(sp, Model.Svtr));

        services.AddSingleton<OcrPipeline>();

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
