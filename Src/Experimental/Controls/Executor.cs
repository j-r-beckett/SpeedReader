// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

namespace Experimental.Controls;

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

    public Executor(Func<TIn, TOut> func, int initialParallelism)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(initialParallelism);

        _func = func;
        _currentMaxParallelism = initialParallelism;
        _semaphore = new SemaphoreSlim(initialParallelism);
    }

    public Sensor Sensor { get; } = new();

    public async Task<Task<TOut>> ExecuteSingle(TIn input)
    {
        Interlocked.Increment(ref _queueDepth);
        await _semaphore.WaitAsync();
        while (_pauseExecutionLock.IsAcquired)
        {
            _semaphore.Release();
            await _pauseExecutionLock.AcquireAsync(CancellationToken.None);
            _pauseExecutionLock.Release();
            await _semaphore.WaitAsync();
        }
        Interlocked.Decrement(ref _queueDepth);

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
