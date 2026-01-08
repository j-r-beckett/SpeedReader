// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.Diagnostics.Metrics;
using Microsoft.Extensions.DependencyInjection;

namespace SpeedReader.Ocr.InferenceEngine.Engines;

public class CpuEngine : IInferenceEngine
{
    private readonly IInferenceKernel _inferenceKernel;
    private readonly OcrThreadPool _threadPool;
    private readonly Model _model;

    public static CpuEngine Factory(IServiceProvider serviceProvider, object? key)
    {
        var config = serviceProvider.GetRequiredKeyedService<CpuEngineConfig>(key);
        var kernel = serviceProvider.GetRequiredKeyedService<IInferenceKernel>(key);
        var meterFactory = serviceProvider.GetService<IMeterFactory>();
        return new CpuEngine(kernel, (Model)key!, meterFactory);
    }

    private CpuEngine(IInferenceKernel inferenceKernel, Model model, IMeterFactory? meterFactory)
    {
        int[] pCores = [0, 2, 4, 6, 8, 10, 12, 14];
        int[] eCores = [16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27];
        _inferenceKernel = inferenceKernel;
        _threadPool = new OcrThreadPool(pCores, [], eCores);
        _model = model;
    }

    public int CurrentMaxCapacity() => _threadPool.Size;

    public Task<(float[] OutputData, int[] OutputShape)> Run(float[] inputData, int[] inputShape) =>
        _threadPool.Run(() =>
        {
            var batchedInputShape = new[] { 1 }.Concat(inputShape).ToArray(); // Add batch dimension
            var (resultData, batchedResultShape) = _inferenceKernel.Execute(inputData, batchedInputShape);
            var resultShape = batchedResultShape[1..]; // Remove batch dimension
            return (resultData, resultShape);
        }, _model);

    public async ValueTask DisposeAsync()
    {
        _threadPool.Dispose();

        GC.SuppressFinalize(this);
    }
}
