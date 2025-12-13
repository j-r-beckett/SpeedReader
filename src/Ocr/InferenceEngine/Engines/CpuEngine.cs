// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.DependencyInjection;
using Ocr.SmartMetrics;

namespace Ocr.InferenceEngine.Engines;

public class CpuEngine : IInferenceEngine
{
    private readonly IInferenceKernel _inferenceKernel;
    private readonly ManagedExecutor _managedExecutor;
    private readonly Sensor _sensor = new();
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _tuningTask;

    private readonly AvgGauge? _currentParallelismGauge;
    private readonly AvgGauge? _queueDepthGauge;
    private readonly AvgGauge? _queueWaitDurationGauge;
    private readonly Histogram<double>? _inferenceDurationHistogram;
    private readonly AvgGauge? _maxParallelismGauge;
    private readonly ThroughputGauge? _inferenceThroughputGauge;
    private readonly KeyValuePair<string, object?>[] _tags;

    private int _queueDepth;
    private int _currentParallelism;

    public static CpuEngine Factory(IServiceProvider serviceProvider, object? key)
    {
        var config = serviceProvider.GetRequiredKeyedService<CpuEngineConfig>(key);
        var kernel = serviceProvider.GetRequiredKeyedService<IInferenceKernel>(key);
        var meterFactory = serviceProvider.GetService<IMeterFactory>();
        return new CpuEngine(config, kernel, meterFactory);
    }

    private CpuEngine(CpuEngineConfig config, IInferenceKernel inferenceKernel, IMeterFactory? meterFactory)
    {
        _inferenceKernel = inferenceKernel;
        _managedExecutor = new ManagedExecutor(config.Parallelism);

        _tags = [new("model", config.Kernel.Model.ToString())];
        var meter = meterFactory?.Create("speedreader.inference.cpu");
        _currentParallelismGauge = meter?.CreateAvgGauge("parallelism", "tasks", "Number of parallel inference tasks", _tags);
        _queueDepthGauge = meter?.CreateAvgGauge("queue_depth", "tasks", "Number of inference tasks waiting in the queue", _tags);
        _queueWaitDurationGauge = meter?.CreateAvgGauge("queue_wait_duration", "ms", "Average time spent waiting in the queue", _tags);
        _inferenceDurationHistogram = meter?.CreateHistogram("inference_duration", "ms", "Histogram of inference durations", _tags, new InstrumentAdvice<double>()
        {
            HistogramBucketBoundaries = Enumerable.Range(0, 40).Select(i => i * 25.0).ToArray(),
        });
        _maxParallelismGauge = meter?.CreateAvgGauge("max_parallelism", "tasks", "Maximum number of parallel inference tasks", _tags);
        _inferenceThroughputGauge = meter?.CreateThroughputGauge("throughput", "tasks/sec", "Inference throughput", _tags);

        _tuningTask = config.AdaptiveTuning != null
            ? Task.Run(() => AdaptiveTune(config.AdaptiveTuning, _cts.Token))
            : Task.CompletedTask;
    }

    public int CurrentMaxCapacity() => _managedExecutor.CurrentMaxParallelism;

    public async Task<Task<(float[] OutputData, int[] OutputShape)>> Run(float[] inputData, int[] inputShape)
    {
        var queueWaitStart = Stopwatch.GetTimestamp();
        _queueDepthGauge?.Record(Interlocked.Increment(ref _queueDepth));
        return await _managedExecutor.ExecuteSingle(DoInference);

        (float[], int[]) DoInference()
        {
            var queueWaitTime = Stopwatch.GetElapsedTime(queueWaitStart).TotalMilliseconds;
            _queueWaitDurationGauge?.Record(queueWaitTime);
            _queueDepthGauge?.Record(Interlocked.Decrement(ref _queueDepth));
            _currentParallelismGauge?.Record(Interlocked.Increment(ref _currentParallelism));
            var inferenceStart = Stopwatch.GetTimestamp();
            try
            {
                Debug.Assert(inputShape.Length > 0);

                var batchedInputShape = new[] { 1 }.Concat(inputShape).ToArray();

                float[] resultData;
                int[] batchedResultShape;
                using (_sensor.RecordJob())
                {
                    (resultData, batchedResultShape) = _inferenceKernel.Execute(inputData, batchedInputShape);
                }

                var resultShape = batchedResultShape[1..];

                return (resultData, resultShape);
            }
            finally
            {
                var inferenceDuration = Stopwatch.GetElapsedTime(inferenceStart).TotalMilliseconds;
                _inferenceDurationHistogram?.Record(inferenceDuration, _tags);
                _currentParallelismGauge?.Record(Interlocked.Decrement(ref _currentParallelism));
                _maxParallelismGauge?.Record(_managedExecutor.CurrentMaxParallelism);
                _inferenceThroughputGauge?.Record();
            }
        }
    }

    private enum ActionType { Increase, Decrease, None }

    private async Task AdaptiveTune(CpuTuningParameters parameters, CancellationToken stoppingToken)
    {
        var lastAction = ActionType.None;
        var lastThroughput = 0.0;

        while (!stoppingToken.IsCancellationRequested)
        {
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
                    var waitFor = parameters.MeasurementWindowMultiplier * statistics.AvgDuration - elapsed.TotalSeconds;
                    if (waitFor <= 0)
                        break;
                    await Task.Delay(TimeSpan.FromSeconds(waitFor), stoppingToken);
                }
                statistics = _sensor.GetSummaryStatistics(startTimestamp, Stopwatch.GetTimestamp());
            }

            if (statistics.AvgParallelism < _managedExecutor.CurrentMaxParallelism - 2)
            {
                await DecrementParallelism();
                goto CLEANUP;
            }

            if (lastAction == ActionType.None)
            {
                IncrementParallelism();
                goto CLEANUP;
            }

            var dt = lastThroughput == 0 ? 0 : (statistics.BoxedThroughput - lastThroughput) / lastThroughput;

            if (lastAction == ActionType.Increase)
            {
                if (dt > parameters.ThroughputThreshold)
                {
                    IncrementParallelism();
                }
                else
                {
                    await DecrementParallelism();
                }
            }
            else
            {
                if (dt > parameters.ThroughputThreshold)
                {
                    await DecrementParallelism();
                }
                else
                {
                    IncrementParallelism();
                }
            }

        CLEANUP:
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
            if (_managedExecutor.CurrentMaxParallelism > parameters.MinParallelism)
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
        }
        GC.SuppressFinalize(this);
    }
}
