// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.Diagnostics;
using System.Threading.Channels;
using Experimental.Inference;

namespace Experimental.Telemetry;

public sealed class ProcessMetricsCollector : IDisposable
{
    private readonly Task _collectionTask;
    private readonly CancellationTokenSource _cts = new();

    public ProcessMetricsCollector() => _collectionTask = Task.Run(CollectionLoop);

    private async Task CollectionLoop()
    {
        var process = Process.GetCurrentProcess();
        var lastCpuCheck = SharedClock.Now;
        var lastCpuTime = process.TotalProcessorTime;

        while (!_cts.Token.IsCancellationRequested)
        {
            try
            {
                var start = SharedClock.Now;
                CollectMetrics(start);
                var elapsed = SharedClock.Now - start;
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

        void CollectMetrics(TimeSpan timestamp)
        {
            process.Refresh();

            var workingSet = process.WorkingSet64;
            MetricRecorder.RecordMetric("process.memory.working_set_bytes", workingSet);
            // _writer.TryWrite(new MetricPoint(timestamp, "process.memory.working_set_bytes", workingSet));

            var now = SharedClock.Now;
            var currentCpuTime = process.TotalProcessorTime;
            var cpuDelta = (currentCpuTime - lastCpuTime).TotalMilliseconds;
            var timeDelta = (now - lastCpuCheck).TotalMilliseconds;

            if (timeDelta > 0)
            {
                var cpuUsageCores = cpuDelta / timeDelta;
                MetricRecorder.RecordMetric("process.cpu.usage_cores", cpuUsageCores);
                // _writer.TryWrite(new MetricPoint(timestamp, "process.cpu.usage_cores", cpuUsageCores));
            }

            lastCpuCheck = now;
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
