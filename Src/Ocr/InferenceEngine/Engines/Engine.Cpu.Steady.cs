// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Ocr.InferenceEngine.Kernels;

namespace Ocr.InferenceEngine.Engines;

public class SteadyCpuEngine : IInferenceEngine
{
    private readonly ManagedExecutor _managedExecutor;
    private readonly IInferenceKernel _inferenceKernel;
    private readonly IMetricRecorder<IInferenceEngine>? _metricRecorder;
    private readonly Sensor _sensor = new();
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _tuningTask;

    private int _queueDepth;
    private int _currentParallelism;

    public static SteadyCpuEngine Factory(IServiceProvider serviceProvider, object? key)
    {
        // TODO: use GetRequiredService instead of GetService
        var metricRecorder = serviceProvider.GetService<IMetricRecorder<IInferenceEngine>>();
        var options = serviceProvider.GetRequiredKeyedService<SteadyCpuEngineOptions>(key);
        var kernel = serviceProvider.GetRequiredKeyedService<IInferenceKernel>(key);
        return new SteadyCpuEngine(options, kernel, metricRecorder);
    }

    private SteadyCpuEngine(SteadyCpuEngineOptions options, IInferenceKernel inferenceKernel, IMetricRecorder<IInferenceEngine>? metricRecorder = null)
    {
        _managedExecutor = new ManagedExecutor(options.Parallelism);
        _inferenceKernel = inferenceKernel;
        _metricRecorder = metricRecorder;
        _tuningTask = Task.Run(() => Tune(_cts.Token));
    }

    public async Task<Task<(float[] OutputData, int[] OutputShape)>> Run(float[] inputData, int[] inputShape)
    {
        _metricRecorder?.RecordMetric("speedreader.inference.max_parallelism", _managedExecutor.CurrentMaxParallelism);
        _metricRecorder?.RecordMetric("speedreader.inference.queue_depth", Interlocked.Increment(ref _queueDepth));
        var queueWaitStart = Stopwatch.GetTimestamp();

        return await _managedExecutor.ExecuteSingle(DoInference);

        (float[], int[]) DoInference()
        {
            _metricRecorder?.RecordMetric("speedreader.inference.queue_depth", Interlocked.Decrement(ref _queueDepth));
            var queueWaitTime = Stopwatch.GetElapsedTime(queueWaitStart).TotalMilliseconds;
            _metricRecorder?.RecordMetric("speedreader.inference.queue_wait_duration", queueWaitTime);
            _metricRecorder?.RecordMetric("speedreader.inference.parallelism", Interlocked.Increment(ref _currentParallelism));
            var inferenceStartTimestamp = Stopwatch.GetTimestamp();
            try
            {
                Debug.Assert(inputShape.Length > 0); // At least one dimension

                // Add a batch size dimension. On CPU we don't batch, so this is just 1
                var batchedInputShape = new[] { 1 }.Concat(inputShape).ToArray();

                float[] resultData;
                int[] batchedResultShape;
                using (_sensor.RecordJob())
                {
                    (resultData, batchedResultShape) = _inferenceKernel.Execute(inputData, batchedInputShape);
                }

                // Strip batch size dimension that we added earlier
                var resultShape = batchedResultShape[1..];

                return (resultData, resultShape);
            }
            finally
            {
                _metricRecorder?.RecordMetric("speedreader.inference.parallelism", Interlocked.Decrement(ref _currentParallelism));
                var inferenceTime = Stopwatch.GetElapsedTime(inferenceStartTimestamp).TotalMilliseconds;
                _metricRecorder?.RecordMetric("speedreader.inference.counter", 1);
                _metricRecorder?.RecordMetric("speedreader.inference.duration", inferenceTime);
            }
        }
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
            if (statistics.AvgParallelism < _managedExecutor.CurrentMaxParallelism - 2)
            {
                await DecrementParallelism();
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
                    await DecrementParallelism();
                }
            }
            else  // lastAction must be ActionType.Decrease
            {
                if (dt > 0.05)
                {
                    // Decreasing parallelism didn't hurt throughput too badly, so decrease again
                    await DecrementParallelism();
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
            _managedExecutor.IncrementParallelism();
            lastAction = ActionType.Increase;
        }

        async Task DecrementParallelism()
        {
            if (_managedExecutor.CurrentMaxParallelism > 1)
            {
                await _managedExecutor.DecrementParallelism();
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
        await _cts.CancelAsync();
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
