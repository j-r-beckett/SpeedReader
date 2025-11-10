// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace Ocr.InferenceEngine.Engines;

public interface IMetricRecorder
{
    void RecordMetric(string name, double value, Dictionary<string, string>? tags = null);
    IMetricRecorder WithTags(Dictionary<string, string> tags);
}

public class CpuEngine : IInferenceEngine
{
    private readonly CpuEngineConfig _config;
    private readonly IInferenceKernel _inferenceKernel;
    private readonly ManagedExecutor _managedExecutor;
    private readonly Sensor _sensor = new();
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _tuningTask;
    private readonly IMetricRecorder? _metricRecorder;

    private int _queueDepth;
    private int _currentParallelism;

    private const string MetricPrefix = "speedreader.inference.cpu.";


    public static CpuEngine Factory(IServiceProvider serviceProvider, object? key)
    {
        var config = serviceProvider.GetRequiredKeyedService<CpuEngineConfig>(key);
        var kernel = serviceProvider.GetRequiredKeyedService<IInferenceKernel>(key);
        var metricRecorder = serviceProvider.GetKeyedService<IMetricRecorder>(key);
        return new CpuEngine(config, kernel, metricRecorder);
    }

    private CpuEngine(CpuEngineConfig config, IInferenceKernel inferenceKernel, IMetricRecorder? metricRecorder = null)
    {
        _config = config;
        _inferenceKernel = inferenceKernel;
        _managedExecutor = new ManagedExecutor(config.Parallelism);
        var telemetryTags = new Dictionary<string, string> { ["model"] = config.Kernel.Model.ToString() };
        _metricRecorder = metricRecorder?.WithTags(telemetryTags);

        _tuningTask = config.AdaptiveTuning != null
            ? Task.Run(() => AdaptiveTune(config.AdaptiveTuning, _cts.Token))
            : Task.CompletedTask;
    }

    public int QueueDepth => _queueDepth;

    public int CurrentMaxParallelism => _managedExecutor.CurrentMaxParallelism;

    public int CurrentParallelism => _currentParallelism;

    public int CurrentBatchSize => 1;

    public async Task<Task<(float[] OutputData, int[] OutputShape)>> Run(float[] inputData, int[] inputShape)
    {
        var queueWaitStart = Stopwatch.GetTimestamp();
        _metricRecorder?.RecordMetric($"{MetricPrefix}queue_depth", Interlocked.Increment(ref _queueDepth));
        return await _managedExecutor.ExecuteSingle(DoInference);

        (float[], int[]) DoInference()
        {
            var queueWaitTime = Stopwatch.GetElapsedTime(queueWaitStart).TotalMilliseconds;
            _metricRecorder?.RecordMetric($"{MetricPrefix}queue_wait_duration", queueWaitTime);
            _metricRecorder?.RecordMetric($"{MetricPrefix}queue_depth", Interlocked.Decrement(ref _queueDepth));
            _metricRecorder?.RecordMetric($"{MetricPrefix}parallelism", Interlocked.Increment(ref _currentParallelism));
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
                var inferenceEndTime = Stopwatch.GetElapsedTime(inferenceStart).TotalMilliseconds;
                _metricRecorder?.RecordMetric($"{MetricPrefix}inference_duration", inferenceEndTime);
                _metricRecorder?.RecordMetric($"{MetricPrefix}parallelism", Interlocked.Decrement(ref _currentParallelism));
                _metricRecorder?.RecordMetric($"{MetricPrefix}max_parallelism", _managedExecutor.CurrentMaxParallelism);
                _metricRecorder?.RecordMetric($"{MetricPrefix}counter", 1);
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
