// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

namespace Ocr.InferenceEngine.Engines;

public class ManagedExecutor : IDisposable
{
    private readonly SemaphoreSlim _semaphore;
    private readonly AsyncLock _pauseExecutionLock = new();
    private int _currentMaxParallelism;

    public ManagedExecutor(int initialParallelism)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(initialParallelism);

        _currentMaxParallelism = initialParallelism;
        _semaphore = new SemaphoreSlim(initialParallelism);
    }

    public async Task<Task<TOut>> ExecuteSingle<TOut>(Func<TOut> doInference)
    {
        await _semaphore.WaitAsync();
        while (_pauseExecutionLock.IsAcquired)
        {
            _semaphore.Release();
            await _pauseExecutionLock.AcquireAsync(CancellationToken.None);
            _pauseExecutionLock.Release();
            await _semaphore.WaitAsync();
        }

        Task<TOut> task;
        try
        {
            task = Task.Run(doInference);
        }
        catch (Exception ex)
        {
            task = Task.FromException<TOut>(
                new ManagedExecutorException("Caller provided inference function threw an exception", ex));
        }

#pragma warning disable CS4014
        task.ContinueWith(_ => _semaphore.Release());
#pragma warning restore CS4014

        return task;

    }

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
