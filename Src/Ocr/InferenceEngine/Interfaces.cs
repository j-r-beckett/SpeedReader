// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

namespace Ocr.InferenceEngine;

public interface IInferenceKernel
{
    (float[] OutputData, int[] OutputShape) Execute(float[] data, int[] shape);
}

