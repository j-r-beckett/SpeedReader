// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.Diagnostics;
using Ocr.Inference;
using Ocr.Telemetry;

namespace Ocr.Controls;

public interface IExecutor
{
    Sensor Sensor { get; }
    int QueueDepth { get; }
    Task DecrementParallelism();
    void IncrementParallelism();
    int CurrentMaxParallelism { get; }
}

public class Executor<TIn, TOut> : IDisposable, IExecutor
{
    private readonly Func<TIn, TOut> _func;
    private readonly SemaphoreSlim _semaphore;
    private readonly AsyncLock _pauseExecutionLock = new();
    private int _currentMaxParallelism;
    private int _queueDepth;
    private readonly Dictionary<string, string>? _telemetryTags;

    public Executor(Func<TIn, TOut> func, int initialParallelism, Dictionary<string, string>? telemetryTags = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(initialParallelism);

        _func = func;
        _currentMaxParallelism = initialParallelism;
        _semaphore = new SemaphoreSlim(initialParallelism);
        _telemetryTags = telemetryTags;
        Sensor = new Sensor(telemetryTags);
    }

    public Sensor Sensor { get; init; }

    public async Task<Task<TOut>> ExecuteSingle(TIn input)
    {
        Interlocked.Increment(ref _queueDepth);
        var enteredQueueTime = SharedClock.Now;
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
        var exitedQueueTime = SharedClock.Now;
        MetricRecorder.RecordMetric("speedreader.inference.queue_wait_duration", (exitedQueueTime - enteredQueueTime).TotalMilliseconds, _telemetryTags);
        MetricRecorder.RecordMetric("speedreader.inference.queue_depth", _queueDepth, _telemetryTags);

        var task = Task.Run(() =>
        {
            using (Sensor.RecordJob())
            {
                return _func(input);
            }
        });

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
