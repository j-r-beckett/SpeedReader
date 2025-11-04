// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.Threading.Channels;
using Experimental.Inference;

namespace Experimental.Telemetry;

public sealed class TimescaleDbWriter : IDisposable
{
    private readonly ChannelReader<MetricPoint> _reader;
    private readonly Task _writerTask;
    private readonly CancellationTokenSource _cts = new();
    private readonly TimeSpan _flushInterval;

    public TimescaleDbWriter(ChannelReader<MetricPoint> reader, TimeSpan? flushInterval = null)
    {
        _reader = reader;
        _flushInterval = flushInterval ?? TimeSpan.FromSeconds(5);
        _writerTask = Task.Run(WriterLoop);
    }

    private async Task WriterLoop()
    {
        var startTime = DateTime.UtcNow;

        while (!_cts.Token.IsCancellationRequested)
        {
            try
            {
                var loopStart = SharedClock.Now;
                var batch = new List<MetricPoint>();

                // Read all available metrics from channel
                while (_reader.TryRead(out var metric))
                {
                    batch.Add(metric);
                }

                // Log the batch (will be replaced with TimescaleDB write)
                if (batch.Count > 0)
                {
                    Console.WriteLine($"[TimescaleDbWriter] Collected {batch.Count} metrics:");
                    foreach (var metric in batch)
                    {
                        var utcTime = startTime + metric.Timestamp;
                        Console.WriteLine($"  {utcTime:yyyy-MM-dd HH:mm:ss.fff} | {metric.Name} = {metric.Value}");
                    }
                }

                // Calculate remaining wait time
                var elapsed = SharedClock.Now - loopStart;
                var remaining = _flushInterval - elapsed;

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
                Console.WriteLine($"Error in TimescaleDbWriter: {ex}");
            }
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        try
        {
            _writerTask.Wait(TimeSpan.FromSeconds(5));
        }
        catch (AggregateException)
        {
        }

        _cts.Dispose();
    }
}
