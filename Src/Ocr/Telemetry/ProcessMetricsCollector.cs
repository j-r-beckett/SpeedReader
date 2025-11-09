// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.Diagnostics;
using System.Threading.Channels;

namespace Ocr.Telemetry;

public sealed class ProcessMetricsCollector : IDisposable
{
    private readonly Task _collectionTask;
    private readonly CancellationTokenSource _cts = new();

    public ProcessMetricsCollector() => _collectionTask = Task.Run(CollectionLoop);

    private async Task CollectionLoop()
    {
        var process = Process.GetCurrentProcess();
        var lastCpuCheckTimestamp = Stopwatch.GetTimestamp();
        var lastCpuTime = process.TotalProcessorTime;

        while (!_cts.Token.IsCancellationRequested)
        {
            try
            {
                var loopStartTimestamp = Stopwatch.GetTimestamp();
                CollectMetrics();
                var elapsed = Stopwatch.GetElapsedTime(loopStartTimestamp);
                var remaining = TimeSpan.FromSeconds(1) - elapsed;

                if (remaining > TimeSpan.Zero)
                {
                    await Task.Delay(remaining, _cts.Token);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error collecting process metrics: {ex}");
            }
        }

        return;

        void CollectMetrics()
        {
            process.Refresh();

            var workingSet = process.WorkingSet64;
            MetricRecorder.RecordMetric("process.memory.working_set_bytes", workingSet);

            var nowTimestamp = Stopwatch.GetTimestamp();
            var currentCpuTime = process.TotalProcessorTime;
            var cpuDelta = (currentCpuTime - lastCpuTime).TotalMilliseconds;
            var timeDelta = Stopwatch.GetElapsedTime(lastCpuCheckTimestamp, nowTimestamp).TotalMilliseconds;

            if (timeDelta > 0)
            {
                var cpuUsageCores = cpuDelta / timeDelta;
                MetricRecorder.RecordMetric("process.cpu.usage_cores", cpuUsageCores);
            }

            lastCpuCheckTimestamp = nowTimestamp;
            lastCpuTime = currentCpuTime;
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        try
        {
            _collectionTask.Wait(TimeSpan.FromSeconds(5));
        }
        catch (AggregateException)
        {
        }

        _cts.Dispose();
    }
}
