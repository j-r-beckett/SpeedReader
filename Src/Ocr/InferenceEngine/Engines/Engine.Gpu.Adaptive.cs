// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using Microsoft.Extensions.DependencyInjection;
using Ocr.InferenceEngine.Kernels;

namespace Ocr.InferenceEngine.Engines;

public record AdaptiveGpuEngineOptions : EngineOptions
{
    public AdaptiveGpuEngineOptions() => throw new NotImplementedException();
}

public class AdaptiveGpuEngine : IInferenceEngine
{
    public AdaptiveGpuEngine([FromKeyedServices(Model.DbNet)] AdaptiveGpuEngineOptions options,
        [FromKeyedServices(Model.DbNet)] IInferenceKernel inferenceKernel, DbNetMarker _)
        : this(options, inferenceKernel) { }

    public AdaptiveGpuEngine([FromKeyedServices(Model.Svtr)] AdaptiveGpuEngineOptions options,
        [FromKeyedServices(Model.Svtr)] IInferenceKernel inferenceKernel, SvtrMarker _)
        : this(options, inferenceKernel) { }

    public AdaptiveGpuEngine(AdaptiveGpuEngineOptions options, IInferenceKernel inferenceKernel) =>
        throw new NotImplementedException();

    public async Task<Task<(float[] OutputData, int[] OutputShape)>> Run(float[] inputData, int[] inputShape) =>
        throw new NotImplementedException();
}

