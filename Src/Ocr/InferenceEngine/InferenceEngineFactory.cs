// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using Resources;

namespace Ocr.Kernels;

public class InferenceEngineFactory
{
    // If applicationThreads == 0, then applicationThreads is adaptively tuned
    public IInferenceEngine<IModel> CreateCpuInferenceEngine<TModel>(
        int applicationThreads,
        int intraOpThreads,
        Quantization quantization) where TModel : IModel => throw new NotImplementedException();

    public IInferenceEngine<IModel> CreateCallerControlledCpuInferenceEngine(
        int initialApplicationThreads,
        int intraOpThreads,
        Quantization quantization,
        out Controller controller) => throw new NotImplementedException();
}

public class Controller
{
    public void TurnUp() { }
    public void TurnDown() { }
}
