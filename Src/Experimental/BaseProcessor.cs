// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

namespace Experimental;

public abstract class BaseProcessor<TIn, TOut>
{
    private readonly SemaphoreSlim _semaphore;

    protected BaseProcessor(int maxParallelism) => _semaphore = new SemaphoreSlim(maxParallelism);

    public async Task<TOut> Process(TIn input)
    {
        await _semaphore.WaitAsync();
        var result = await ProcessProtected(input);
        _semaphore.Release();
        return result;
    }

    protected abstract Task<TOut> ProcessProtected(TIn input);

    protected async Task<T> RunOutside<T>(Func<Task<Task<T>>> func)
    {
        var queuedWorkItem = await func();
        _semaphore.Release();
        var result = await queuedWorkItem;
        await _semaphore.WaitAsync();
        return result;
    }
}
