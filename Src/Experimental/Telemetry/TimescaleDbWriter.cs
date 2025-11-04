// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.Text.Json;
using System.Threading.Channels;
using Experimental.Inference;
using Npgsql;
using NpgsqlTypes;

namespace Experimental.Telemetry;

public sealed class TimescaleDbWriter : IDisposable
{
    private readonly ChannelReader<MetricPoint> _reader;
    private readonly Task _writerTask;
    private readonly CancellationTokenSource _cts = new();
    private readonly TimeSpan _flushInterval;
    private readonly NpgsqlDataSource? _dataSource;

    public TimescaleDbWriter(ChannelReader<MetricPoint> reader, TimeSpan? flushInterval = null)
    {
        _reader = reader;
        _flushInterval = flushInterval ?? TimeSpan.FromSeconds(5);

        var connectionString = Environment.GetEnvironmentVariable("TIMESCALEDB_CONNECTION_STRING");
        if (connectionString == null)
        {
            Console.WriteLine("TIMESCALEDB_CONNECTION_STRING not set, metrics will not be written to database");
            _writerTask = Task.CompletedTask;
            return;
        }

        _dataSource = NpgsqlDataSource.Create(connectionString);
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

                // Write batch to TimescaleDB using Binary COPY
                if (batch.Count > 0)
                {
                    await using var connection = await _dataSource!.OpenConnectionAsync(_cts.Token);
                    await using var importer = await connection.BeginBinaryImportAsync(
                        "COPY metrics (time, metric_name, value, tags) FROM STDIN (FORMAT BINARY)",
                        _cts.Token);

                    foreach (var metric in batch)
                    {
                        await importer.StartRowAsync(_cts.Token);
                        await importer.WriteAsync(metric.Timestamp.ToUtc(), NpgsqlDbType.TimestampTz, _cts.Token);
                        await importer.WriteAsync(metric.Name, NpgsqlDbType.Text, _cts.Token);
                        await importer.WriteAsync(metric.Value, NpgsqlDbType.Double, _cts.Token);

                        if (metric.Tags != null)
                        {
                            var tagsJson = JsonSerializer.Serialize(metric.Tags);
                            await importer.WriteAsync(tagsJson, NpgsqlDbType.Jsonb, _cts.Token);
                        }
                        else
                        {
                            await importer.WriteNullAsync(_cts.Token);
                        }
                    }

                    await importer.CompleteAsync(_cts.Token);
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
        _dataSource?.Dispose();
    }
}
