// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Ocr.InferenceEngine.Kernels;

namespace Ocr.InferenceEngine.Engines;

public record AdaptiveCpuEngineOptions : EngineOptions
{
    public AdaptiveCpuEngineOptions(int initialParallelism) => InitialParallelism = initialParallelism;

    public int InitialParallelism { get; }
}

public class AdaptiveCpuEngine : IInferenceEngine
{
    private readonly TaskPool<(float[], int[])> _taskPool;
    private readonly IInferenceKernel _inferenceKernel;
    private readonly Sensor _sensor = new();
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _tuningTask;

    public static AdaptiveCpuEngine Factory(IServiceProvider serviceProvider, object? key)
    {
        var options = serviceProvider.GetRequiredKeyedService<AdaptiveCpuEngineOptions>(key);
        var kernel = serviceProvider.GetRequiredKeyedService<IInferenceKernel>(key);
        return new AdaptiveCpuEngine(options, kernel);
    }

    private AdaptiveCpuEngine(AdaptiveCpuEngineOptions options, IInferenceKernel inferenceKernel)
        : this(new TaskPool<(float[], int[])>(options.InitialParallelism), inferenceKernel) { }

    private AdaptiveCpuEngine(TaskPool<(float[], int[])> taskPool, IInferenceKernel inferenceKernel)
    {
        _taskPool = taskPool;
        _inferenceKernel = inferenceKernel;
        _tuningTask = Task.Run(() => Tune(_cts.Token));
    }

    public async Task<Task<(float[] OutputData, int[] OutputShape)>> Run(float[] inputData, int[] inputShape)
    {
        Debug.Assert(inputShape.Length > 0);  // At least one dimension
        var batchedShape = new[] { 1 }.Concat(inputShape).ToArray();  // Add a batch size dimension. On CPU we don't batch, so this is just 1

        var inferenceTask = Task.Run(() =>
        {
            using (_sensor.RecordJob())
            {
                var (data, shape) = _inferenceKernel.Execute(inputData, batchedShape);
                var unbatchedShape = shape[1..]; // Strip batch size dimension that we added earlier
                return (data, unbatchedShape);
            }
        });

        return await _taskPool.Execute(() => inferenceTask);
    }

    private enum ActionType { Increase, Decrease, None }

    public async Task Tune(CancellationToken stoppingToken)
    {
        // State
        var lastAction = ActionType.None;
        var lastThroughput = 0.0;

        // Main loop
        while (!stoppingToken.IsCancellationRequested)
        {
            // Keep an eye on avg processing duration, wait for at least 8 * avg duration to ensure we get stable
            // throughput and parallelism measurements
            var startTimestamp = Stopwatch.GetTimestamp();
            var statistics = _sensor.GetSummaryStatistics(startTimestamp, Stopwatch.GetTimestamp());
            while (true)
            {
                if (statistics.AvgDuration == 0)
                {
                    await Task.Delay(20, stoppingToken);
                }
                else
                {
                    var elapsed = Stopwatch.GetElapsedTime(startTimestamp);
                    var waitFor = 8 * statistics.AvgDuration - elapsed.TotalSeconds;
                    if (waitFor <= 0)
                        break;
                    await Task.Delay(TimeSpan.FromSeconds(waitFor), stoppingToken);
                }
                statistics = _sensor.GetSummaryStatistics(startTimestamp, Stopwatch.GetTimestamp());
            }

            // If we have plenty of headroom, bring down max
            if (statistics.AvgParallelism < _taskPool.PoolSize - 2)
            {
                DecrementParallelism();
                goto CLEANUP;
            }

            // If we haven't taken any actions yet, take an increase action
            if (lastAction == ActionType.None)
            {
                IncrementParallelism();
                goto CLEANUP;
            }

            // Derivative of throughput with respect to time
            var dt = lastThroughput == 0 ? 0 : (statistics.BoxedThroughput - lastThroughput) / lastThroughput;

            // Keep increasing as long as each increase is significant (adding a thread produces a > 5% speedup over
            // previous thread count).  Decrease as long as each decrease is insignificant (removing a thread produces
            // a < 5% slowdown vs previous thread count). This causes us to oscillate around the parallelism level
            // at further parallelism increases do not produce significant speedups.
            // We increment oscillationCounter each time we decrease after increasing, or increase after decreasing.
            if (lastAction == ActionType.Increase)
            {
                if (dt > 0.05)
                {
                    // Increasing parallelism significantly increased throughput, so increase again
                    IncrementParallelism();
                }
                else
                {
                    // Increasing parallelism didn't significantly increase throughput, so decrease
                    DecrementParallelism();
                }
            }
            else  // lastAction must be ActionType.Decrease
            {
                if (dt > 0.05)
                {
                    // Decreasing parallelism didn't hurt throughput too badly, so decrease again
                    DecrementParallelism();
                }
                else
                {
                    // Decreasing parallelism hurt throughput too much, so increase
                    IncrementParallelism();
                }
            }

        CLEANUP:
            // var maxParallelism = _taskPool.PoolSize + (lastAction == ActionType.Increase ? -1 : 1);
            // MetricRecorder.RecordMetric("speedreader.inference.max_parallelism", maxParallelism, _telemetryTags);
            lastThroughput = statistics.BoxedThroughput;
            _sensor.Prune(Stopwatch.GetTimestamp());
        }

        return;

        void IncrementParallelism()
        {
            _taskPool.IncreasePoolSize(1);
            lastAction = ActionType.Increase;
        }

        void DecrementParallelism()
        {
            if (_taskPool.PoolSize > 1)
            {
                _taskPool.IncreasePoolSize(-1);
                lastAction = ActionType.Decrease;
            }
            else
            {
                lastAction = ActionType.None;
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();
        try
        {
            await _tuningTask;
        }
        catch (OperationCanceledException)
        {
            // Do nothing
        }
        GC.SuppressFinalize(this);
    }
}
