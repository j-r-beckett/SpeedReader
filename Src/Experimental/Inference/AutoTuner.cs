// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.Threading.Channels;

namespace Experimental.Inference;

public class AutoTuner : IAsyncDisposable
{
    private readonly Throttler<(float[], int[]), (float[], int[])> _dbnetThrotter;
    private readonly Throttler<(float[], int[]), (float[], int[])> _svtrThrotter;
    private readonly CancellationTokenSource _tuneCts = new();
    private readonly Task _tuningTask;

    private readonly TimeSpan _tuneInterval = TimeSpan.FromMilliseconds(250);

    public AutoTuner(Throttler<(float[], int[]), (float[], int[])> dbnetThrotter, Throttler<(float[], int[]), (float[], int[])> svtrThrotter)
    {
        _dbnetThrotter = dbnetThrotter;
        _svtrThrotter = svtrThrotter;
        _tuningTask = Tune();
    }

    private async Task Tune()
    {
        while (true)
        {
            _tuneCts.Token.ThrowIfCancellationRequested();

            await Task.Delay(_tuneInterval);

            var incrementedSvtrParallelism = false;

            if (_svtrThrotter.QueueDepth > 10)
            {
                _svtrThrotter.IncrementParallelism();
                incrementedSvtrParallelism = true;
            }
            else if (_svtrThrotter.QueueDepth < 5)
            {
                await _svtrThrotter.DecrementParallelism();
            }

            if (!incrementedSvtrParallelism && _dbnetThrotter.QueueDepth > 4)
            {
                _dbnetThrotter.IncrementParallelism();
            }
            else if (_dbnetThrotter.QueueDepth < 2)
            {
                await _dbnetThrotter.DecrementParallelism();
            }
        }
    }

    // private async IAsyncEnumerable<TimeSpan> SortedTimes(Channel<TimeSpan> times)
    // {
    //     TimeSpan? currentTime = TimeSpan.Zero;
    //     await foreach (var time in times.Reader.ReadAllAsync(_tuneCts.Token))
    //     {
    //         if (time > currentTime)
    //         {
    //             yield return time;
    //             currentTime = time;
    //         }
    //     }
    // }

    public async ValueTask DisposeAsync()
    {
        _tuneCts.Cancel();
        try
        {
            await _tuningTask;
        }
        catch (OperationCanceledException)
        {
            // Do nothing
        }
    }
}
