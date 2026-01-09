// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.Diagnostics;
using System.Threading.Channels;
using SpeedReader.Native.Threading;

namespace SpeedReader.Ocr.InferenceEngine.Engines;

public class AffinitizedThreadPool : IDisposable
{
    private readonly Channel<Action> _queuedActions = Channel.CreateUnbounded<Action>();
    private readonly VIPQueue<PinnedThread> _availableThreads = new();

    private readonly Task _orchestratorTask;
    private readonly CancellationTokenSource _orchestratorCts = new();

    private readonly Lock _threadsLock = new();
    private readonly HashSet<PinnedThread> _allThreads = [];

    public AffinitizedThreadPool(IEnumerable<int> cores)
    {
        foreach (var core in cores)
        {
            var thread = new PinnedThread(thread => _availableThreads.Enqueue(thread), core);
            _availableThreads.Enqueue(thread);
            _allThreads.Add(thread);
        }
        if (_allThreads.Count == 0)
            throw new ArgumentException("No cores specified", nameof(cores));

        _orchestratorTask = Task.Run(OrchestratorTask);
    }

    private async Task OrchestratorTask()
    {
        try
        {
            while (true)
            {
                var action = await _queuedActions.Reader.ReadAsync(_orchestratorCts.Token);
                var thread = await _availableThreads.DequeueAsync(vip: false, _orchestratorCts.Token);
                thread.Run(action);
            }
        }
        catch (OperationCanceledException) { }
    }

    private void PushThread(PinnedThread thread)
    {
        lock (_threadsLock)
            _allThreads.Add(thread);
        _availableThreads.Enqueue(thread);
    }

    private async Task<PinnedThread> PopThread()
    {
        var thread = await _availableThreads.DequeueAsync(vip: true);
        lock (_threadsLock)
            _allThreads.Remove(thread);
        return thread;
    }

    public static async Task TransferThread(AffinitizedThreadPool from, AffinitizedThreadPool to)
        => to.PushThread(await from.PopThread());

    public Task<T> Run<T>(Func<T> func)
    {
        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        return !_queuedActions.Writer.TryWrite(ExecuteFunc)
            ? throw new ObjectDisposedException(GetType().FullName)
            : tcs.Task;

        void ExecuteFunc()
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
        }
    }

    public Task Run(Action action)
    {
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        return !_queuedActions.Writer.TryWrite(ExecuteAction)
            ? throw new ObjectDisposedException(GetType().FullName)
            : tcs.Task;

        void ExecuteAction()
        {
            try
            {
                action();
                tcs.SetResult();
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        }
    }

    public int PoolSize { get { lock (_threadsLock) return _allThreads.Count; } }

    public void Dispose()
    {
        _queuedActions.Writer.Complete();
        _orchestratorCts.Cancel();
        _orchestratorTask.Wait();
        _orchestratorCts.Dispose();
        foreach (var thread in _allThreads)
            thread.Dispose();
        GC.SuppressFinalize(this);
    }

    private class PinnedThread : IDisposable
    {
        private Action? _action;
        private readonly ManualResetEventSlim _activationEvent = new(false);

        private readonly Action<PinnedThread> _requeueThread;
        private readonly int _core;

        private readonly Thread _thread;
        private readonly CancellationTokenSource _cts = new();
        private bool _disposed;

        public PinnedThread(Action<PinnedThread> requeueThread, int core)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(core, 0, nameof(core));
            _requeueThread = requeueThread;
            _core = core;
            _thread = new Thread(ThreadProc) { IsBackground = true };
            _thread.Start();
        }

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
