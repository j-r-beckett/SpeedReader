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

            // await Task.Delay(_tuneInterval);
            var observedParallelism = await Task.WhenAll(GetAverageParallelism(_dbnetThrotter, _tuneInterval), GetAverageParallelism(_svtrThrotter, _tuneInterval));
            var (dbnetParallelism, svtrParallelism) = (observedParallelism[0], observedParallelism[1]);

            var incrementedSvtrParallelism = false;

            if (_svtrThrotter.QueueDepth >= Math.Ceiling(svtrParallelism * 0.5))
            {
                _svtrThrotter.IncrementParallelism();
                incrementedSvtrParallelism = true;
            }

            if (!incrementedSvtrParallelism && _dbnetThrotter.QueueDepth >= Math.Ceiling(dbnetParallelism * 0.5))
            {
                _dbnetThrotter.IncrementParallelism();
            }
        }
    }

    private async Task<double> GetAverageParallelism(Throttler<(float[], int[]), (float[], int[])> throttler, TimeSpan period)
    {
        const int numSamples = 10;
        double sum = throttler.QueueDepth;  // record first sample
        var start = SharedClock.Now;
        var samplePeriod = period / numSamples;
        for (var i = 0; i < numSamples - 1; i++)
        {
            var samplePeriodStart = start + i * samplePeriod;
            var samplePeriodEnd = start + (i + 1) * samplePeriod;
            await Task.Delay(samplePeriodStart - samplePeriodEnd, _tuneCts.Token);
            sum += throttler.QueueDepth;
        }
        return sum / numSamples;
    }

    private async IAsyncEnumerable<TimeSpan> SortedTimes(Channel<TimeSpan> times)
    {
        TimeSpan? currentTime = TimeSpan.Zero;
        await foreach (var time in times.Reader.ReadAllAsync(_tuneCts.Token))
        {
            if (time > currentTime)
            {
                yield return time;
                currentTime = time;
            }
        }
    }

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
