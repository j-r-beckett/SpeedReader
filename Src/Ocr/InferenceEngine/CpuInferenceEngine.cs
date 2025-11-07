// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

namespace Ocr.Kernels;

public class CpuInferenceEngine<TModel> : IInferenceEngine<TModel> where TModel : IModel
{
    private readonly IInferenceKernel _inferenceKernel;

    public CpuInferenceEngine(IInferenceKernel inferenceKernel) => _inferenceKernel = inferenceKernel;

    public async Task<(float[] OutputData, int[] OutputShape)> Run(float[] inputData, int[] inputShape) => _inferenceKernel.Execute(inputData, inputShape);
}
