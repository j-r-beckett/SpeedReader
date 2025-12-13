// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Ocr.SmartMetrics;

public class ThroughputGauge
{
    private readonly Lock _lock = new();
    private long? _lastReset;
    private int _count;
    private readonly KeyValuePair<string, object?>[] _tags;

    public ThroughputGauge(KeyValuePair<string, object?>[] tags) => _tags = tags;

    public void Record()
    {
        lock (_lock)
        {
            _lastReset ??= Stopwatch.GetTimestamp();
            _count++;
        }
    }

    public Measurement<double> CollectAndReset()
    {
        lock (_lock)
        {
            var now = Stopwatch.GetTimestamp();
            var elapsedSeconds = _lastReset == null ? 0 : Stopwatch.GetElapsedTime(_lastReset.Value, now).TotalSeconds;
            var throughput = elapsedSeconds == 0 ? 0 : _count / elapsedSeconds;
            if (_lastReset != null)
                _lastReset = now;
            _count = 0;
            return new Measurement<double>(throughput, _tags);
        }
    }
}

public static partial class SmartMetricsExtensions
{
    public static ThroughputGauge CreateThroughputGauge(this Meter meter, string name, string unit,
        string? description = null, IEnumerable<KeyValuePair<string, object?>>? tags = null)
    {
        var gauge = new ThroughputGauge(tags?.ToArray() ?? []);
        meter.CreateObservableGauge(name, gauge.CollectAndReset, unit, description);
        return gauge;
    }
}
