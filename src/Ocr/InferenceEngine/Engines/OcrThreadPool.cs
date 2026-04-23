// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.Collections.Concurrent;
using System.Diagnostics;
using SpeedReader.Native.Threading;

namespace SpeedReader.Ocr.InferenceEngine.Engines;

public class Topology
{
    public required int[] PCores { get; init; }
    public required int[] ECores { get; init; }
}

public class OcrThreadPool : IDisposable
{
    private readonly VIPQueue<AffinitizedRunner> _dbnetPowerWorkers = new(3);
    private readonly VIPQueue<AffinitizedRunner> _svtrPowerWorkers = new(3);
    private readonly VIPQueue<AffinitizedRunner> _svtrEffWorkers = new(3);  // All users of this queue have vip == 0
    private readonly List<AffinitizedRunner> _allRunners = [];

    public OcrThreadPool(Topology topology, int startingDbNetWorkers)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(startingDbNetWorkers, 0, nameof(startingDbNetWorkers));
        if (startingDbNetWorkers > topology.PCores.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(startingDbNetWorkers),
                $"Requested {startingDbNetWorkers} workers, {topology.PCores.Length} available");
        }

        foreach (var core in topology.PCores[..startingDbNetWorkers])
        {
            var runner = new AffinitizedRunner(core);
            _allRunners.Add(runner);
            _dbnetPowerWorkers.Enqueue(runner);
        }

        foreach (var core in topology.PCores[startingDbNetWorkers..])
        {
            var runner = new AffinitizedRunner(core);
            _allRunners.Add(runner);
            _svtrPowerWorkers.Enqueue(runner);
        }

        foreach (var core in topology.ECores)
        {
            var runner = new AffinitizedRunner(core);
            _allRunners.Add(runner);
            _svtrEffWorkers.Enqueue(runner);
        }
    }

    public async Task<T> RunDbNet<T>(Func<T> func)
    {
        var thread = await _dbnetPowerWorkers.DequeueAsync(1);
        try
        {
            return await thread.Run(func);
        }
        finally
        {
            _dbnetPowerWorkers.Enqueue(thread);
        }
    }

    public async Task<T> RunSvtr<T>(Func<T> func)
    {
        var (thread, threadQueue) = await GetSvtrThread();
        try
        {
            return await thread.Run(func);
        }
        finally
        {
            threadQueue.Enqueue(thread);
        }
    }

    public Task<T> Run<T>(Func<T> func, Model model) => model switch
    {
        Model.DbNet => RunDbNet(func),
        Model.Svtr => RunSvtr(func),
        _ => throw new ArgumentException($"Unknown model {model}")
    };

    public async Task RebalanceDbNet2Svtr()
    {
        var dbnetThread = await _dbnetPowerWorkers.DequeueAsync(2);
        _svtrPowerWorkers.Enqueue(dbnetThread);
    }

    public async Task RebalanceSvtr2DbNet()
    {
        var svtrThread = await _svtrPowerWorkers.DequeueAsync(2);
        _dbnetPowerWorkers.Enqueue(svtrThread);
    }

    // Best-effort returns (in preference order) E-core, non-DbNet P-core, DbNet P-core
    private async Task<(AffinitizedRunner, VIPQueue<AffinitizedRunner>)> GetSvtrThread()
    {
        // queues[i], cancellations[i], dequeueTasks[i]
        VIPQueue<AffinitizedRunner>[] queues = [_svtrEffWorkers, _svtrPowerWorkers, _dbnetPowerWorkers];  // Order matters!
        var cancellations = new CancellationTokenSource[queues.Length];
        var dequeueTasks = new Task<AffinitizedRunner>[queues.Length];
        for (var i = 0; i < queues.Length; i++)
        {
            cancellations[i] = new CancellationTokenSource();
            dequeueTasks[i] = queues[i].DequeueAsync(0, cancellations[i].Token);
        }

        // Wait for any (at least one) dequeue task to complete
        await Task.WhenAny(dequeueTasks);

        // chosenThread and chosenThreadQueue are mutated by Step
        AffinitizedRunner? chosenThread = null;
        VIPQueue<AffinitizedRunner>? chosenThreadQueue = null;

        // Pick the first completed dequeue task we find; cancel all others, if they've already completed just put them back
        for (var i = 0; i < queues.Length; i++)
            await Step(i);

        Debug.Assert(chosenThread != null && chosenThreadQueue != null);

        foreach (var cts in cancellations)
            cts.Dispose();

        return (chosenThread, chosenThreadQueue);

        async ValueTask Step(int i)
        {
            var dequeueTask = dequeueTasks[i];
            var cancellation = cancellations[i];
            var queue = queues[i];
            if (chosenThread == null)
            {
                // If dequeue task is completed and we haven't found a thread yet, assign to output vars
                Debug.Assert(chosenThreadQueue == null);
                (chosenThread, chosenThreadQueue) = (dequeueTask.Result, queue);
            }
            else
            {
                // ReSharper disable once MethodHasAsyncOverload
                cancellation.Cancel();  // No-op if dequeue task already completed
                try
                {
                    var t = await dequeueTask;
                    queue.Enqueue(t);  // If dequeue succeeded, put the thread back
                }
                catch (OperationCanceledException) { }  // If dequeue was canceled, do nothing
            }
        }
    }

    private sealed class AffinitizedRunner : IDisposable
    {
        private readonly BlockingCollection<Action> _jobQueue = new();
        private readonly int _core;
        private readonly Thread _thread;
        private int _disposed;

        public AffinitizedRunner(int core)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(core, 0, nameof(core));
            _core = core;
            _thread = new Thread(ThreadProc) { IsBackground = true };
            _thread.Start();
        }

        public async Task<T> Run<T>(Func<T> func)
        {
            var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
            try
            {
                _jobQueue.Add(ExecuteAsync);
            }
            catch (InvalidOperationException)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }

            return await tcs.Task;

            void ExecuteAsync()
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

        private void ThreadProc()
        {
            Affinitizer.PinToCore(_core);

            foreach (var action in _jobQueue.GetConsumingEnumerable())
                action();
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 1)
                return;
            _jobQueue.CompleteAdding();
            _thread.Join();
            _jobQueue.Dispose();
        }
    }

    public void Dispose()
    {
        foreach (var runner in _allRunners)
            runner.Dispose();
    }
}
