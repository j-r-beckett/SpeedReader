// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0


using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SpeedReader.Ocr.InferenceEngine.Engines;
// ReSharper disable once InconsistentNaming
public class VIPQueue<T>
{
    private readonly Lock _lock = new();
    private readonly Queue<T> _items = new();  // Stores a backlog of items when there's not enough waiters
    private readonly Queue<TaskCompletionSource<T>> _vipWaiters = new();
    private readonly Queue<TaskCompletionSource<T>> _regularWaiters = new();

    public void Enqueue(T item)
    {
        lock (_lock)
        {
            // Loop through VIP waiters until we find one that takes the item
            while (_vipWaiters.TryDequeue(out var waiter))
            {
                if (waiter.TrySetResult(item))
                    return;
            }

            // If no VIP waiters took the item, loop through regular waiters until we find one that takes the item
            while (_regularWaiters.TryDequeue(out var waiter))
            {
                if (waiter.TrySetResult(item))
                    return;
            }

            // If no waiters took the item / there were no waiters, store the item in the backlog
            _items.Enqueue(item);
        }
    }

    public async Task<T> DequeueAsync(bool vip, CancellationToken ct = default)
    {
        TaskCompletionSource<T> tcs;

        lock (_lock)
        {
            if (_items.Count > 0)
                return _items.Dequeue();  // If there's a backlog of items, return the first one

            // If there's no backlog to pull from, enqueue a new waiter
            tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
            (vip ? _vipWaiters : _regularWaiters).Enqueue(tcs);
        }

        await using var reg = ct.Register(() =>
        {
            lock (_lock)
                tcs.TrySetCanceled(ct); // Note: TCS stays in queue when canceled, skipped in Enqueue by *Try*SetResult
        });

        return await tcs.Task;
    }
}
