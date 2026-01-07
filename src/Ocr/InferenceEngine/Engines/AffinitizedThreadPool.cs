// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.Collections.Concurrent;
using System.Diagnostics;
using SpeedReader.Native.Threading;

namespace SpeedReader.Ocr.InferenceEngine.Engines;

public class AffinitizedThreadPool : IDisposable
{
    private readonly ConcurrentQueue<Action> _queuedActions = new();
    private readonly ConcurrentQueue<ManagedThread> _availableThreads = new();

    // Dispatcher runs actions from _queuedActions on threads from _availableThreads
    private readonly Thread _dispatcherThread;
    private readonly CancellationTokenSource _dispatcherCts = new();
    private readonly SemaphoreSlim _semaphore = new(0);

    private readonly List<ManagedThread> _allThreads = [];

    public AffinitizedThreadPool(IEnumerable<int> cores)
    {
        foreach (var core in cores)
        {
            var thread = new ManagedThread(thread =>
            {
                _availableThreads.Enqueue(thread);
                _semaphore.Release();
            }, core);
            _availableThreads.Enqueue(thread);
            _allThreads.Add(thread);
        }
        if (_allThreads.Count == 0)
            throw new ArgumentException("No cores specified", nameof(cores));

        _dispatcherThread = new Thread(DispatcherThreadProc) { IsBackground = true };
        _dispatcherThread.Start();
    }

    private void DispatcherThreadProc()
    {
        try
        {
            while (true)
            {
                _semaphore.Wait(_dispatcherCts.Token);
                if (_availableThreads.Count > 0 && _queuedActions.Count > 0)
                {
                    _availableThreads.TryDequeue(out var thread);
                    Debug.Assert(thread != null);
                    _queuedActions.TryDequeue(out var action);
                    Debug.Assert(action != null);
                    thread.Run(action);
                }
            }
        }
        catch (OperationCanceledException) { }
    }

    public Task<T> Run<T>(Func<T> func)
    {
        ObjectDisposedException.ThrowIf(_dispatcherCts.IsCancellationRequested, this);
        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        _queuedActions.Enqueue(() =>
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
        _semaphore.Release();
        return tcs.Task;
    }

    public int Size => _allThreads.Count;

    public void Dispose()
    {
        _dispatcherCts.Cancel();
        _dispatcherThread.Join();
        _dispatcherCts.Dispose();

        foreach (var thread in _allThreads)
            thread.Dispose();
        _semaphore.Dispose();
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
            _requeueThread = requeueThread;
            _core = core;
            _thread = new Thread(ThreadProc) { IsBackground = true };
            _thread.Start();
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
