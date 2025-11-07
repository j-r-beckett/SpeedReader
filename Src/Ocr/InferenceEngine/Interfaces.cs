// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

namespace Ocr.Kernels;

public interface IInferenceEngine<TModel> where TModel : IModel
{
    public Task<(float[] OutputData, int[] OutputShape)> Run(float[] inputData, int[] inputShape);
}

public interface IInferenceKernel
{
    (float[] OutputData, int[] OutputShape) Execute(float[] data, int[] shape);
}

public interface IModel;

public interface IDbNet : IModel;

public interface ISvtr : IModel;
