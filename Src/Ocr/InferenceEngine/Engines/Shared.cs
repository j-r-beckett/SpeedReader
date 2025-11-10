// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

namespace Ocr.InferenceEngine.Engines;

public interface IInferenceEngine : IAsyncDisposable
{
    Task<Task<(float[] OutputData, int[] OutputShape)>> Run(float[] inputData, int[] inputShape);
}
