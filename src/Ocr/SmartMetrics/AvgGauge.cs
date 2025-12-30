// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.Diagnostics.Metrics;

namespace SpeedReader.Ocr.SmartMetrics;

public class AvgGauge
{
    private readonly Lock _lock = new();
    private double _sum;
    private int _count;
    private readonly KeyValuePair<string, object?>[] _tags;

    public AvgGauge(KeyValuePair<string, object?>[] tags) => _tags = tags;

    public void Record(double value)
    {
        lock (_lock)
        {
            _sum += value;
            _count++;
        }
    }

    public Measurement<double> CollectAndReset()
    {
        lock (_lock)
        {
            var avg = _count == 0 ? 0 : _sum / _count;
            _sum = 0;
            _count = 0;
            return new Measurement<double>(avg, _tags);
        }
    }
}

public static partial class SmartMetricsExtensions
{
    public static AvgGauge CreateAvgGauge(this Meter meter, string name, string unit,
        string? description = null, IEnumerable<KeyValuePair<string, object?>>? tags = null)
    {
        var gauge = new AvgGauge(tags?.ToArray() ?? []);
        meter.CreateObservableGauge(name, gauge.CollectAndReset, unit, description);
        return gauge;
    }
}

