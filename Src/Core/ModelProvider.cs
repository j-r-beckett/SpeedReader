// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.Collections.Concurrent;
using Microsoft.ML.OnnxRuntime;
using Resources;

namespace Core;

public class ModelProvider : IDisposable
{
    private readonly ConcurrentDictionary<(Model, ModelPrecision), InferenceSession> _sessions = new();
    private static readonly Lock _lock = new();  // ONNX inference session creation is globally thread unsafe
    private bool _disposed;

    public InferenceSession GetSession(Model model) => GetSession(model, ModelPrecision.FP32);

    public InferenceSession GetSession(Model model, ModelPrecision precision) => GetSession(model, precision, new SessionOptions
    {
        IntraOpNumThreads = 1,
        InterOpNumThreads = 1
    });

    public InferenceSession GetSession(Model model, ModelPrecision precision, SessionOptions options)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _sessions.GetOrAdd((model, precision), _ => CreateSessionInternal(model, precision, options));
    }

    private InferenceSession CreateSessionInternal(Model model, ModelPrecision precision, SessionOptions options)
    {
        lock (_lock)
        {
            var modelBytes = Models.GetModelBytes(model, precision);
            return new InferenceSession(modelBytes, options);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        foreach (var session in _sessions.Values)
        {
            session.Dispose();
        }

        _sessions.Clear();
        _disposed = true;
    }
}
