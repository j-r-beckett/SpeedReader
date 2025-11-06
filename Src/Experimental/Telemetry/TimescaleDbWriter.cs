// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.Text.Json;
using System.Threading.Channels;
using Experimental.Inference;
using Npgsql;
using NpgsqlTypes;

namespace Experimental.Telemetry;


public static class MetricRecorder
{
    private static readonly Channel<MetricPoint> _channel;
    private static readonly Task _writerTask;
    private static readonly CancellationTokenSource _cts = new();
    private static readonly TimeSpan _flushInterval;
    private static readonly NpgsqlDataSource? _dataSource;

    static MetricRecorder()
    {
        _channel = Channel.CreateUnbounded<MetricPoint>();
        _flushInterval = TimeSpan.FromSeconds(5);

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

    public static void RecordMetric(TimeSpan timestamp, string name, double value, Dictionary<string, string>? tags = null) =>
        _channel.Writer.TryWrite(new MetricPoint(timestamp, name, value, tags));

    public static void RecordMetric(string name, double value, Dictionary<string, string>? tags = null) =>
        RecordMetric(SharedClock.Now, name, value, tags);

    private static async Task WriterLoop()
    {
        while (!_cts.Token.IsCancellationRequested)
        {
            try
            {
                var loopStart = SharedClock.Now;
                var batch = new List<MetricPoint>();

                // Read all available metrics from channel
                while (_channel.Reader.TryRead(out var metric))
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
}
