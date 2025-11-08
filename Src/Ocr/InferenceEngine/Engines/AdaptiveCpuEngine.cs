// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

namespace Ocr.InferenceEngine.Engines;

public class AdaptiveCpuEngine : IInferenceEngine
{
    private readonly TaskPool<(float[], int[])> _taskPool;
    private readonly IInferenceKernel _inferenceKernel;

    internal AdaptiveCpuEngine(AdaptiveCpuEngineOptions options, IInferenceKernel inferenceKernel)
    {
        _taskPool = new TaskPool<(float[], int[])>(options.InitialParallelism);
        _inferenceKernel = inferenceKernel;
    }

    // TODO: actually make this adaptive
    public async Task<(float[] OutputData, int[] OutputShape)> Run(float[] inputData, int[] inputShape)
    {
        var inferenceTask = Task.Run(() => _inferenceKernel.Execute(inputData, inputShape));
        return await await _taskPool.Execute(() => inferenceTask);
    }
}
