// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using Microsoft.Extensions.DependencyInjection;
using SpeedReader.Ocr.InferenceEngine;
using SpeedReader.Resources.CharDict;

namespace SpeedReader.Ocr;

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

        services.AddSingleton<EmbeddedCharDict>();

        services.AddSingleton(sp => TextDetector.Factory(sp, Model.DbNet));
        services.AddSingleton(sp => TextRecognizer.Factory(sp, Model.Svtr));

        services.AddSingleton<OcrPipeline>();

        return services;
    }
}
