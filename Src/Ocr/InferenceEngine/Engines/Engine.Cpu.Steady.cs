// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using Microsoft.Extensions.DependencyInjection;
using Ocr.InferenceEngine.Kernels;

namespace Ocr.InferenceEngine.Engines;

public record SteadyCpuEngineOptions : EngineOptions
{
    public SteadyCpuEngineOptions(int parallelism) => Parallelism = parallelism;

    public int Parallelism { get; }
}

public class SteadyCpuEngine : IInferenceEngine
{
    private readonly TaskPool<(float[], int[])> _taskPool;
    private readonly IInferenceKernel _inferenceKernel;

    public SteadyCpuEngine([FromKeyedServices(Model.DbNet)] SteadyCpuEngineOptions options,
        [FromKeyedServices(Model.DbNet)] IInferenceKernel inferenceKernel, DbNetMarker _)
        : this(options, inferenceKernel) { }

    public SteadyCpuEngine([FromKeyedServices(Model.Svtr)] SteadyCpuEngineOptions options,
        [FromKeyedServices(Model.Svtr)] IInferenceKernel inferenceKernel, SvtrMarker _)
        : this(options, inferenceKernel) { }

    public SteadyCpuEngine(SteadyCpuEngineOptions options, IInferenceKernel inferenceKernel)
    {
        _taskPool = new TaskPool<(float[], int[])>(options.Parallelism);
        _inferenceKernel = inferenceKernel;
    }

    public async Task<Task<(float[] OutputData, int[] OutputShape)>> Run(float[] inputData, int[] inputShape)
    {
        var inferenceTask = Task.Run(() => _inferenceKernel.Execute(inputData, inputShape));
        return await _taskPool.Execute(() => inferenceTask);
    }
}
