// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.Diagnostics;
using Ocr.Telemetry;

namespace Ocr.InferenceEngine.Engines;

public class ManagedExecutor : IDisposable
{
    private readonly SemaphoreSlim _semaphore;
    private readonly AsyncLock _pauseExecutionLock = new();
    private int _currentMaxParallelism;
    private int _currentParallelism;
    private int _queueDepth;
    private readonly Dictionary<string, string>? _telemetryTags;

    public ManagedExecutor(int initialParallelism, Dictionary<string, string>? telemetryTags = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(initialParallelism);

        _currentMaxParallelism = initialParallelism;
        _semaphore = new SemaphoreSlim(initialParallelism);
        _telemetryTags = telemetryTags;
    }

    public async Task<Task<TOut>> ExecuteSingle<TOut>(Func<TOut> doInference)
    {
        Interlocked.Increment(ref _queueDepth);
        var enteredQueueTimestamp = Stopwatch.GetTimestamp();
        MetricRecorder.RecordMetric("speedreader.inference.queue_depth", _queueDepth, _telemetryTags);
        await _semaphore.WaitAsync();
        while (_pauseExecutionLock.IsAcquired)
        {
            _semaphore.Release();
            await _pauseExecutionLock.AcquireAsync(CancellationToken.None);
            _pauseExecutionLock.Release();
            await _semaphore.WaitAsync();
        }
        Interlocked.Decrement(ref _queueDepth);
        var queueWaitDuration = Stopwatch.GetElapsedTime(enteredQueueTimestamp);
        MetricRecorder.RecordMetric("speedreader.inference.queue_wait_duration", queueWaitDuration.TotalMilliseconds, _telemetryTags);
        MetricRecorder.RecordMetric("speedreader.inference.queue_depth", _queueDepth, _telemetryTags);

        var startTimestamp = Stopwatch.GetTimestamp();
        Task<TOut> task;
        try
        {
            MetricRecorder.RecordMetric("speedreader.inference.parallelism",
                Interlocked.Increment(ref _currentMaxParallelism), _telemetryTags);
            task = Task.Run(doInference);
        }
        catch (Exception ex)
        {
            task = Task.FromException<TOut>(
                new ManagedExecutorException("Caller provided inference function threw an exception", ex));
        }
        finally
        {
            MetricRecorder.RecordMetric("speedreader.inference.parallelism", Interlocked.Decrement(ref _currentMaxParallelism), _telemetryTags);
            MetricRecorder.RecordMetric("speedreader.inference.counter", 1, _telemetryTags);
            MetricRecorder.RecordMetric("speedreader.inference.duration", Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds, _telemetryTags);
        }

#pragma warning disable CS4014
        task.ContinueWith(_ => _semaphore.Release());
#pragma warning restore CS4014

        return task;

    }

    // Estimate
    public int QueueDepth => _queueDepth;

    // Estimate
    public int CurrentMaxParallelism => _currentMaxParallelism;

    public void IncrementParallelism()
    {
        _semaphore.Release();
        Interlocked.Increment(ref _currentMaxParallelism);
    }

    public async Task DecrementParallelism()
    {
        await _pauseExecutionLock.AcquireAsync(CancellationToken.None);
        await _semaphore.WaitAsync();
        _pauseExecutionLock.Release();
        Interlocked.Decrement(ref _currentMaxParallelism);
    }

    public void Dispose()
    {
        _semaphore.Dispose();
        _pauseExecutionLock.Dispose();
        GC.SuppressFinalize(this);
    }

    private class AsyncLock : IDisposable
    {
        private readonly SemaphoreSlim _semaphore = new(1, 1);

        public async Task AcquireAsync(CancellationToken cancellationToken) => await _semaphore.WaitAsync(cancellationToken);

        public void Release() => _semaphore.Release();

        public bool IsAcquired => _semaphore.CurrentCount == 0;

        public void Dispose()
        {
            _semaphore.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}

public class ManagedExecutorException : Exception
{
    public ManagedExecutorException(string message, Exception innerException) : base(message, innerException) { }
}
