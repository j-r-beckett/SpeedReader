using System.Collections.Concurrent;
using Microsoft.ML.OnnxRuntime;
using Resources;

namespace Core;

public class ModelProvider : IDisposable
{
    private readonly ConcurrentDictionary<Model, InferenceSession> _sessions = new();
    private readonly Lock _lock = new();
    private bool _disposed;

    public InferenceSession GetSession(Model model) => GetSession(model, new SessionOptions
    {
        IntraOpNumThreads = 1,
        InterOpNumThreads = 1
    });

    public InferenceSession GetSession(Model model, SessionOptions options)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _sessions.GetOrAdd(model, _ => CreateSessionInternal(model, options));
    }

    private InferenceSession CreateSessionInternal(Model model, SessionOptions options)
    {
        lock (_lock)
        {
            var modelBytes = Models.GetModelBytes(model);
            return new InferenceSession(modelBytes, options);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;

        foreach (var session in _sessions.Values)
        {
            session.Dispose();
        }

        _sessions.Clear();
        _disposed = true;
    }
}
