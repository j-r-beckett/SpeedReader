// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

namespace Ocr.InferenceEngine.Engines;

public class SteadyCpuEngine : IInferenceEngine
{
    private readonly TaskPool<(float[], int[])> _taskPool;
    private readonly IInferenceKernel _inferenceKernel;

    internal SteadyCpuEngine(SteadyCpuEngineOptions options, IInferenceKernel inferenceKernel)
    {
        _taskPool = new TaskPool<(float[], int[])>(options.Parallelism);
        _inferenceKernel = inferenceKernel;
    }

    public async Task<(float[] OutputData, int[] OutputShape)> Run(float[] inputData, int[] inputShape)
    {
        var inferenceTask = Task.Run(() => _inferenceKernel.Execute(inputData, inputShape));
        return await await _taskPool.Execute(() => inferenceTask);
    }
}
