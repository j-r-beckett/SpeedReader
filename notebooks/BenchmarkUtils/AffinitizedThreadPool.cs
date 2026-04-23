// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.Diagnostics;
using System.Threading.Channels;
using SpeedReader.Native.Threading;

namespace BenchmarkUtils;

public class AffinitizedThreadPool : IDisposable
{
    private readonly Channel<Action> _queuedActions = Channel.CreateUnbounded<Action>();
    private readonly Channel<PinnedThread> _availableThreads = Channel.CreateUnbounded<PinnedThread>();

    private readonly Task _orchestratorTask;
    private readonly CancellationTokenSource _orchestratorCts = new();

    private readonly List<PinnedThread> _allThreads = [];

    public AffinitizedThreadPool(IEnumerable<int> cores)
    {
        foreach (var core in cores)
        {
            var thread = new PinnedThread(thread => _availableThreads.Writer.TryWrite(thread), core);
            _availableThreads.Writer.TryWrite(thread);
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
                var thread = await _availableThreads.Reader.ReadAsync(_orchestratorCts.Token);
                thread.Run(action);
            }
        }
        catch (OperationCanceledException) { }
    }

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

    public int PoolSize => _allThreads.Count;

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
