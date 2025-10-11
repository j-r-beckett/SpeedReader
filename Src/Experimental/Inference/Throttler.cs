// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.Diagnostics;
using System.Threading.Channels;

namespace Experimental.Inference;

public class Throttler<TIn, TOut>
{
    private readonly Func<TIn, TOut> _func;
    private readonly SemaphoreSlim _semaphore;
    private readonly AsyncLock _pauseExecutionLock = new();
    private int _queueDepth = 0;

    // Paired start and end times, good for calculating latency per item. Only written to once we have both (start, end)
    public readonly Channel<(TimeSpan Start, TimeSpan End)> Latencies = Channel.CreateUnbounded<(TimeSpan Start, TimeSpan End)>();

    // Start times, written to immediately once we have start
    public readonly Channel<TimeSpan> StartTimes = Channel.CreateUnbounded<TimeSpan>();

    // End times, written to once we have end
    public readonly Channel<TimeSpan> EndTimes = Channel.CreateUnbounded<TimeSpan>();

    private static readonly Stopwatch _clock = Stopwatch.StartNew();  // Synchronized monotonic clock

    public Throttler(Func<TIn, TOut> func, int start)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(start);

        _func = func;
        _semaphore = new SemaphoreSlim(start);
    }

    public async Task<Task<TOut>> Call(TIn input)
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
            var start = _clock.Elapsed;
            StartTimes.Writer.TryWrite(start);
            var result = _func(input);
            var end = _clock.Elapsed;
            EndTimes.Writer.TryWrite(end);
            Latencies.Writer.TryWrite((start, end));
            return result;
        });
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
        task.ContinueWith(_ => _semaphore.Release());
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
        return task;
    }

    public int QueueDepth => _queueDepth;

    public void IncrementParallelism() => _semaphore.Release();

    public async Task DecrementParallelism()
    {
        await _pauseExecutionLock.AcquireAsync(CancellationToken.None);
        await _semaphore.WaitAsync();
        _pauseExecutionLock.Release();
    }
}
