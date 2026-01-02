// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

namespace SpeedReader.Ocr.InferenceEngine;

public interface IInferenceEngine : IAsyncDisposable
{
    Task<(float[] OutputData, int[] OutputShape)> Run(float[] inputData, int[] inputShape);
    int CurrentMaxCapacity();
}

public interface IInferenceKernel
{
    (float[] OutputData, int[] OutputShape) Execute(float[] data, int[] shape);
}

