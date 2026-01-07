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
    private readonly Queue<ManagedThread> _reservedPThreads = new();
    private readonly Queue<ManagedThread> _unreservedPThreads = new();
    private readonly Queue<ManagedThread> _eThreads = new();

    private readonly Thread _dispatcherThread;
    private readonly CancellationTokenSource _dispatcherCts = new();
    private readonly SemaphoreSlim _semaphore = new(0);

    private readonly Lock _threadsLock = new();

    private readonly List<ManagedThread> _allThreads = [];
    private readonly Dictionary<ManagedThread, Queue<ManagedThread>> _threadTargetQueues = new();

    public AffinitizedThreadPool(Span<int> reservedPCores, Span<int> unreservedPCores, Span<int> eCores)
    {
        if (reservedPCores.IsEmpty)
            throw new ArgumentException("At least one reserved P-core core must be specified");

        foreach (var core in reservedPCores)
            AddThread(_reservedPThreads, core);

        foreach (var core in unreservedPCores)
            AddThread(_unreservedPThreads, core);

        foreach (var core in eCores)
            AddThread(_eThreads, core);

        _dispatcherThread = new Thread(DispatcherThreadProc) { IsBackground = true };
        _dispatcherThread.Start();

        return;

        void AddThread(Queue<ManagedThread> threadQueue, int core)
        {
            var thread = new ManagedThread(RequeueThreadCallback, core);
            _threadTargetQueues[thread] = threadQueue;
            threadQueue.Enqueue(thread);
            _allThreads.Add(thread);
        }

        void RequeueThreadCallback(ManagedThread thread)
        {
            lock (_threadsLock)
                _threadTargetQueues[thread].Enqueue(thread);
            _semaphore.Release();
        }
    }

    private void DispatcherThreadProc()
    {
        try
        {
            while (true)
            {
                // Goal: maximize combined DBNet and SVTR throughput while keeping DBNet latency manageable.
                //       Backpressure is NOT in scope (that's managed by an external system).

                // Facts:
                //   - DBNet is DRAM bandwidth bound
                //   - SVTR is CPU bound, but it still consumes some DRAM bandwidth
                //   - SVTR jobs are downstream of DBNet jobs--a successfully completed DBNet job spawns SVTR jobs
                //   - DBNet is slow, ~125 ms. SVTR is fast, ~10 ms
                //   - Overall system latency is primary a function of DBNet latency
                //   - Compared to P-cores, E-cores have both less compute and less ability to drive memory. But the gap
                //     between P-cores and E-cores on compute is typically smaller than the gap on ability to drive memory

                // Deductions:
                //   - If we run DBNet on an E-core, lack of memory bandwidth will substantially increase latency. This
                //     will kill overall system latency. Additionally, on a typical system P-cores are sufficient to
                //     saturate memory bandwidth. So don't run DBNet on E-cores!
                //   - Running SVTR jobs on P-cores is fine, as long as it doesn't slow down DBNet jobs by stealing
                //     needed memory bandwidth
                //   - DBNet is memory bandwidth limited, so we can't just throw cores at it. But we can throw cores at
                //     SVTR! This suggests the following architecture: carefully manage the amount of cores devoted
                //     to DBNet, and give SVTR the rest of the cores on the system
                //   - SVTR jobs do consume some memory bandwidth. We want to avoid our SVTR jobs stealing bandwidth
                //     from DBNet jobs. So we only run a SVTR job on a P-core if we're keeping up with our DBNet jobs

                // Algorithm:
                //   Preferentially:
                //     - Run a [SVTR] job on an [E-core]
                //     - Run a [DBNet] job on a [reserved P-core]
                //     - Run a [SVTR] job on an [unreserved P-core]
                //     - Run a [SVTR] job on a [reserved P-core]


                _semaphore.Wait(_dispatcherCts.Token);
                lock (_threadsLock)
                {
                    if (!_svtrActions.IsEmpty && _eThreads.Count > 0)
                        Dispatch(_svtrActions, _eThreads);
                    else if (!_dbnetActions.IsEmpty && _reservedPThreads.Count > 0)
                        Dispatch(_dbnetActions, _reservedPThreads);
                    else if (!_svtrActions.IsEmpty && _unreservedPThreads.Count > 0)
                        Dispatch(_svtrActions, _unreservedPThreads);
                    else if (!_svtrActions.IsEmpty && _reservedPThreads.Count > 0)
                        Dispatch(_svtrActions, _reservedPThreads);
                }
            }
        }
        catch (OperationCanceledException) { }

        return;

        static void Dispatch(ConcurrentQueue<Action> actions, Queue<ManagedThread> threads)
        {
            var thread = threads.Dequeue();
            actions.TryDequeue(out var action);
            Debug.Assert(action != null);
            thread.Run(action);
        }
    }

    // Must not be called concurrently with Dispose
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

    public void ReservePCore()
    {
        lock (_threadsLock)
        {
            if (_unreservedPThreads.Count == 0)
                throw new InvalidOperationException("No unreserved P-core available");
            var thread = _unreservedPThreads.Dequeue();
            _threadTargetQueues[thread] = _reservedPThreads;
            _reservedPThreads.Enqueue(thread);
        }
        _semaphore.Release();
    }

    public void ReleasePCore()
    {
        lock (_threadsLock)
        {
            Debug.Assert(_reservedPThreads.Count != 0);
            if (_reservedPThreads.Count == 1)
                throw new InvalidOperationException("Cannot release last reserved P-core");
            var thread = _reservedPThreads.Dequeue();
            _threadTargetQueues[thread] = _unreservedPThreads;
            _unreservedPThreads.Enqueue(thread);
        }
        _semaphore.Release();
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
