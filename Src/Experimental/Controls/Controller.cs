// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using Experimental.Inference;

namespace Experimental.Controls;

public class Controller
{
    private readonly IExecutor _executor;
    private readonly int _oscillations;
    private readonly InferenceTelemetryRecorder _telemetryRecorder;

    public Controller(IExecutor executor, int oscillations, InferenceTelemetryRecorder telemetryRecorder)
    {
        _executor = executor;
        _oscillations = oscillations;
        _telemetryRecorder = telemetryRecorder;
    }

    public bool IsOscillating { get; private set; } = false;

    private enum ActionType { Increase, Decrease, None }

    public async Task Tune(CancellationToken stoppingToken)
    {
        // State
        var lastAction = ActionType.None;
        var lastThroughput = 0.0;
        long oscillationCounter = 0;

        Console.WriteLine("Starting controller");

        // Main loop
        while (!stoppingToken.IsCancellationRequested)
        {
            // Keep an eye on avg processing duration, wait for at least 8 * avg duration to ensure we get stable
            // throughput and parallelism measurements
            var start = SharedClock.Now;
            var statistics = _executor.Sensor.GetSummaryStatistics(start, SharedClock.Now);
            while (true)
            {
                if (statistics.AvgDuration == 0)
                {
                    await Task.Delay(20, stoppingToken);
                }
                else
                {
                    var waitFor = start.TotalSeconds + 8 * statistics.AvgDuration - SharedClock.Now.TotalSeconds;
                    if (waitFor <= 0)
                        break;
                    await Task.Delay(TimeSpan.FromSeconds(waitFor), stoppingToken);
                }
                statistics = _executor.Sensor.GetSummaryStatistics(start, SharedClock.Now);
            }

            Console.WriteLine("Average duration requirements meant");

            // If we have plenty of headroom, bring down max
            if (statistics.AvgParallelism < _executor.CurrentMaxParallelism - 2)
            {
                Console.WriteLine("Slack detected, reducing parallelism");
                await DecrementParallelism();
                oscillationCounter = 0;  // Slack in the system detected, we are no longer oscillating
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
                    oscillationCounter++;
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
                    oscillationCounter++;
                }
            }

        CLEANUP:
            var maxParallelism = _executor.CurrentMaxParallelism + (lastAction == ActionType.Increase ? -1 : 1);
            Console.WriteLine("Statistics:" +
                              $"Max parallelism: {maxParallelism}, " +
                              $"Observed parallelism: {statistics.AvgParallelism}, " +
                              $"Throughput: {statistics.Throughput}, " +
                              $"Boxed throughput: {statistics.BoxedThroughput}, " +
                              $"Duration: {statistics.AvgDuration}");
            IsOscillating = oscillationCounter > _oscillations;
            lastThroughput = statistics.BoxedThroughput;
            // _telemetryRecorder.RecordDuration(TimeSpan.FromSeconds(statistics.AvgDuration));
            Console.Write($"Recording durations: {string.Join(", ", statistics.Enclosed.Select(p => (p.End - p.Start).TotalMilliseconds))}");
            foreach (var (s, e) in statistics.Enclosed)
            {
                _telemetryRecorder.RecordDuration(e - s);
            }
            _telemetryRecorder.RecordThroughput(statistics.Throughput);
            _telemetryRecorder.RecordParallelism(statistics.AvgParallelism);
            _telemetryRecorder.RecordMaxParallelism(_executor.CurrentMaxParallelism);
            _executor.Sensor.Prune(SharedClock.Now);
        }

        return;

        void IncrementParallelism()
        {
            Console.WriteLine($"Incrementing parallelism to {_executor.CurrentMaxParallelism + 1}");
            _executor.IncrementParallelism();
            lastAction = ActionType.Increase;
        }

        async Task DecrementParallelism()
        {
            if (_executor.CurrentMaxParallelism > 1)
            {
                Console.WriteLine($"Decrementing parallelism to {_executor.CurrentMaxParallelism - 1}");
                await _executor.DecrementParallelism();
                lastAction = ActionType.Decrease;
            }
            else
            {
                Console.WriteLine("Can't decrement parallelism, already at 1");
                lastAction = ActionType.None;
            }
        }
    }
}
