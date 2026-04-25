// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using Microsoft.Extensions.DependencyInjection;
using SpeedReader.Ocr;
using SpeedReader.Ocr.InferenceEngine;

namespace SpeedReader.Library;

internal class Instance : IDisposable
{
    public readonly OcrPipeline Pipeline;
    private readonly ServiceProvider _serviceProvider;

    public Instance()
    {
        var options = new OcrPipelineOptions
        {
            DetectionOptions = new DetectionOptions(),
            RecognitionOptions = new RecognitionOptions(),
            DetectionEngine = new CpuEngineConfig
            {
                Kernel = new OnnxInferenceKernelOptions(
                    model: Model.DbNet,
                    quantization: Quantization.Int8,
                    numIntraOpThreads: 4),
                MaxParallelism = 4,
                ReservedPCores = [0, 2, 4, 6]
            },
            RecognitionEngine = new CpuEngineConfig
            {
                Kernel = new OnnxInferenceKernelOptions(
                    model: Model.Svtr,
                    quantization: Quantization.Fp32,
                    numIntraOpThreads: 4),
                MaxParallelism = 4,
                ReservedPCores = [0, 2, 4, 6]
            }
        };

        var services = new ServiceCollection();
        services.AddOcrPipeline(options);
        _serviceProvider = services.BuildServiceProvider();
        Pipeline = _serviceProvider.GetRequiredService<OcrPipeline>();
    }

    public void Dispose() => _serviceProvider.Dispose();
}
