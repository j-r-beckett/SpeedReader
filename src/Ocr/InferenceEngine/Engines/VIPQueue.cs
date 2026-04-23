// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

namespace SpeedReader.Ocr.InferenceEngine.Engines;

// ReSharper disable once InconsistentNaming
public class VIPQueue<T>
{
    private readonly Lock _lock = new();
    private readonly Queue<T> _items = new();  // Stores a backlog of items when there's not enough waiters
    private readonly Queue<TaskCompletionSource<T>>?[] _waiters;

    public VIPQueue(int vipLevels)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(vipLevels, 1, nameof(vipLevels));
        _waiters = new Queue<TaskCompletionSource<T>>?[vipLevels];
    }

    public void Enqueue(T item)
    {
        lock (_lock)
        {
            // Loop through waiters in descending VIP order until we find one that takes the item
            for (var vip = _waiters.Length - 1; vip >= 0; vip--)
            {
                var waiterQueue = _waiters[vip];
                if (waiterQueue == null)
                    continue;

                while (waiterQueue.TryDequeue(out var waiter))
                {
                    if (waiter.TrySetResult(item))
                        return;
                }

                if (waiterQueue.Count == 0)
                    _waiters[vip] = null;
            }

            // If no waiters took the item or there were no waiters, store the item in the backlog
            _items.Enqueue(item);
        }
    }

    public async Task<T> DequeueAsync(int vip, CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(vip, nameof(vip));
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(vip, _waiters.Length, nameof(vip));

        TaskCompletionSource<T> tcs;

        lock (_lock)
        {
            // If there's a backlog of items, return the first one
            if (_items.Count > 0)
                return _items.Dequeue();

            // If there's no backlog to pull from, enqueue a new waiter
            tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
            var waiterQueue = _waiters[vip] ??= new Queue<TaskCompletionSource<T>>();
            waiterQueue.Enqueue(tcs);
        }

        await using var reg = ct.Register(() =>
        {
            lock (_lock)
                tcs.TrySetCanceled(ct); // Note: TCS stays in queue when canceled, skipped in Enqueue by TrySetResult
        });

        return await tcs.Task;
    }
}
