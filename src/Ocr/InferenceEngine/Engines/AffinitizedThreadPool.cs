// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.Collections.Concurrent;
using System.Diagnostics;
using SpeedReader.Native.Threading;

namespace SpeedReader.Ocr.InferenceEngine.Engines;

public class AffinitizedThreadPool : IDisposable
{
    private readonly ConcurrentQueue<Action> _dbnetActions = new();
    private readonly ConcurrentQueue<Action> _svtrActions = new();
    private readonly ConcurrentQueue<ManagedThread> _dbnetThreads = new();
    private readonly ConcurrentQueue<ManagedThread> _svtrThreads = new();

    private readonly Thread _dispatcherThread;
    private readonly CancellationTokenSource _dispatcherCts = new();
    private readonly SemaphoreSlim _semaphore = new(0);

    private readonly List<ManagedThread> _allThreads = [];

    public AffinitizedThreadPool(Span<int> dbnetCores, Span<int> svtrCores)
    {
        if (dbnetCores.IsEmpty)
            throw new ArgumentException("No dbnet cores specified", nameof(dbnetCores));

        if (svtrCores.IsEmpty)
            throw new ArgumentException("No svtr cores specified", nameof(svtrCores));

        foreach (var core in dbnetCores)
            AddThread(_dbnetThreads, core);

        foreach (var core in svtrCores)
            AddThread(_svtrThreads, core);

        _dispatcherThread = new Thread(DispatcherThreadProc) { IsBackground = true };
        _dispatcherThread.Start();

        return;

        void AddThread(ConcurrentQueue<ManagedThread> threadQueue, int core)
        {
            var thread = new ManagedThread(thread =>
            {
                threadQueue.Enqueue(thread);
                _semaphore.Release();
            }, core);
            threadQueue.Enqueue(thread);
            _allThreads.Add(thread);
        }
    }

    private void DispatcherThreadProc()
    {
        try
        {
            while (true)
            {
                // Preferentially:
                //   SVTR job -> SVTR core
                //   DBNet job -> DBNet core
                //   SVTR job -> DBNet core
                // Where SVTR cores are E-cores and unused P-cores, while DBNet cores are a subset of P-cores

                _semaphore.Wait(_dispatcherCts.Token);
                if (!_svtrActions.IsEmpty && !_svtrThreads.IsEmpty)
                    Dispatch(_svtrActions, _svtrThreads);
                else if (!_dbnetActions.IsEmpty && !_dbnetThreads.IsEmpty)
                    Dispatch(_dbnetActions, _dbnetThreads);
                else if (!_svtrActions.IsEmpty && !_dbnetThreads.IsEmpty)
                    Dispatch(_svtrActions, _dbnetThreads);
            }
        }
        catch (OperationCanceledException) { }

        return;

        static void Dispatch(ConcurrentQueue<Action> actions, ConcurrentQueue<ManagedThread> threads)
        {
            threads.TryDequeue(out var thread);
            Debug.Assert(thread != null);
            actions.TryDequeue(out var action);
            Debug.Assert(action != null);
            thread.Run(action);
        }
    }

    public Task<T> Run<T>(Func<T> func, Model model)
    {
        ObjectDisposedException.ThrowIf(_dispatcherCts.IsCancellationRequested, this);
        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        var channel = model switch
        {
            Model.DbNet => _dbnetActions,
            Model.Svtr => _svtrActions,
            _ => throw new ArgumentOutOfRangeException(nameof(model))
        };
        channel.Enqueue(() =>
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
