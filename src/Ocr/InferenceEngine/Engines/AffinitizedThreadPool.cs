// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.Collections.Concurrent;
using System.Diagnostics;
using SpeedReader.Native.Threading;

namespace SpeedReader.Ocr.InferenceEngine.Engines;

public class AffinitizedThreadPool : IDisposable
{
    private readonly BlockingCollection<Action> _queuedActions = new();
    private readonly BlockingCollection<ManagedThread> _availableThreads = new();

    // Orchestrator runs actions from _queuedActions on threads from _availableThreads
    private readonly Thread _orchestratorThread;
    private readonly CancellationTokenSource _orchestratorCts = new();

    private readonly List<ManagedThread> _allThreads = [];

    public AffinitizedThreadPool(IEnumerable<int> cores)
    {
        foreach (var core in cores)
        {
            var thread = new ManagedThread(thread => _availableThreads.Add(thread), core);
            _availableThreads.Add(thread);
            _allThreads.Add(thread);
        }
        if (_allThreads.Count == 0)
            throw new ArgumentException("No cores specified", nameof(cores));

        _orchestratorThread = new Thread(OrchestratorThreadProc) { IsBackground = true };
        _orchestratorThread.Start();
    }

    private void OrchestratorThreadProc()
    {
        try
        {
            while (true)
            {
                var action = _queuedActions.Take(_orchestratorCts.Token);
                var thread = _availableThreads.Take(_orchestratorCts.Token);
                thread.Run(action);
            }
        }
        catch (OperationCanceledException) { }
    }

    public Task<T> Run<T>(Func<T> func)
    {
        ObjectDisposedException.ThrowIf(_queuedActions.IsAddingCompleted, this);
        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        _queuedActions.Add(() =>
        {
            try
            {
                var result = func();
                tcs.SetResult(result);
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        });
        return tcs.Task;
    }

    public int Size => _allThreads.Count;

    public void Dispose()
    {
        _queuedActions.CompleteAdding();
        _orchestratorCts.Cancel();
        _orchestratorThread.Join();
        _orchestratorCts.Dispose();
        _queuedActions.Dispose();
        foreach (var thread in _allThreads)
            thread.Dispose();
        _availableThreads.CompleteAdding();
        _availableThreads.Dispose();
        GC.SuppressFinalize(this);
    }

    private class ManagedThread : IDisposable
    {
        private Action? _action;
        private readonly ManualResetEventSlim _activationEvent = new(false);

        private readonly Action<ManagedThread> _requeueThread;
        private readonly int _core;

        private readonly Thread _thread;
        private readonly CancellationTokenSource _cts = new();
        private bool _disposed;

        public ManagedThread(Action<ManagedThread> requeueThread, int core)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(core, 0, nameof(core));
            _thread = new Thread(ThreadProc) { IsBackground = true };
            _thread.Start();
            _requeueThread = requeueThread;
            _core = core;
        }

        // Not thread safe
        public void Run(Action action)
        {
            Debug.Assert(!_activationEvent.IsSet);
            _action = action;
            _activationEvent.Set();
        }

        private void ThreadProc()
        {
            Thread.BeginThreadAffinity();

            Affinitizer.PinToCore(_core);

            try
            {
                // Wait for activation, then run _action, then reset and do it all over again
                while (true)
                {
                    _activationEvent.Wait(_cts.Token);
                    Debug.Assert(_action != null);
                    _action();
                    _action = null;
                    _activationEvent.Reset();
                    _requeueThread(this);
                }
            }
            catch (OperationCanceledException) { }
            finally
            {
                Thread.EndThreadAffinity();
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            _cts.Cancel();
            _thread.Join();
            _cts.Dispose();
            _activationEvent.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
