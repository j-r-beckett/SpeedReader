// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.Collections.Concurrent;
using Microsoft.ML.OnnxRuntime;
using Resources;

namespace Ocr;

public static class InferenceSessionProvider
{
    private static readonly ConcurrentDictionary<(Model, ModelPrecision), InferenceSession> _sessions = new();
    private static readonly Lock _lock = new();  // ONNX inference session creation is globally thread unsafe

    public static InferenceSession GetCpuInferenceSession(Model model, ModelPrecision precision, int intraOpThreads)
    {
        return _sessions.GetOrAdd((model, precision), _ => CreateInferenceSession());

        InferenceSession CreateInferenceSession()
        {
            var options = new SessionOptions { IntraOpNumThreads = intraOpThreads, InterOpNumThreads = 1 };
            var modelBytes = Models.GetModelBytes(model, precision);
            lock (_lock)
            {
                return new InferenceSession(modelBytes, options);
            }
        }
    }
}
