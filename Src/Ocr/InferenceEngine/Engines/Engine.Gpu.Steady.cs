// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using Microsoft.Extensions.DependencyInjection;
using Ocr.InferenceEngine.Kernels;

namespace Ocr.InferenceEngine.Engines;

public record SteadyGpuEngineOptions : EngineOptions
{
    public SteadyGpuEngineOptions() => throw new NotImplementedException();
}

public class SteadyGpuEngine : IInferenceEngine
{
    private readonly IInferenceKernel _inferenceKernel;

    public SteadyGpuEngine([FromKeyedServices(Model.DbNet)] SteadyGpuEngineOptions options,
        [FromKeyedServices(Model.DbNet)] IInferenceKernel inferenceKernel, DbNetMarker _)
        : this(options, inferenceKernel) { }

    public SteadyGpuEngine([FromKeyedServices(Model.Svtr)] SteadyGpuEngineOptions options,
        [FromKeyedServices(Model.Svtr)] IInferenceKernel inferenceKernel, SvtrMarker _)
        : this(options, inferenceKernel) { }

    public SteadyGpuEngine(SteadyGpuEngineOptions options, IInferenceKernel inferenceKernel) =>
        throw new NotImplementedException();

    public async Task<(float[] OutputData, int[] OutputShape)> Run(float[] inputData, int[] inputShape) =>
        throw new NotImplementedException();
}
