// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

namespace Experimental.Inference;

public class ParallelismManager<TIn, TOut>
{
    private readonly Func<TIn, TOut> _func;
    private readonly SemaphoreSlim _semaphore;
    private readonly AsyncLock _pauseExecutionLock = new();

    public ParallelismManager(Func<TIn, TOut> func, int start)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(start);

        _func = func;
        _semaphore = new SemaphoreSlim(start);
    }

    public async Task<Task<TOut>> Call(TIn input)
    {
        await _semaphore.WaitAsync();
        while (_pauseExecutionLock.IsAcquired)
        {
            _semaphore.Release();
            await _pauseExecutionLock.AcquireAsync(CancellationToken.None);
            _pauseExecutionLock.Release();
            await _semaphore.WaitAsync();
        }

        var task = Task.Run(() => _func(input));
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
        task.ContinueWith(_ => _semaphore.Release());
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
        return task;
    }

    public void IncrementParallelism() => _semaphore.Release();

    public async Task DecrementParallelism()
    {
        await _pauseExecutionLock.AcquireAsync(CancellationToken.None);
        await _semaphore.WaitAsync();
        _pauseExecutionLock.Release();
    }
}
