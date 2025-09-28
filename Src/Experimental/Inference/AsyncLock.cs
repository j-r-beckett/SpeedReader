// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

namespace Experimental.Inference;

public class AsyncLock
{
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public async Task AcquireAsync(CancellationToken cancellationToken) => await _semaphore.WaitAsync(cancellationToken);

    public void Release() => _semaphore.Release();

    public bool IsAcquired => _semaphore.CurrentCount == 0;
}
