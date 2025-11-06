// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.Threading.Channels;
using Ocr.Inference;

namespace Ocr.Telemetry;

public sealed class ContainerMetricsCollector : IDisposable
{
    private readonly Task _collectionTask;
    private readonly CancellationTokenSource _cts = new();

    public ContainerMetricsCollector() => _collectionTask = Task.Run(CollectionLoop);

    public static bool IsRunningInContainer() => File.Exists("/sys/fs/cgroup/memory.max");

    private async Task CollectionLoop()
    {
        var lastCpuCheck = SharedClock.Now;
        var lastCpuUsage = ReadCpuUsageMicroseconds();

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
                Console.WriteLine($"Error collecting container metrics: {ex}");
            }
        }

        void CollectMetrics(TimeSpan timestamp)
        {
            // Memory limit (bytes)
            var memoryLimit = ReadMemoryLimit();
            if (memoryLimit > 0)
            {
                MetricRecorder.RecordMetric("container.memory.limit_bytes", memoryLimit);
                // _writer.TryWrite(new MetricPoint(timestamp, "container.memory.limit_bytes", memoryLimit));
            }

            // Memory usage (bytes)
            var memoryUsage = ReadMemoryUsage();
            if (memoryUsage > 0)
            {
                MetricRecorder.RecordMetric("container.memory.usage_bytes", memoryUsage);
                // _writer.TryWrite(new MetricPoint(timestamp, "container.memory.usage_bytes", memoryUsage));
            }

            // CPU limit (cores)
            var cpuLimit = ReadCpuLimit();
            if (cpuLimit > 0)
            {
                MetricRecorder.RecordMetric("container.cpu.limit_cores", cpuLimit);
                // _writer.TryWrite(new MetricPoint(timestamp, "container.cpu.limit_cores", cpuLimit));
            }

            // CPU usage (cores)
            var now = SharedClock.Now;
            var currentCpuUsage = ReadCpuUsageMicroseconds();
            if (currentCpuUsage > 0 && lastCpuUsage > 0)
            {
                var usageDeltaMicros = currentCpuUsage - lastCpuUsage;
                var timeDeltaSeconds = (now - lastCpuCheck).TotalSeconds;

                if (timeDeltaSeconds > 0)
                {
                    var microsPerSecond = 1_000_000.0;
                    var cpuUsageCores = usageDeltaMicros / (timeDeltaSeconds * microsPerSecond);
                    MetricRecorder.RecordMetric("container.cpu.usage_cores", cpuUsageCores);
                    // _writer.TryWrite(new MetricPoint(timestamp, "container.cpu.usage_cores", cpuUsageCores));
                }
            }

            lastCpuCheck = now;
            lastCpuUsage = currentCpuUsage;
        }
    }

    private static long ReadMemoryLimit()
    {
        if (File.Exists("/sys/fs/cgroup/memory.max"))
        {
            var content = File.ReadAllText("/sys/fs/cgroup/memory.max").Trim();
            if (content != "max" && long.TryParse(content, out var limit))
            {
                return limit;
            }
        }

        return 0;
    }

    private static long ReadMemoryUsage()
    {
        if (File.Exists("/sys/fs/cgroup/memory.current"))
        {
            if (long.TryParse(File.ReadAllText("/sys/fs/cgroup/memory.current").Trim(), out var usage))
            {
                return usage;
            }
        }

        return 0;
    }

    private static double ReadCpuLimit()
    {
        if (File.Exists("/sys/fs/cgroup/cpu.max"))
        {
            var parts = File.ReadAllText("/sys/fs/cgroup/cpu.max").Trim().Split(' ');
            if (parts.Length == 2 && parts[0] != "max" &&
                long.TryParse(parts[0], out var quota) &&
                long.TryParse(parts[1], out var period) &&
                period > 0)
            {
                return (double)quota / period;
            }
        }

        return 0;
    }

    private static long ReadCpuUsageMicroseconds()
    {
        if (File.Exists("/sys/fs/cgroup/cpu.stat"))
        {
            foreach (var line in File.ReadAllLines("/sys/fs/cgroup/cpu.stat"))
            {
                if (line.StartsWith("usage_usec "))
                {
                    var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length == 2 && long.TryParse(parts[1], out var usec))
                    {
                        return usec;
                    }
                }
            }
        }

        return 0;
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
