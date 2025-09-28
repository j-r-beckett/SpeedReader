// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

namespace Experimental.Inference;

public class ParallelismManager<T>
{
    private readonly Func<Task<T>> _func;
    private readonly SemaphoreSlim _semaphore;
    private readonly AsyncLock _pauseExecutionLock = new();

    public ParallelismManager(Func<Task<T>> func, int start)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(start);

        _func = func;
        _semaphore = new SemaphoreSlim(start);
    }

    public async Task<T> Call()
    {
        await _semaphore.WaitAsync();
        while (_pauseExecutionLock.IsAcquired)
        {
            _semaphore.Release();
            await _pauseExecutionLock.AcquireAsync(CancellationToken.None);
            _pauseExecutionLock.Release();
            await _semaphore.WaitAsync();
        }
        try
        {
            return await _func();
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public void IncrementParallelism() => _semaphore.Release();

    public async Task DecrementParallelism()
    {
        await _pauseExecutionLock.AcquireAsync(CancellationToken.None);
        await _semaphore.WaitAsync();
        _pauseExecutionLock.Release();
    }
}
